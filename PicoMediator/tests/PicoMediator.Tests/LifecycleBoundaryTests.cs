namespace PicoMediator.Tests;

public class LifecycleBoundaryTests
{
    public record LifePing : IRequest<string>;

    public sealed class LifePingHandler : IRequestHandler<LifePing, string>
    {
        public ValueTask<string> Handle(LifePing r, CancellationToken ct) =>
            ValueTask.FromResult("ok");
    }

    public record DisposedNotification : INotification;

    [Test]
    public async Task Send_AfterScopeDisposed_Throws()
    {
        var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterTransient<IRequestHandler<LifePing, string>>(_ => new LifePingHandler());
        container.AddPicoMediator();
        container.Build();

        var scope = container.CreateScope();
        var mediator = scope.GetService<IMediator>();
        await scope.DisposeAsync();

        await Assert.ThrowsAsync(async () => await mediator.Send<LifePing, string>(new LifePing()));
    }

    [Test]
    public async Task Publish_AfterScopeDisposed_Throws()
    {
        var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.AddPicoMediator();
        container.Build();

        var scope = container.CreateScope();
        var mediator = scope.GetService<IMediator>();
        await scope.DisposeAsync();

        await Assert.ThrowsAsync(async () => await mediator.Publish(new DisposedNotification()));
    }
}
