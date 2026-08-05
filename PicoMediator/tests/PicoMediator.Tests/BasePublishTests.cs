namespace PicoMediator.Tests;

// Contract tests for base-typed publish (generated bridge subscribers).
// Event types are dedicated to this class so subscriber counts stay
// deterministic across test files.

public class BasePublishTests
{
    public interface IBpDomain : IEvent { }

    public record BpPaid(int Id) : IBpDomain;
    public record BpShipped(int Id) : IBpDomain;

    public sealed class BpPaidSubscriber : ISubscriber<BpPaid>
    {
        public bool Throw;
        public List<int> Received { get; } = [];

        public ValueTask Handle(BpPaid e, CancellationToken ct)
        {
            if (Throw)
                throw new InvalidOperationException($"boom:{e.Id}");
            Received.Add(e.Id);
            return ValueTask.CompletedTask;
        }
    }

    public sealed class BpShippedSubscriber : ISubscriber<BpShipped>
    {
        public List<int> Received { get; } = [];

        public ValueTask Handle(BpShipped e, CancellationToken ct)
        {
            Received.Add(e.Id);
            return ValueTask.CompletedTask;
        }
    }

    // Base-declared subscriber (audit-style): receives base-typed publishes directly.
    public sealed class BpAuditSubscriber : ISubscriber<IEvent>
    {
        public List<int> Received { get; } = [];

        public ValueTask Handle(IEvent e, CancellationToken ct)
        {
            Received.Add(1);
            return ValueTask.CompletedTask;
        }
    }

    // Keeps container + scope alive for the duration of the test: disposing the
    // scope inside CreateMediator would strand the mediator and any resolved
    // dispatchers on a disposed scope.
    private sealed class TestHarness(SvcContainer container) : IAsyncDisposable
    {
        public required Mediator Mediator { get; init; }
        public ValueTask DisposeAsync() => container.DisposeAsync();
    }

    private static async Task<TestHarness> CreateMediator(
        Action<SvcContainer>? register = null
    )
    {
        var container = new SvcContainer(autoConfigureFromGenerator: false);
        register?.Invoke(container);
        container.AddPicoMediator();
        container.Build();
        var scope = container.CreateScope();
        return new TestHarness(container) { Mediator = (Mediator)scope.GetService<IMediator>() };
    }

    [Test]
    public async Task Publish_BaseTypedList_ReachesConcreteSubscribers()
    {
        var paid = new BpPaidSubscriber();
        await using var h = await CreateMediator(c => c.RegisterSingle<ISubscriber<BpPaid>>(paid));
        var mediator = h.Mediator;

        IReadOnlyList<IEvent> events = [new BpPaid(1), new BpPaid(2)];
        foreach (var e in events)
            await mediator.Publish(e); // static type IEvent

        await Assert.That(paid.Received).IsEquivalentTo([1, 2]);
    }

    [Test]
    public async Task Publish_IntermediateBase_ReachesConcreteSubscribers()
    {
        var paid = new BpPaidSubscriber();
        await using var h = await CreateMediator(c => c.RegisterSingle<ISubscriber<BpPaid>>(paid));
        var mediator = h.Mediator;

        await mediator.Publish<IBpDomain>(new BpPaid(7));

        await Assert.That(paid.Received).IsEquivalentTo([7]);
    }

    [Test]
    public async Task Publish_BaseTyped_OneN_FanOut()
    {
        var a = new BpPaidSubscriber();
        var b = new BpPaidSubscriber();
        await using var h = await CreateMediator(c =>
        {
            c.RegisterSingle<ISubscriber<BpPaid>>(a);
            c.RegisterSingle<ISubscriber<BpPaid>>(b);
        });
        var mediator = h.Mediator;

        IReadOnlyList<IEvent> events = [new BpPaid(3)];
        foreach (var e in events)
            await mediator.Publish(e);

        await Assert.That(a.Received).IsEquivalentTo([3]);
        await Assert.That(b.Received).IsEquivalentTo([3]);
    }

    [Test]
    public async Task Publish_BaseTyped_NoDoubleDelivery()
    {
        var paid = new BpPaidSubscriber();
        var audit = new BpAuditSubscriber();
        await using var h = await CreateMediator(c =>
        {
            c.RegisterSingle<ISubscriber<BpPaid>>(paid);
            c.RegisterSingle<ISubscriber<IEvent>>(audit);
        });
        var mediator = h.Mediator;

        IReadOnlyList<IEvent> events = [new BpPaid(5)];
        foreach (var e in events)
            await mediator.Publish(e);

        await Assert.That(paid.Received).IsEquivalentTo([5]); // once via dispatcher
        await Assert.That(audit.Received).IsEquivalentTo([1]); // once directly
    }

    [Test]
    public async Task Publish_BaseTyped_SubscriberThrows_OthersStillRun_Aggregate()
    {
        var good = new BpPaidSubscriber();
        var bad = new BpPaidSubscriber { Throw = true };
        await using var h = await CreateMediator(c =>
        {
            c.RegisterSingle<ISubscriber<BpPaid>>(bad);
            c.RegisterSingle<ISubscriber<BpPaid>>(good);
        });
        var mediator = h.Mediator;

        IReadOnlyList<IEvent> events = [new BpPaid(9)];
        var ex = await Assert.ThrowsAsync(async () =>
        {
            foreach (var e in events)
                await mediator.Publish(e);
        });

        await Assert.That(ex).IsTypeOf<AggregateException>();
        await Assert.That(good.Received).IsEquivalentTo([9]);
    }

    [Test]
    public async Task Publish_BaseTyped_NoSubscribersForRuntimeType_Silent()
    {
        await using var h = await CreateMediator(); // nothing registered
        var mediator = h.Mediator;

        IReadOnlyList<IEvent> events = [new BpPaid(1)];
        foreach (var e in events)
            await mediator.Publish(e); // must not throw

        await Assert.That(true).IsTrue();
    }

    [Test]
    public async Task Publish_ExactType_Unchanged_Regression()
    {
        var paid = new BpPaidSubscriber();
        var audit = new BpAuditSubscriber();
        await using var h = await CreateMediator(c =>
        {
            c.RegisterSingle<ISubscriber<BpPaid>>(paid);
            c.RegisterSingle<ISubscriber<IEvent>>(audit);
        });
        var mediator = h.Mediator;

        await mediator.Publish(new BpPaid(11));

        await Assert.That(paid.Received).IsEquivalentTo([11]); // concrete subscriber
        await Assert.That(audit.Received).IsEmpty(); // base subscriber NOT called (variance boundary)
    }

    [Test]
    public async Task AddPicoMediator_NoAutoRegisterHandlers_NoBridges()
    {
        var paid = new BpPaidSubscriber();
        var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterSingle<ISubscriber<BpPaid>>(paid);
        container.AddPicoMediator(autoRegisterHandlers: false);
        container.Build();
        await using var scope = container.CreateScope();
        var mediator = (Mediator)scope.GetService<IMediator>();

        IReadOnlyList<IEvent> events = [new BpPaid(1)];
        foreach (var e in events)
            await mediator.Publish(e); // no bridge → silent drop (documented)

        await Assert.That(paid.Received).IsEmpty();
    }
}
