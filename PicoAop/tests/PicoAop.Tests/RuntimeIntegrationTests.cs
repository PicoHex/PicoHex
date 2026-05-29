namespace PicoAop.Tests;

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

        public override TResult Invoke<TResult>(
            IInvocation<TResult> inv,
            Func<IInvocation<TResult>, TResult> next
        )
        {
            CallCount++;
            return next(inv);
        }
    }

    public static void RegisterIntercepted(SvcContainer container)
    {
        container.RegisterSingleton<CallTrackingInterceptor>(_ => new CallTrackingInterceptor());
        container
            .Register<ICalculator>(scope => new Calculator(), SvcLifetime.Scoped)
            .InterceptBy<CallTrackingInterceptor>();
    }

    [Test]
    public async Task Interceptor_WrapsService_InterceptorCalled()
    {
        var container = new SvcContainer(autoConfigureFromGenerator: true);
        RegisterIntercepted(container);
        container.Build();

        await using var scope = container.CreateScope();
        var inner = scope.GetService<ICalculator>()!;
        var interceptor = scope.GetService<CallTrackingInterceptor>();
        var decorator =
            new PicoAop_Tests_RuntimeIntegrationTests_ICalculator_CallTrackingInterceptorDecorator(
                inner,
                interceptor!
            );

        var result = decorator.Add(3, 4);
        await Assert.That(result).IsEqualTo(7);
        await Assert.That(interceptor!.CallCount).IsEqualTo(1);
    }
}
