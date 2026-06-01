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

        var count = handlers.Count;
        var tasks = new Task[count];
        var exceptions = new Exception?[count];

        for (var i = 0; i < count; i++)
        {
            var idx = i;
            tasks[i] = HandleSafelyAsync(handlers[idx], notification, ct, exceptions, idx);
        }

        await Task.WhenAll(tasks);

        var actual = new List<Exception>(count);
        foreach (var e in exceptions)
        {
            if (e is not null)
                actual.Add(e);
        }

        if (actual.Count > 0)
            throw new AggregateException(actual);
    }

    private static async Task HandleSafelyAsync<TNotification>(
        INotificationHandler<TNotification> handler,
        TNotification notification,
        CancellationToken ct,
        Exception?[] exceptions,
        int index
    )
        where TNotification : INotification
    {
        try
        {
            await handler.Handle(notification, ct);
        }
        catch (Exception ex)
        {
            exceptions[index] = ex;
        }
    }
}
