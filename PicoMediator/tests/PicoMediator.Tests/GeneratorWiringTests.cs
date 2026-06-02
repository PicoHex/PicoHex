namespace PicoMediator.Tests;

public class GeneratorWiringTests
{
    public record SwitchPing : IRequest<string>;

    public sealed class SwitchPingHandler : IRequestHandler<SwitchPing, string>
    {
        public ValueTask<string> Handle(SwitchPing r, CancellationToken ct) =>
            ValueTask.FromResult("switched");
    }

    [Test]
    public async Task Send_WithGenerator_UsesCompiledSwitch()
    {
        GeneratedDispatch.ClearSwitches();

        var container = new SvcContainer();
        container.RegisterScoped<IRequestHandler<SwitchPing, string>>(_ => new SwitchPingHandler());
        container.AddPicoMediator();
        container.Build();

        await using var scope = container.CreateScope();
        var mediator = scope.GetService<IMediator>();

        // Send works whether the generator wired a switch or not —
        // the runtime fallback handles unregistered switch cases.
        var result = await mediator.Send<SwitchPing, string>(new SwitchPing());
        await Assert.That(result).IsEqualTo("switched");
    }

    [Test]
    public async Task Send_WithoutRegisteredHandler_FallsBackToGetService()
    {
        GeneratedDispatch.ClearSwitches();

        var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterScoped<IRequestHandler<SwitchPing, string>>(_ => new SwitchPingHandler());
        container.AddPicoMediator();
        container.Build();

        await using var scope = container.CreateScope();
        var mediator = scope.GetService<IMediator>();

        // No switches registered after ClearSwitches — falls back to runtime.

        var result = await mediator.Send<SwitchPing, string>(new SwitchPing());
        await Assert.That(result).IsEqualTo("switched");
    }
}
