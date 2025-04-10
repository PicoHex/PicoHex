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

        if (IsConflictForServiceType(descriptor, out var conflictDetails))
        {
            throw new InvalidOperationException(
                $"Duplicate registration for type '{descriptor.ServiceType.FullName}'. {conflictDetails}"
            );
        }

        _descriptors.TryAdd(descriptor.ServiceType, []);
        _descriptors[descriptor.ServiceType].Add(descriptor);

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

    private bool IsConflictForServiceType(SvcDescriptor newDescriptor, out string conflictDetails)
    {
        conflictDetails = null!;

        if (!_descriptors.TryGetValue(newDescriptor.ServiceType, out var existingDescriptors))
            return false;

        foreach (var existing in existingDescriptors)
        {
            if (existing.ImplementationType == newDescriptor.ImplementationType)
            {
                conflictDetails =
                    $"ImplementationType '{existing.ImplementationType.FullName}' is already registered.";
                return true;
            }

            if (
                existing.SingleInstance == null
                || newDescriptor.SingleInstance == null
                || existing.SingleInstance != newDescriptor.SingleInstance
            )
                continue;
            conflictDetails =
                $"SingleInstance of type '{existing.SingleInstance.GetType().FullName}' is already registered.";
            return true;
        }

        return false;
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
