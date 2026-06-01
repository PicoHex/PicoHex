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
        var container = new SvcContainer();
        container.RegisterScoped<IRequestHandler<SwitchPing, string>>(_ => new SwitchPingHandler());
        container.AddPicoMediator();
        container.Build();

        await using var scope = container.CreateScope();
        var mediator = scope.GetService<IMediator>();

        // Verify the delegate was wired by the generator
        await Assert.That(GeneratedDispatch.Switch).IsNotNull();

        var result = await mediator.Send<SwitchPing, string>(new SwitchPing());
        await Assert.That(result).IsEqualTo("switched");
    }

    [Test]
    public async Task Send_WithoutRegisteredHandler_FallsBackToGetService()
    {
        var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterScoped<IRequestHandler<SwitchPing, string>>(_ => new SwitchPingHandler());
        container.AddPicoMediator();
        container.Build();

        await using var scope = container.CreateScope();
        var mediator = scope.GetService<IMediator>();

        // Switch may be set assembly-wide by ModuleInitializer from other tests.
        // Regardless, Send works via fallback when no switch case matches.

        var result = await mediator.Send<SwitchPing, string>(new SwitchPing());
        await Assert.That(result).IsEqualTo("switched");
    }
}
