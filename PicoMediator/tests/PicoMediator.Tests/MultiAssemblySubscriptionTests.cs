namespace PicoMediator.Tests;

// Handlers declared in a SEPARATE assembly (PicoMediator.IntegrationHandlers).
// Its generator run emits its own registrations; AddPicoMediator() applies
// configurators from every loaded assembly — the "library of handlers"
// pattern PicoActor will rely on.

public class MultiAssemblySubscriptionTests
{
    [Test]
    public async Task AddPicoMediator_RegistersHandlersFromReferencedAssembly()
    {
        var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.AddPicoMediator();
        container.Build();
        await using var scope = container.CreateScope();
        var mediator = scope.GetService<IMediator>();

        // Command handler from the other assembly (generated switch + registration).
        var result = await mediator.Send<LibPing, string>(new LibPing("x"));
        await Assert.That(result).IsEqualTo("pong:x");

        // Subscriber from the other assembly (auto-registered, dedup-free).
        await mediator.Publish(new LibEvent(7));
        await Assert.That(LibEventLog.Received).Contains(7);
    }

    [Test]
    public async Task AddPicoMediator_DedupAppliesAcrossAssemblies()
    {
        var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterSingle<ISubscriber<LibEvent>>(new ManualLibSubscriber());
        container.AddPicoMediator();
        container.Build();
        await using var scope = container.CreateScope();

        // Manual registration for ISubscriber<LibEvent> wins: the library's
        // auto-registration is skipped for that service type.
        var subscribers = scope.GetServices<ISubscriber<LibEvent>>();
        await Assert.That(subscribers.Count).IsEqualTo(1);
        await Assert.That(subscribers[0]).IsTypeOf<ManualLibSubscriber>();
    }

    private sealed class ManualLibSubscriber : ISubscriber<LibEvent>
    {
        public ValueTask Handle(LibEvent e, CancellationToken ct) => ValueTask.CompletedTask;
    }
}
