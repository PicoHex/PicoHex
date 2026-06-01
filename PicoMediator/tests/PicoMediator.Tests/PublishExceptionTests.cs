namespace PicoMediator.Tests;

public class PublishExceptionTests
{
    public record Boom : INotification;

    public sealed class BoomHandler(string label, List<string> log) : INotificationHandler<Boom>
    {
        public ValueTask Handle(Boom n, CancellationToken ct)
        {
            log.Add($"{label}:Handle");
            throw new InvalidOperationException($"boom from {label}");
        }
    }

    [Test]
    public async Task Publish_MultipleHandlers_AllExecutedDespiteFailures()
    {
        var log = new List<string>();
        var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterSingle<INotificationHandler<Boom>>(new BoomHandler("A", log));
        container.RegisterSingle<INotificationHandler<Boom>>(new BoomHandler("B", log));
        container.RegisterSingle<INotificationHandler<Boom>>(new BoomHandler("C", log));
        container.Build();
        await using var scope = container.CreateScope();

        // Verify all 3 handlers are registered
        var handlers = scope.GetServices<INotificationHandler<Boom>>();
        await Assert.That(handlers.Count).IsEqualTo(3);

        var mediator = new Mediator(scope);
        await Assert.ThrowsAsync(async () => await mediator.Publish(new Boom()));

        await Assert.That(log).Contains("A:Handle");
        await Assert.That(log).Contains("B:Handle");
        await Assert.That(log).Contains("C:Handle");
    }

    [Test]
    public async Task Publish_MultipleHandlers_AggregatesExceptions()
    {
        var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterSingle<INotificationHandler<Boom>>(new BoomHandler("X", []));
        container.RegisterSingle<INotificationHandler<Boom>>(new BoomHandler("Y", []));
        container.Build();
        await using var scope = container.CreateScope();
        var mediator = new Mediator(scope);

        var ex = await Assert.ThrowsAsync(async () => await mediator.Publish(new Boom()));

        await Assert.That(ex).IsTypeOf<AggregateException>();
        await Assert.That(((AggregateException)ex!).InnerExceptions.Count).IsEqualTo(2);
    }
}
