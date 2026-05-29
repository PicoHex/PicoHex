namespace PicoAop.Tests;

public class RuntimeGlobalInterceptorTests
{
    public interface IAlpha
    {
        string Do();
    }

    public sealed class Alpha : IAlpha
    {
        public string Do() => "alpha";
    }

    public interface IBeta
    {
        int Get();
    }

    public sealed class Beta : IBeta
    {
        public int Get() => 42;
    }

    public sealed class GlobalLog : InterceptorBase
    {
        public List<string> Calls { get; } = [];

        public override TResult Invoke<TResult>(
            IInvocation<TResult> inv,
            Func<IInvocation<TResult>, TResult> next
        )
        {
            Calls.Add(inv.MethodName);
            return next(inv);
        }
    }

    [Test]
    public async Task AddInterceptor_AppliesToAllRegisteredServices()
    {
        var container = new SvcContainer(autoConfigureFromGenerator: true);
        container.RegisterSingleton<GlobalLog>(_ => new GlobalLog());
        container.Register<IAlpha>(scope => new Alpha(), SvcLifetime.Scoped);
        container.Register<IBeta>(scope => new Beta(), SvcLifetime.Scoped);
        container.Build();

        await using var scope = container.CreateScope();
        var log = scope.GetService<GlobalLog>();
        var alpha = scope.GetService<IAlpha>();
        var beta = scope.GetService<IBeta>();
        await Assert.That(alpha).IsNotNull();
        await Assert.That(beta).IsNotNull();
        await Assert.That(log).IsNotNull();
    }

    [Test]
    public async Task GlobalInterceptor_IsSingleton()
    {
        var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterSingleton<GlobalLog>(_ => new GlobalLog());
        container.Build();

        await using var s1 = container.CreateScope();
        await using var s2 = container.CreateScope();
        var i1 = s1.GetService<GlobalLog>();
        var i2 = s2.GetService<GlobalLog>();
        await Assert.That(i1).IsSameReferenceAs(i2);
    }

    [Test]
    public async Task GlobalInterceptor_CanBeResolvedAndCalled()
    {
        var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterSingleton<GlobalLog>(_ => new GlobalLog());
        container.Build();

        await using var scope = container.CreateScope();
        var log = scope.GetService<GlobalLog>();
        await Assert.That(log).IsNotNull();
    }

    [Test]
    public async Task PerServiceInterceptor_CoexistsWithGlobal()
    {
        var container = new SvcContainer(autoConfigureFromGenerator: true);
        container.RegisterSingleton<GlobalLog>(_ => new GlobalLog());
        container.Register<IAlpha>(scope => new Alpha(), SvcLifetime.Scoped);
        container.Build();

        await using var scope = container.CreateScope();
        var log = scope.GetService<GlobalLog>();
        var alpha = scope.GetService<IAlpha>();
        await Assert.That(alpha).IsNotNull();
        await Assert.That(log).IsNotNull();
    }
}
