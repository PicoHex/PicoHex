namespace PicoHex.DependencyInjection.NG;

public enum Lifetime
{
    Transient,
    Singleton,
    Scoped,
    PerThread
}

public interface IContainer : IDisposable
{
    IContainer CreateScope();
    object GetService(Type serviceType);
    T GetService<T>();
}

public sealed class Container : IContainer
{
    private readonly Registry _registry;
    private readonly ConcurrentDictionary<Type, Func<Container, object>> _factories;
    private readonly ConcurrentDictionary<Type, object> _singletons = new();
    private readonly ThreadLocal<Dictionary<Type, object>> _perThreadInstances = new(() => new());
    private Dictionary<Type, object>? _scopedInstances;
    private static readonly AsyncLocal<Stack<Type>> _resolvingStack = new();

    public Container(Registry registry)
    {
        _registry = registry;
        _factories = BuildFactories(registry);
        RegisterSelf();
    }

    private void RegisterSelf()
    {
        _singletons.TryAdd(typeof(IContainer), this);
        _singletons.TryAdd(typeof(Container), this);
    }

    public IContainer CreateScope()
    {
        return new Container(_registry) { _scopedInstances = new Dictionary<Type, object>() };
    }

    public object GetService(Type serviceType)
    {
        var resolvingStack = _resolvingStack.Value ??= new Stack<Type>();

        if (resolvingStack.Contains(serviceType))
        {
            throw new InvalidOperationException(
                $"Circular dependency detected: {string.Join(" -> ", resolvingStack)} -> {serviceType}"
            );
        }

        resolvingStack.Push(serviceType);
        try
        {
            if (!_registry.Mappings.TryGetValue(serviceType, out var descriptor))
            {
                if (
                    serviceType.IsGenericType
                    && _registry
                        .Mappings
                        .TryGetValue(serviceType.GetGenericTypeDefinition(), out descriptor)
                )
                {
                    descriptor = descriptor.MakeGenericType(serviceType.GenericTypeArguments);
                }
                else
                {
                    throw new InvalidOperationException($"Service {serviceType} not registered");
                }
            }

            return Resolve(descriptor);
        }
        finally
        {
            resolvingStack.Pop();
            if (resolvingStack.Count == 0)
            {
                _resolvingStack.Value = null;
            }
        }
    }

    public T GetService<T>() => (T)GetService(typeof(T));

    private object Resolve(ServiceDescriptor descriptor)
    {
        return descriptor.Lifetime switch
        {
            Lifetime.Singleton => GetSingleton(descriptor),
            Lifetime.Scoped => GetScoped(descriptor),
            Lifetime.PerThread => GetPerThread(descriptor),
            _ => CreateInstance(descriptor)
        };
    }

    private object GetSingleton(ServiceDescriptor descriptor)
    {
        return _singletons.GetOrAdd(descriptor.ServiceType, _ => CreateInstance(descriptor));
    }

    private object GetScoped(ServiceDescriptor descriptor)
    {
        if (_scopedInstances == null)
            throw new InvalidOperationException("No active scope");

        lock (_scopedInstances)
        {
            if (_scopedInstances.TryGetValue(descriptor.ServiceType, out var instance))
                return instance;
            instance = CreateInstance(descriptor);
            _scopedInstances[descriptor.ServiceType] = instance;
            return instance;
        }
    }

    private object GetPerThread(ServiceDescriptor descriptor)
    {
        var instances = _perThreadInstances.Value!;
        lock (instances)
        {
            if (instances.TryGetValue(descriptor.ServiceType, out var instance))
                return instance;
            instance = CreateInstance(descriptor);
            instances[descriptor.ServiceType] = instance;
            return instance;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private object CreateInstance(ServiceDescriptor descriptor)
    {
        return _factories
            .GetOrAdd(descriptor.ServiceType, _ => BuildFactory(descriptor.ImplementationType))
            .Invoke(this);
    }

    private static ConcurrentDictionary<Type, Func<Container, object>> BuildFactories(
        Registry registry
    )
    {
        var factories = new ConcurrentDictionary<Type, Func<Container, object>>();
        foreach (var descriptor in registry.Mappings.Values)
        {
            factories.TryAdd(descriptor.ServiceType, BuildFactory(descriptor.ImplementationType));
        }
        return factories;
    }

    private static Func<Container, object> BuildFactory(Type implementationType)
    {
        var ctor = implementationType
            .GetConstructors()
            .OrderByDescending(c => c.GetParameters().Length)
            .First();

        var containerParam = Expression.Parameter(typeof(Container));
        var parameters = ctor.GetParameters()
            .Select(p =>
            {
                var paramType = p.ParameterType;
                if (paramType is { IsGenericType: true, ContainsGenericParameters: true })
                {
                    paramType = paramType.GetGenericTypeDefinition();
                }

                var resolveMethod = typeof(Container).GetMethod(nameof(GetService))!;
                return Expression.Convert(
                    Expression.Call(containerParam, resolveMethod, Expression.Constant(paramType)),
                    p.ParameterType
                );
            });

        var newExpr = Expression.New(ctor, parameters);
        var lambda = Expression.Lambda<Func<Container, object>>(newExpr, containerParam);
        return lambda.Compile();
    }

    public void Dispose()
    {
        foreach (var disposable in _singletons.Values.OfType<IDisposable>())
        {
            disposable.Dispose();
        }
        _singletons.Clear();
        _perThreadInstances.Dispose();
    }
}

public class ServiceDescriptor(Type serviceType, Type implementationType, Lifetime lifetime)
{
    public Type ServiceType { get; } = serviceType;
    public Type ImplementationType { get; } = implementationType;
    public Lifetime Lifetime { get; } = lifetime;

    public ServiceDescriptor MakeGenericType(params Type[] typeArguments)
    {
        var genericService = ServiceType.MakeGenericType(typeArguments);
        var genericImplementation = ImplementationType.MakeGenericType(typeArguments);
        return new ServiceDescriptor(genericService, genericImplementation, Lifetime);
    }
}

public class Registry
{
    internal readonly Dictionary<Type, ServiceDescriptor> Mappings = new();

    public Registry Register<TService, TImplementation>(Lifetime lifetime = Lifetime.Transient)
    {
        return Register(typeof(TService), typeof(TImplementation), lifetime);
    }

    public Registry Register(Type serviceType, Type implementationType, Lifetime lifetime)
    {
        if (serviceType.IsGenericTypeDefinition && !implementationType.IsGenericTypeDefinition)
            throw new InvalidOperationException(
                "Open generic service requires open generic implementation"
            );

        Mappings[serviceType] = new ServiceDescriptor(serviceType, implementationType, lifetime);
        return this;
    }

    public Registry RegisterInstance<TService>(TService instance)
    {
        Mappings[typeof(TService)] = new ServiceDescriptor(
            typeof(TService),
            instance.GetType(),
            Lifetime.Singleton
        );
        return this;
    }

    public IContainer Build() => new Container(this);
}
