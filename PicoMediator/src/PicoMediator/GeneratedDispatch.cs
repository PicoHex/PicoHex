namespace PicoMediator;

internal static class GeneratedDispatch
{
    /// <summary>
    /// Set by PicoMediator.Gen's [ModuleInitializer] to enable
    /// compile-time switch dispatch. When null, falls back to
    /// <see cref="ISvcScope.GetService"/>.
    /// </summary>
    internal static Func<Type, ISvcScope, object, CancellationToken, object?>? Switch;

    internal static ValueTask<TResponse> Send<TRequest, TResponse>(
        ISvcScope scope,
        TRequest request,
        CancellationToken ct
    )
        where TRequest : IRequest<TResponse>
    {
        var s = Switch;
        if (s is not null)
        {
            var result = s(typeof(TRequest), scope, request!, ct);
            if (result is not null)
                return (ValueTask<TResponse>)result;
        }

        var handler = scope.GetService<IRequestHandler<TRequest, TResponse>>();
        if (handler is null)
            throw new InvalidOperationException(
                $"No handler registered for {typeof(TRequest).FullName}."
            );

        return handler.Handle(request, ct);
    }
}
