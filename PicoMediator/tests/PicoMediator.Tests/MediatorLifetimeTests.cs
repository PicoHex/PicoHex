namespace PicoMediator.Tests;

public class MediatorLifetimeTests
{
    public record Ping : IRequest<string>;

    public sealed class PingHandler : IRequestHandler<Ping, string>
    {
        public ValueTask<string> Handle(Ping r, CancellationToken ct) =>
            ValueTask.FromResult("pong");
    }

    [Test]
    public async Task AddPicoMediator_Default_IsScoped()
    {
        // Default behavior should remain Scoped for backward compatibility
        var container = new SvcContainer();
        container.RegisterScoped<IRequestHandler<Ping, string>>(_ => new PingHandler());
        container.AddPicoMediator();
        container.Build();

        await using var scope = container.CreateScope();
        var mediator = scope.GetService<IMediator>();
        await Assert.That(mediator).IsNotNull();
    }

    [Test]
    public async Task AddPicoMediator_Singleton_Works()
    {
        // 🔴 RED: Currently AddPicoMediator() doesn't accept a lifetime parameter
        // and always registers as Scoped. After the fix, this should work.
        var container = new SvcContainer();
        container.RegisterScoped<IRequestHandler<Ping, string>>(_ => new PingHandler());
        container.AddPicoMediator(SvcLifetime.Singleton);
        container.Build();

        // Resolve the mediator once (creates the singleton)
        await using var scope1 = container.CreateScope();
        var mediator1 = scope1.GetService<IMediator>();

        // Resolve again in a different scope — should be the same instance
        await using var scope2 = container.CreateScope();
        var mediator2 = scope2.GetService<IMediator>();

        await Assert.That(ReferenceEquals(mediator1, mediator2)).IsTrue();
    }

    [Test]
    public async Task AddPicoMediator_ScopedExplicit_BehavesAsScoped()
    {
        var container = new SvcContainer();
        container.RegisterScoped<IRequestHandler<Ping, string>>(_ => new PingHandler());
        container.AddPicoMediator(SvcLifetime.Scoped);
        container.Build();

        await using var scope1 = container.CreateScope();
        var mediator1 = scope1.GetService<IMediator>();

        await using var scope2 = container.CreateScope();
        var mediator2 = scope2.GetService<IMediator>();

        await Assert.That(ReferenceEquals(mediator1, mediator2)).IsFalse();
    }
}
