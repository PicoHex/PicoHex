using PicoDI.Generated.Aop;

namespace PicoDI.Aop.Tests;

public class RuntimeIntegrationTests
{
    public interface ICalculator
    {
        int Add(int a, int b);
    }

    public sealed class Calculator : ICalculator
    {
        public int Add(int a, int b) => a + b;
    }

    public sealed class CallTrackingInterceptor : InterceptorBase
    {
        public int CallCount { get; private set; }

        public override TResult Invoke<TResult>(IInvocation<TResult> inv,
            Func<IInvocation<TResult>, TResult> next)
        {
            CallCount++;
            return next(inv);
        }
    }

    public sealed class DoublingInterceptor : InterceptorBase
    {
        public override TResult Invoke<TResult>(IInvocation<TResult> inv,
            Func<IInvocation<TResult>, TResult> next)
        {
            var original = next(inv);
            return (TResult)(object)((int)(object)original! * 2);
        }
    }

    // Trigger source generator to produce decorator types for these combos.
    // These calls use InterceptBy to signal the generator, but InterceptBy is a
    // runtime no-op — the actual decorator wiring is manual in test code.
    public static void RegisterInterceptedServices(SvcContainer container)
    {
        container.RegisterSingleton<CallTrackingInterceptor>(_ => new CallTrackingInterceptor());
        container.RegisterSingleton<DoublingInterceptor>(_ => new DoublingInterceptor());
        container.Register<ICalculator>(scope => new Calculator(), SvcLifetime.Scoped)
            .InterceptBy<CallTrackingInterceptor>();
        container.Register<ICalculator>(scope => new Calculator(), SvcLifetime.Scoped)
            .InterceptBy<DoublingInterceptor>();
    }

    [Test]
    public async Task Interceptor_WrapsService_InterceptorCalled()
    {
        var container = new SvcContainer(autoConfigureFromGenerator: true);
        RegisterInterceptedServices(container);
        container.Build();

        await using var scope = container.CreateScope();
        var inner = scope.GetService<ICalculator>()!;
        var interceptor = scope.GetService<CallTrackingInterceptor>();
        var decorator = new PicoDI_Aop_Tests_RuntimeIntegrationTests_ICalculator_CallTrackingInterceptorDecorator(inner, interceptor!);

        var result = decorator.Add(3, 4);

        await Assert.That(result).IsEqualTo(7);
        await Assert.That(interceptor!.CallCount).IsEqualTo(1);
    }

    [Test]
    public async Task Interceptor_ModifiesResult_ReturnsModifiedValue()
    {
        var container = new SvcContainer(autoConfigureFromGenerator: false);
        RegisterInterceptedServices(container);
        container.Build();

        await using var scope = container.CreateScope();
        var inner = scope.GetService<ICalculator>()!;
        var interceptor = scope.GetService<DoublingInterceptor>();
        var decorator = new PicoDI_Aop_Tests_RuntimeIntegrationTests_ICalculator_DoublingInterceptorDecorator(inner, interceptor!);

        var result = decorator.Add(3, 4);

        await Assert.That(result).IsEqualTo(14);
    }
}
