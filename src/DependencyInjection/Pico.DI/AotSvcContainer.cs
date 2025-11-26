using Pico.DI.Generated;

namespace Pico.DI;

/// <summary>
/// AOT-friendly service container that uses compile-time generated code
/// </summary>
public sealed class AotSvcContainer : ISvcContainer
{
    private readonly ConcurrentDictionary<Type, List<SvcDescriptor>> _descriptors = new();
    private readonly Lazy<ISvcProvider> _lazyProvider;
    private readonly Lock _lock = new();

    public AotSvcContainer()
    {
        _lazyProvider = new Lazy<ISvcProvider>(
            () => new AotSvcProvider(this),
            LazyThreadSafetyMode.ExecutionAndPublication
        );

        // Register compile-time services
        this.RegisterCompileTimeServices();
    }

    public ISvcContainer Register(SvcDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        _descriptors.TryAdd(descriptor.ServiceType, []);
        _descriptors[descriptor.ServiceType].Add(descriptor);

        return this;
    }

    public ISvcProvider GetProvider() => _lazyProvider.Value;

    public List<SvcDescriptor> GetDescriptors(Type type)
    {
        var descriptors = _descriptors.GetValueOrDefault(type);

        if (descriptors is not null)
            return descriptors;

        // Try compile-time resolution
        var compileTimeDescriptor = GetCompileTimeDescriptor(type);
        if (compileTimeDescriptor is not null)
        {
            Register(compileTimeDescriptor);
            return _descriptors.GetValueOrDefault(type) ?? throw new ServiceNotRegisteredException(type);
        }

        throw new ServiceNotRegisteredException(type);
    }

    public SvcDescriptor GetDescriptor(Type type) => GetDescriptors(type).Last();

    private SvcDescriptor? GetCompileTimeDescriptor(Type type)
    {
        // This method would be implemented by the Source Generator
        // to provide compile-time service discovery
        return null;
    }
}

/// <summary>
/// AOT-friendly service provider that uses compile-time generated factories
/// </summary>
public sealed class AotSvcProvider : ISvcProvider
{
    private readonly ConcurrentDictionary<Type, object> _singletonInstances = new();
    private readonly ISvcContainer _container;
    private readonly ISvcScopeFactory _scopeFactory;
    private volatile bool _disposed;

    public AotSvcProvider(ISvcContainer container)
    {
        _container = container;
        _scopeFactory = new SvcScopeFactory(new SvcResolverFactory());
    }

    public object Resolve(Type serviceType)
    {
        EnsureNotDisposed();

        // Try compile-time resolution first
        var compileTimeInstance = CompileTimeServiceFactories.ResolveService(serviceType, this);
        if (compileTimeInstance is not null)
            return compileTimeInstance;

        // Fallback to runtime resolution
        var descriptor = _container.GetDescriptor(serviceType);

        return descriptor.Lifetime switch
        {
            SvcLifetime.Transient => CreateServiceInstance(descriptor),
            SvcLifetime.Singleton => _singletonInstances.GetOrAdd(
                serviceType,
                _ => CreateServiceInstance(descriptor)
            ),
            SvcLifetime.Scoped => throw new InvalidOperationException(
                $"Cannot resolve {serviceType} as Scoped from root provider."
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(descriptor.Lifetime))
        };
    }

    public ISvcScope CreateScope()
    {
        EnsureNotDisposed();
        return _scopeFactory.CreateScope(_container, this);
    }

    public void Dispose() => DisposeCore(disposing: true);

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        DisposeCore(disposing: false);
    }

    #region Private Methods

    private void EnsureNotDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AotSvcProvider));
    }

    private void DisposeCore(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
            DisposeInstances(instance => (instance as IDisposable)?.Dispose());
        _disposed = true;
    }

    private async ValueTask DisposeAsyncCore()
    {
        if (_disposed)
            return;

        foreach (var instance in _singletonInstances.Values)
        {
            if (instance is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            (instance as IDisposable)?.Dispose();
        }
        _disposed = true;
    }

    private void DisposeInstances(Action<object> disposeAction)
    {
        foreach (var instance in _singletonInstances.Values)
            disposeAction(instance);
    }

    private object CreateServiceInstance(SvcDescriptor descriptor)
    {
        // Try compile-time factory first
        var compileTimeInstance = CompileTimeServiceFactories.CreateService(descriptor.ServiceType, this);
        if (compileTimeInstance is not null)
            return compileTimeInstance;

        // Fallback to runtime factory
        if (descriptor.Factory is null)
        {
            lock (descriptor)
            {
                descriptor.Factory ??= SvcFactory.CreateAotFactory(descriptor);
            }
        }

        var instance = descriptor.Factory!(this);

        // Cache singleton instances
        if (descriptor.Lifetime is SvcLifetime.Singleton && descriptor.SingleInstance is null)
            descriptor.SingleInstance = instance;

        return instance;
    }

    #endregion
}