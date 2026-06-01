using PicoDI.Abs;
using PicoMediator.Abs;

namespace PicoMediator;

public sealed class Mediator(ISvcScope scope) : IMediator
{
    public ValueTask<TResponse> Send<TRequest, TResponse>(
        TRequest request,
        CancellationToken ct = default
    )
        where TRequest : IRequest<TResponse> =>
        GeneratedDispatch.Send<TRequest, TResponse>(scope, request, ct);

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
            return;
        }

        List<Exception>? exceptions = null;
        foreach (var h in handlers)
        {
            try
            {
                await h.Handle(notification, ct);
            }
            catch (Exception ex)
            {
                if (exceptions is null)
                    exceptions =  [];
                exceptions.Add(ex);
            }
        }

        if (exceptions is { Count: > 0 })
            throw new AggregateException(exceptions);
    }

    public async ValueTask PublishParallel<TNotification>(
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
            return;
        }

        var tasks = new Task[handlers.Count];
        for (var i = 0; i < handlers.Count; i++)
            tasks[i] = HandleSafelyAsync(handlers[i], notification, ct);
        await Task.WhenAll(tasks);
    }

    private static async Task HandleSafelyAsync<TNotification>(
        INotificationHandler<TNotification> handler,
        TNotification notification,
        CancellationToken ct
    )
        where TNotification : INotification
    {
        try
        {
            await handler.Handle(notification, ct);
        }
        catch { }
    }
}
