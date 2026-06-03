namespace PicoMediator;

internal static class GeneratedDispatch
{
    private static readonly Lock _switchesLock = new();
    private static List<Func<Type, ISvcScope, object, CancellationToken, object?>>? _switches;

    /// <summary>
    /// Registers a compile-time dispatch switch. Called by
    /// PicoMediator.Gen's [ModuleInitializer] from each assembly
    /// that contains handler implementations. Multiple assemblies
    /// can register switches — each is tried in registration order
    /// until one returns a non-null result.
    /// </summary>
    internal static void RegisterSwitch(
        Func<Type, ISvcScope, object, CancellationToken, object?> dispatch
    )
    {
        ArgumentNullException.ThrowIfNull(dispatch);
        lock (_switchesLock)
        {
            _switches ??= new List<Func<Type, ISvcScope, object, CancellationToken, object?>>();
            _switches.Add(dispatch);
        }
    }

    /// <summary>
    /// Exposed for testing: clears all registered switches.
    /// </summary>
    internal static void ClearSwitches()
    {
        lock (_switchesLock)
        {
            _switches?.Clear();
        }
    }

    internal static ValueTask<TResponse> Send<TRequest, TResponse>(
        ISvcScope scope,
        TRequest request,
        CancellationToken ct
    )
        where TRequest : IRequest<TResponse>
    {
        Func<Type, ISvcScope, object, CancellationToken, object?>[]? switches;
        lock (_switchesLock)
        {
            switches = _switches?.ToArray();
        }

        if (switches is not null)
        {
            foreach (var s in switches)
            {
                var result = s(typeof(TRequest), scope, request!, ct);
                if (result is ValueTask<TResponse> typedResult)
                    return typedResult;

                if (result is not null)
                {
                    throw new InvalidOperationException(
                        $"Generated dispatch type mismatch for request '{typeof(TRequest).FullName}': "
                            + $"expected ValueTask<{typeof(TResponse).FullName}>, "
                            + $"got '{result.GetType().FullName}'. "
                            + "This typically indicates a version mismatch between "
                            + "the PicoMediator.Gen generated code and the PicoMediator runtime."
                    );
                }
            }
        }

        var handler = scope.GetService<IRequestHandler<TRequest, TResponse>>();
        if (handler is null)
            throw new InvalidOperationException(
                $"No handler registered for {typeof(TRequest).FullName}."
            );

        return handler.Handle(request, ct);
    }
}
