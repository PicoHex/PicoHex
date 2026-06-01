namespace PicoMediator.Tests;

public class PublishParallelExceptionTests
{
    public record ParaBoom : INotification;

    public sealed class ParaBoomHandler(string label, List<string> log)
        : INotificationHandler<ParaBoom>
    {
        public ValueTask Handle(ParaBoom n, CancellationToken ct)
        {
            log.Add($"{label}:Handle");
            throw new InvalidOperationException($"boom from {label}");
        }
    }

    [Test]
    public async Task PublishParallel_AllHandlersExecute_DespiteFailures()
    {
        var log = new List<string>();
        var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterSingle<INotificationHandler<ParaBoom>>(new ParaBoomHandler("A", log));
        container.RegisterSingle<INotificationHandler<ParaBoom>>(new ParaBoomHandler("B", log));
        container.RegisterSingle<INotificationHandler<ParaBoom>>(new ParaBoomHandler("C", log));
        container.Build();
        await using var scope = container.CreateScope();
        var mediator = new Mediator(scope);

        // All handlers should execute
        await Assert.ThrowsAsync(async () => await mediator.PublishParallel(new ParaBoom()));

        await Assert.That(log).Contains("A:Handle");
        await Assert.That(log).Contains("B:Handle");
        await Assert.That(log).Contains("C:Handle");
    }

    [Test]
    public async Task PublishParallel_AggregatesExceptions()
    {
        var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterSingle<INotificationHandler<ParaBoom>>(new ParaBoomHandler("X", []));
        container.RegisterSingle<INotificationHandler<ParaBoom>>(new ParaBoomHandler("Y", []));
        container.Build();
        await using var scope = container.CreateScope();
        var mediator = new Mediator(scope);

        var ex = await Assert.ThrowsAsync(
            async () => await mediator.PublishParallel(new ParaBoom())
        );

        await Assert.That(ex).IsTypeOf<AggregateException>();
        await Assert.That(((AggregateException)ex!).InnerExceptions.Count).IsEqualTo(2);
    }
}
