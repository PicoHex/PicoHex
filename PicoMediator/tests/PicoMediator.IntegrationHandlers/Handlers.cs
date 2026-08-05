using System.Collections.Concurrent;

namespace PicoMediator.IntegrationHandlers;

// Handlers declared in a SEPARATE assembly from the app (the "library of
// handlers" pattern). This assembly's PicoMediator.Gen run emits its own
// switch cases and handler registrations; the app's AddPicoMediator() applies
// configurators from every loaded assembly.

public record LibPing(string Message) : ICommand<string>;

public sealed class LibPingHandler : ICommandHandler<LibPing, string>
{
    public ValueTask<string> Handle(LibPing c, CancellationToken ct) => new($"pong:{c.Message}");
}

public record LibEvent(int Id) : IEvent;

public static class LibEventLog
{
    public static readonly ConcurrentQueue<int> Received = new();
}

public sealed class LibSubscriber : ISubscriber<LibEvent>
{
    public ValueTask Handle(LibEvent e, CancellationToken ct)
    {
        LibEventLog.Received.Enqueue(e.Id);
        return ValueTask.CompletedTask;
    }
}
