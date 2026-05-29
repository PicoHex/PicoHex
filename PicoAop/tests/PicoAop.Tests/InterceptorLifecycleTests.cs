namespace PicoAop.Tests;

public class LifecycleTests
{
    public interface IGreeter
    {
        string Greet(string name);
    }

    public sealed class Greeter : IGreeter
    {
        public string Greet(string name) => $"Hi {name}";
    }

    public sealed class CallCounter : InterceptorBase
    {
        public int Count { get; private set; }

        public override TResult Invoke<TResult>(
            IInvocation<TResult> inv,
            Func<IInvocation<TResult>, TResult> next
        )
        {
            Count++;
            return next(inv);
        }
    }

    // Trigger SG to generate decorator type
    public static void TriggerGen(SvcContainer c)
    {
        c.Register<IGreeter, Greeter>(SvcLifetime.Scoped).InterceptBy<CallCounter>();
    }

    [Test]
    public async Task Decorator_DoesNotImplement_IAsyncDisposable()
    {
        var type = typeof(PicoAop_Tests_LifecycleTests_IGreeter_CallCounterDecorator);
        var hasDispose = typeof(IAsyncDisposable).IsAssignableFrom(type);
        await Assert.That(hasDispose).IsFalse();
    }

    [Test]
    public async Task SingletonInterceptor_SameInstanceAcrossScopes()
    {
        var container = new SvcContainer(autoConfigureFromGenerator: true);
        container.RegisterSingleton<CallCounter>(_ => new CallCounter());
        container.Register<IGreeter>(scope => new Greeter(), SvcLifetime.Scoped);
        container.Build();

        await using var s1 = container.CreateScope();
        var i1 = s1.GetService<CallCounter>();
        await using var s2 = container.CreateScope();
        var i2 = s2.GetService<CallCounter>();

        await Assert.That(i1).IsSameReferenceAs(i2);
    }

    [Test]
    public async Task ScopedInterceptor_DifferentInstancePerScope()
    {
        var container = new SvcContainer(autoConfigureFromGenerator: true);
        container.RegisterScoped<CallCounter>(_ => new CallCounter());
        container.Register<IGreeter>(scope => new Greeter(), SvcLifetime.Scoped);
        container.Build();

        await using var s1 = container.CreateScope();
        var i1 = s1.GetService<CallCounter>();
        await using var s2 = container.CreateScope();
        var i2 = s2.GetService<CallCounter>();

        await Assert.That(i1).IsNotSameReferenceAs(i2);
    }
}
