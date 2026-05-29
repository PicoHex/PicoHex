namespace PicoAop.Tests;

public class RuntimeSelfRegisterTests
{
    public class Worker
    {
        public virtual string Do() => "work";
    }

    public sealed class LogInterceptor : InterceptorBase
    {
        public bool WasCalled { get; private set; }

        public override TResult Invoke<TResult>(
            IInvocation<TResult> inv,
            Func<IInvocation<TResult>, TResult> next
        )
        {
            WasCalled = true;
            return next(inv);
        }
    }

    // Trigger the source generator to emit decorator types.
    public static void RegisterTypes(SvcContainer c)
    {
        c.Register<Worker>(SvcLifetime.Scoped).InterceptBy<LogInterceptor>();
    }

    [Test]
    public async Task SelfRegister_DecoratorIsConstructable()
    {
        var container = new SvcContainer(autoConfigureFromGenerator: true);
        container.RegisterSingleton<LogInterceptor>(_ => new LogInterceptor());
        container.Build();

        await using var scope = container.CreateScope();
        var inner = new Worker();
        var interceptor = scope.GetService<LogInterceptor>();

        var decoratorType =
            typeof(PicoAop_Tests_RuntimeSelfRegisterTests_Worker_LogInterceptorDecorator);
        var ctor = decoratorType.GetConstructors().First();
        var decorator = (Worker)ctor.Invoke([inner, interceptor!]);

        var result = decorator!.Do();
        await Assert.That(result).IsEqualTo("work");
    }

    [Test]
    public async Task SelfRegister_InterceptorResolvedFromDI()
    {
        var container = new SvcContainer(autoConfigureFromGenerator: true);
        container.RegisterSingleton<LogInterceptor>(_ => new LogInterceptor());
        container.Build();

        await using var scope = container.CreateScope();
        var interceptor = scope.GetService<LogInterceptor>();
        await Assert.That(interceptor).IsNotNull();
    }

    [Test]
    public async Task SelfRegister_InterceptorIsResolvable()
    {
        var container = new SvcContainer(autoConfigureFromGenerator: true);
        container.RegisterSingleton<LogInterceptor>(_ => new LogInterceptor());
        container.Build();

        await using var scope = container.CreateScope();
        var interceptor = scope.GetService<LogInterceptor>();
        await Assert.That(interceptor).IsNotNull();
    }
}
