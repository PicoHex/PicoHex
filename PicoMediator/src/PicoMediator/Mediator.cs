using PicoDI.Abs;
using PicoMediator.Abs;

namespace PicoMediator;

/// <summary>
/// Compile-time request/notification dispatcher.
/// Uses <see cref="GeneratedDispatch.Send"/> for request routing
/// and <see cref="ISvcScope.GetServices{T}"/> for notification fan-out.
/// </summary>
public sealed class Mediator(ISvcScope scope) : IMediator
{
    /// <inheritdoc />
    public ValueTask<TResponse> Send<TRequest, TResponse>(
        TRequest request,
        CancellationToken ct = default
    )
        where TRequest : IRequest<TResponse> =>
        GeneratedDispatch.Send<TRequest, TResponse>(scope, request, ct);

    /// <inheritdoc />
    public async ValueTask Publish<TNotification>(
        TNotification notification,
        CancellationToken ct = default
    )
        where TNotification : INotification
    {
        IReadOnlyList<INotificationHandler<TNotification>> handlers;

        try
        {
            handlers = scope.GetServices<INotificationHandler<TNotification>>();
        }
        catch (PicoDiException)
        {
            // PUB/SUB: no subscribers means silent drop (ZeroMQ PUB semantics)
            return;
        }

        foreach (var h in handlers)
            await h.Handle(notification, ct);
    }
}

/// <summary>
/// Placeholder — the source generator replaces this with
/// a concrete switch-based dispatch implementation.
/// </summary>
internal static class GeneratedDispatch
{
    internal static ValueTask<TResponse> Send<TRequest, TResponse>(
        ISvcScope scope,
        TRequest request,
        CancellationToken ct
    )
        where TRequest : IRequest<TResponse>
    {
        var handler = scope.GetService<IRequestHandler<TRequest, TResponse>>();
        if (handler is null)
            throw new InvalidOperationException(
                $"No handler registered for {typeof(TRequest).FullName}."
            );

        return handler.Handle(request, ct);
    }
}
