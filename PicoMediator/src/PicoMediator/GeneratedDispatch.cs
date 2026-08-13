namespace PicoMediator;

public static class GeneratedDispatch
{
    private static readonly Lock _switchesLock = new();
    private static List<Func<Type, ISvcScope, object, CancellationToken, object?>>? _switches;

    /// <summary>
    /// Cached snapshot of the switches array, updated after each write to _switches.
    /// Allows lock-free reads on the hot Send path.
    /// </summary>
    private static volatile Func<
        Type,
        ISvcScope,
        object,
        CancellationToken,
        object?
    >[]? _switchesSnapshot;

    /// <summary>
    /// Registers a compile-time dispatch switch. Called by
    /// PicoMediator.Gen's [ModuleInitializer] from each assembly that contains
    /// handler implementations. Multiple assemblies can register switches —
    /// each is tried in registration order until one returns a non-null result.
    /// Not intended for direct use — generated code only.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void RegisterSwitch(
        Func<Type, ISvcScope, object, CancellationToken, object?> dispatch
    )
    {
        ArgumentNullException.ThrowIfNull(dispatch);
        lock (_switchesLock)
        {
            _switches ??= [];
            _switches.Add(dispatch);
            _switchesSnapshot = [.. _switches];
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
            _switchesSnapshot = null;
        }
    }

    internal static ValueTask<TResponse> Send<TCommand, TResponse>(
        ISvcScope scope,
        TCommand command,
        CancellationToken ct
    )
        where TCommand : ICommand<TResponse>
    {
        // Lock-free fast path: use the cached snapshot
        var switches = _switchesSnapshot;

        if (switches is not null)
        {
            foreach (var s in switches)
            {
                var result = s(typeof(TCommand), scope, command, ct);
                if (result is ValueTask<TResponse> typedResult)
                    return typedResult;

                if (result is not null)
                {
                    throw new InvalidOperationException(
                        $"Generated dispatch type mismatch for request '{typeof(TCommand).FullName}': "
                            + $"expected ValueTask<{typeof(TResponse).FullName}>, "
                            + $"got '{result.GetType().FullName}'. "
                            + "This typically indicates a version mismatch between "
                            + "the PicoMediator.Gen generated code and the PicoMediator runtime."
                    );
                }
            }
        }

        // No generated switch handled this request — fall back to direct
        // container resolution. Use TryGetService so the missing-handler
        // failure carries a descriptive message instead of a raw DI
        // resolution exception.
        if (!scope.TryGetService(typeof(ICommandHandler<TCommand, TResponse>), out var handler))
            throw new InvalidOperationException(
                $"No handler registered for {typeof(TCommand).FullName}. "
                    + "Register an ICommandHandler for the command or ensure PicoMediator.Gen "
                    + "can discover its handler implementation."
            );

        return ((ICommandHandler<TCommand, TResponse>)handler).Handle(command, ct);
    }
}
