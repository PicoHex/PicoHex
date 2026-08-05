namespace PicoMediator.Abs;

// ── Message taxonomy (domain semantics) ──

/// <summary>Root marker: everything that flows through the mediator.</summary>
public interface IMessage { }

/// <summary>REQ — an intent that something should happen, expecting a response.
/// Queries are commands whose result is a read. Void commands use
/// PicoDI.Abs.VoidResult as the response type.</summary>
public interface ICommand<TResponse> : IMessage { }

/// <summary>PUB — a fact that has already happened. 1:N, no response.</summary>
public interface IEvent : IMessage { }

// ── Handler contracts (pattern vocabulary) ──

/// <summary>REP — handles one command type. 1:1 protocol.</summary>
public interface ICommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    ValueTask<TResponse> Handle(TCommand command, CancellationToken ct = default);
}

/// <summary>SUB — handles one event type. 1:N protocol.</summary>
public interface ISubscriber<in TEvent>
    where TEvent : IEvent
{
    ValueTask Handle(TEvent @event, CancellationToken ct = default);
}

// ── Caller ports ──

/// <summary>REQ socket — can Send, cannot Publish.</summary>
public interface IRequester
{
    ValueTask<TResponse> Send<TCommand, TResponse>(TCommand command, CancellationToken ct = default)
        where TCommand : ICommand<TResponse>;
}

/// <summary>PUB socket — can Publish, cannot Send.</summary>
public interface IPublisher
{
    ValueTask Publish<TEvent>(TEvent @event, CancellationToken ct = default)
        where TEvent : IEvent;

    ValueTask PublishParallel<TEvent>(TEvent @event, CancellationToken ct = default)
        where TEvent : IEvent;
}

/// <summary>Combined REQ + PUB socket. Orchestration-level code only.</summary>
public interface IMediator : IRequester, IPublisher { }
