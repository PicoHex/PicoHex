namespace PicoDI;

internal sealed class SvcRuntimeRegistration
{
    internal readonly Type ServiceType;
    internal readonly Func<ISvcScope, object>? Factory;
    internal readonly int GeneratedFactoryId;
    internal readonly SvcLifetime Lifetime;
    internal readonly SvcRuntimeSingletonState? SingletonState;

    private SvcRuntimeRegistration(
        Type serviceType,
        Func<ISvcScope, object>? factory,
        int generatedFactoryId,
        SvcLifetime lifetime,
        SvcRuntimeSingletonState? singletonState
    )
    {
        ServiceType = serviceType;
        Factory = factory;
        GeneratedFactoryId = generatedFactoryId;
        Lifetime = lifetime;
        SingletonState = singletonState;
    }

    internal static SvcRuntimeRegistration Create(SvcDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (descriptor.Factory is not null && descriptor.SingleInstance is not null)
        {
            throw new ArgumentException(
                $"Service '{descriptor.ServiceType}' has both a Factory and a SingleInstance set. "
                    + "Use Factory for deferred construction or SingleInstance for a pre-built instance, not both.",
                nameof(descriptor)
            );
        }

        var singletonState =
            descriptor.Lifetime == SvcLifetime.Singleton
                ? new SvcRuntimeSingletonState(
                    descriptor.SingleInstance,
                    // The container owns (and thus disposes) singletons it creates
                    // itself via the factory. User-supplied instances passed through
                    // RegisterSingle / FromInstance are owned by the caller, matching
                    // Microsoft.Extensions.DependencyInjection semantics (Bug 9).
                    ownsInstance: descriptor.SingleInstance is null
                )
                : null;

        return new SvcRuntimeRegistration(
            descriptor.ServiceType,
            descriptor.Factory,
            descriptor.GeneratedFactoryId,
            descriptor.Lifetime,
            singletonState
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal object? GetSingletonInstance()
    {
        var singletonState = SingletonState;
        return singletonState is null ? null : Volatile.Read(ref singletonState.Instance);
    }

    /// <summary>
    /// Sets the sealed flag on the singleton state, preventing any further
    /// singleton creation through <see cref="GetOrCreateSingletonSlow"/>.
    /// Called by the container disposal path before iterating registrations.
    /// </summary>
    internal void SealSingletonState()
    {
        if (SingletonState is { } state)
        {
            lock (state.SyncLock)
                state.Sealed = true;
        }
    }

    /// <summary>
    /// Atomically takes (reads and clears) the singleton instance under
    /// <see cref="SvcRuntimeSingletonState.SyncLock"/>. This establishes
    /// happens-before with any in-flight <see cref="GetOrCreateSingletonSlow"/>
    /// that wrote the instance under the same lock, closing the race window
    /// where the disposal pass disposes an instance that has already been
    /// returned to a caller.
    /// </summary>
    internal object? TakeSingletonInstanceUnderLock()
    {
        if (SingletonState is { } state)
        {
            lock (state.SyncLock)
            {
                var instance = state.Instance;
                state.Instance = null;
                return instance;
            }
        }
        return null;
    }
}

internal sealed class SvcRuntimeSingletonState(object? instance, bool ownsInstance = true)
{
    internal readonly Lock SyncLock = new();
    internal object? Instance = instance;

    /// <summary>
    /// True when the container created the instance (and is therefore responsible
    /// for disposing it). False when the user supplied the instance through
    /// <c>RegisterSingle</c> / <c>SvcDescriptor.FromInstance</c>; in that case the
    /// container will not dispose it, matching Microsoft.Extensions.DependencyInjection.
    /// </summary>
    internal bool OwnsInstance = ownsInstance;

    /// <summary>
    /// When true, the container is being disposed and no new singleton
    /// instances should be created. <see cref="GetOrCreateSingletonSlow"/>
    /// checks this flag inside the lock before writing the candidate.
    /// </summary>
    internal bool Sealed;

    /// <summary>
    /// Monotonically increasing tag assigned when the singleton is constructed
    /// by the container's factory. Used by the disposal pass to release
    /// container-owned singletons in reverse construction order (LIFO), so a
    /// singleton can safely use its dependencies in its own Dispose method.
    /// Zero means "not yet constructed by the container" (e.g. user-supplied
    /// instances or registrations whose factory has not run).
    /// </summary>
    internal long CreationOrder;
}
