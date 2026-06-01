namespace PicoMediator.Abs;

// ── Protocol markers ──

/// <summary>REQ — 1:1 request with response. The response type is encoded in the generic parameter.</summary>
public interface IRequest<TResponse> { }

/// <summary>PUB — 1:N notification. No response by protocol design.</summary>
public interface INotification { }

// ── Handlers ──

/// <summary>REP — handles a specific request type. One handler per request type (1:1 protocol).</summary>
public interface IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    ValueTask<TResponse> Handle(TRequest request, CancellationToken ct = default);
}

/// <summary>SUB — handles a specific notification type. Multiple handlers per notification (1:N protocol).</summary>
public interface INotificationHandler<TNotification>
    where TNotification : INotification
{
    ValueTask Handle(TNotification notification, CancellationToken ct = default);
}

// ── Caller ports ──

/// <summary>REQ socket — can Send, cannot Publish.</summary>
public interface ISender
{
    ValueTask<TResponse> Send<TRequest, TResponse>(TRequest request, CancellationToken ct = default)
        where TRequest : IRequest<TResponse>;
}

/// <summary>PUB socket — can Publish, cannot Send.</summary>
public interface IPublisher
{
    ValueTask Publish<TNotification>(TNotification notification, CancellationToken ct = default)
        where TNotification : INotification;

    ValueTask PublishParallel<TNotification>(
        TNotification notification,
        CancellationToken ct = default
    )
        where TNotification : INotification;
}

/// <summary>Combined REQ + PUB socket. Only for orchestration-level code.</summary>
public interface IMediator : ISender, IPublisher { }
