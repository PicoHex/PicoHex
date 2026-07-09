namespace PicoMediator;

public sealed class Mediator(ISvcScope scope) : IMediator
{
    /// <summary>
    /// Optional callback invoked when a notification has no registered handlers.
    /// Receives the notification type name. Silent drop by default (PUB/SUB semantics).
    /// </summary>
    public Action<string>? OnNoSubscribers { get; set; }

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
        if (!scope.TryGetServices(typeof(INotificationHandler<TNotification>), out var rawServices))
        {
            OnNoSubscribers?.Invoke(typeof(TNotification).FullName!);
            return;
        }

        List<Exception>? exceptions = null;
        foreach (var raw in rawServices)
        {
            var h = (INotificationHandler<TNotification>)raw;
            try
            {
                await h.Handle(notification, ct);
            }
            catch (Exception ex)
            {
                if (exceptions is null)
                    exceptions = [];
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
        if (!scope.TryGetServices(typeof(INotificationHandler<TNotification>), out var rawServices))
        {
            OnNoSubscribers?.Invoke(typeof(TNotification).FullName!);
            return;
        }

        var count = rawServices.Count;
        var tasks = new Task[count];
        var exceptions = new Exception?[count];

        for (var i = 0; i < count; i++)
        {
            var idx = i;
            var handler = (INotificationHandler<TNotification>)rawServices[idx];
            tasks[i] = HandleSafelyAsync(handler, notification, ct, exceptions, idx);
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
