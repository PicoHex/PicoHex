namespace PicoMediator.Tests;

public class DiIntegrationTests
{
    public record Ping : IRequest<string>;

    public sealed class PingHandler : IRequestHandler<Ping, string>
    {
        public ValueTask<string> Handle(Ping r, CancellationToken ct) =>
            ValueTask.FromResult("pong");
    }

    [Test]
    public async Task AddPicoMediator_RegistersMediatorAsScoped()
    {
        var container = new SvcContainer();
        container.RegisterScoped<IRequestHandler<Ping, string>>(_ => new PingHandler());
        container.AddPicoMediator();
        container.Build();

        await using var scope = container.CreateScope();
        var mediator = scope.GetService<IMediator>();

        var result = await mediator.Send<Ping, string>(new Ping());

        await Assert.That(result).IsEqualTo("pong");
    }

    [Test]
    public async Task AddPicoMediator_NoHandler_Throws()
    {
        var container = new SvcContainer();
        container.AddPicoMediator();
        container.Build();

        await using var scope = container.CreateScope();
        var mediator = scope.GetService<IMediator>();

        await Assert.ThrowsAsync(async () => await mediator.Send<Ping, string>(new Ping()));
    }
}
