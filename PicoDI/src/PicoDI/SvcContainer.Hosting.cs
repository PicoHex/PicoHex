namespace PicoDI;

public sealed partial class SvcContainer
{
    private List<SvcRuntimeRegistration>? _hostedRegistrations;

    // Long-lived scope for resolving hosted services. Created lazily on first
    // use and disposed with the container. This prevents scoped dependencies
    // captured by hosted singletons from being disposed when a temporary
    // resolution scope goes out of scope.
    private SvcScope? _hostingScope;

    // State machine: Created=0 → Started=1 → Stopped=2
    // Uses Interlocked for atomic one-shot transitions.
    // TryTransitionToStopped uses Exchange (not CAS) to guarantee the state
    // always moves to 2 — this prevents Start from winning a race against Stop.
    private int _hostedState;

    // Serializes StartHostedServicesAsync and StopHostedServicesAsync so that
    // concurrent Start/Stop calls cannot overlap on the same hosted service,
    // which would violate the IHostedSvc contract (StopAsync called while
    // StartAsync is still in flight).
    private readonly SemaphoreSlim _hostingLock = new(1, 1);

    private bool TryTransitionToStarted()
    {
        return Interlocked.CompareExchange(ref _hostedState, 1, 0) is 0;
    }

    private bool TryTransitionToStopped()
    {
        return Interlocked.Exchange(ref _hostedState, 2) == 1;
    }

    private static bool IsHostedSvcType(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type serviceType
    ) => typeof(IHostedSvc).IsAssignableFrom(serviceType);

    private void TrackHostedServicesFromRegistrations(
        Dictionary<Type, List<SvcRuntimeRegistration>> cache
    )
    {
        var lastByType = new Dictionary<Type, SvcRuntimeRegistration>();
        foreach (var (_, registrations) in cache)
        {
            foreach (var registration in registrations)
            {
                if (!IsHostedSvcType(registration.ServiceType))
                    continue;

                if (registration.Lifetime != SvcLifetime.Singleton)
                {
                    throw new HostedSvcRegistrationException(
                        $"Type '{registration.ServiceType.FullName}' implements IHostedSvc but was registered as {registration.Lifetime}. "
                            + "Hosted services must be registered as Singleton. Use container.RegisterHostedSvc<T>() or register with SvcLifetime.Singleton."
                    );
                }

                lastByType[registration.ServiceType] = registration;
            }
        }

        if (lastByType.Count > 0)
            Volatile.Write(
                ref _hostedRegistrations,
                new List<SvcRuntimeRegistration>(lastByType.Values)
            );
    }

    /// <summary>
    /// Starts all registered hosted services in registration order.
    /// Each service is a one-shot: once started, it cannot be restarted
    /// without creating a new container.
    /// </summary>
    internal async Task StartHostedServicesAsync(CancellationToken cancellationToken = default)
    {
        if (!TryTransitionToStarted())
            return;

        // Lock acquisition must not be cancelled — the user's token controls
        // individual service start/stop, not infrastructure serialization.
        await _hostingLock.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            // Re-check: Stop or Dispose may have raced in between
            // TryTransitionToStarted and the lock acquisition.
            if (Volatile.Read(ref _hostedState) != 1 || Volatile.Read(ref _disposed) != 0)
                return;

            await StartHostedServicesCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _hostingLock.Release();
        }
    }

    private async Task StartHostedServicesCoreAsync(CancellationToken cancellationToken)
    {
        var registrations = Volatile.Read(ref _hostedRegistrations);
        if (registrations is null or { Count: 0 })
            return;

        // Iterate in registration order (forward) — matches IHostedService contract.
        foreach (var registration in registrations)
        {
            var instance = registration.GetSingletonInstance();
            if (instance is null)
            {
                _hostingScope ??= (SvcScope)CreateScope();
                instance = _hostingScope.GetService(registration.ServiceType);
            }

            if (instance is IHostedLifecycleSvc lifecycle)
            {
                await TryRunPhaseAsync(
                        "Starting",
                        registration,
                        () => lifecycle.StartingAsync(cancellationToken)
                    )
                    .ConfigureAwait(false);
                await TryRunPhaseAsync(
                        "Start",
                        registration,
                        () => lifecycle.StartAsync(cancellationToken)
                    )
                    .ConfigureAwait(false);
                await TryRunPhaseAsync(
                        "Started",
                        registration,
                        () => lifecycle.StartedAsync(cancellationToken)
                    )
                    .ConfigureAwait(false);
            }
            else if (instance is IHostedSvc hosted)
            {
                await TryRunPhaseAsync(
                        "Start",
                        registration,
                        () => hosted.StartAsync(cancellationToken)
                    )
                    .ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Stops all registered hosted services in reverse registration order (LIFO).
    /// Each service is a one-shot: once stopped, it cannot be restarted
    /// without creating a new container.
    /// </summary>
    internal async Task StopHostedServicesAsync(CancellationToken cancellationToken = default)
    {
        if (!TryTransitionToStopped())
            return;

        await _hostingLock.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            await StopHostedServicesCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _hostingLock.Release();
        }
    }

    private async Task StopHostedServicesCoreAsync(CancellationToken cancellationToken)
    {
        var registrations = Volatile.Read(ref _hostedRegistrations);
        if (registrations is null or { Count: 0 })
            return;

        // Iterate in reverse order (LIFO) — matches IHostedService contract.
        for (int i = registrations.Count - 1; i >= 0; i--)
        {
            var registration = registrations[i];

            var instance = registration.GetSingletonInstance();
            if (instance is null)
                continue;

            if (instance is IHostedLifecycleSvc lifecycle)
            {
                await TryRunPhaseAsync(
                        "Stopping",
                        registration,
                        () => lifecycle.StoppingAsync(cancellationToken)
                    )
                    .ConfigureAwait(false);
                await TryRunPhaseAsync(
                        "Stop",
                        registration,
                        () => lifecycle.StopAsync(cancellationToken)
                    )
                    .ConfigureAwait(false);
                await TryRunPhaseAsync(
                        "Stopped",
                        registration,
                        () => lifecycle.StoppedAsync(cancellationToken)
                    )
                    .ConfigureAwait(false);
            }
            else if (instance is IHostedSvc hosted)
            {
                await TryRunPhaseAsync(
                        "Stop",
                        registration,
                        () => hosted.StopAsync(cancellationToken)
                    )
                    .ConfigureAwait(false);
            }
        }
    }

    private async Task TryRunPhaseAsync(
        string phase,
        SvcRuntimeRegistration registration,
        Func<Task> action
    )
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            OnError?.Invoke(
                ex,
                $"Error in {phase} phase of hosted service '{registration.ServiceType.FullName}'"
            );
        }
    }

    private async ValueTask StopHostedServicesAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await StopHostedServicesAsync(cts.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            OnError?.Invoke(
                ex,
                "Error stopping hosted services during asynchronous disposal"
            );
        }
    }
}
