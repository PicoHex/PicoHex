namespace PicoHex.DI;

public sealed class SvcContainer(ISvcProviderFactory providerFactory) : ISvcContainer
{
    private readonly ConcurrentDictionary<Type, List<SvcDescriptor>> _descriptors = new();
    private volatile bool _disposed;

    public ISvcContainer Register(SvcDescriptor descriptor)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SvcContainer));
        ArgumentNullException.ThrowIfNull(descriptor);

        _descriptors.TryAdd(descriptor.ServiceType, []);
        _descriptors[descriptor.ServiceType].Add(descriptor);

        _descriptors.TryAdd(descriptor.ImplementationType, []);
        _descriptors[descriptor.ImplementationType].Add(descriptor);

        return this;
    }

    public ISvcProvider CreateProvider() => providerFactory.CreateProvider(this);

    public List<SvcDescriptor>? GetDescriptors(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type type
    ) => _descriptors.GetValueOrDefault(type);

    public SvcDescriptor? GetDescriptor(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type type
    ) => _descriptors.GetValueOrDefault(type)?.Last();

    public void Dispose() => Dispose(disposing: true);

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        Dispose(disposing: false);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
            return;
        if (disposing)
        {
            foreach (var descriptor in _descriptors.Values.SelectMany(p => p))
                if (descriptor.SingleInstance is IDisposable disposable)
                    disposable.Dispose();
        }
        _disposed = true;
    }

    private async ValueTask DisposeAsyncCore()
    {
        if (_disposed)
            return;
        foreach (var descriptor in _descriptors.Values.SelectMany(p => p))
        {
            if (descriptor.SingleInstance is IDisposable disposable)
                disposable.Dispose();
            if (descriptor.SingleInstance is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        _disposed = true;
    }
}
