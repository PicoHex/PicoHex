namespace PicoAop.Tests;

using PicoAop.DI;

public class InterceptorRuntimeIntegrationTests
{
    [Test]
    public async Task InterceptBy_Chain_AppliesInterceptor_AtRuntime()
    {
        var container = new SvcContainer();
        container.RegisterSingleton<RuntimeCountingInterceptor>();
        container
            .RegisterScoped<IRuntimeSampleService, RuntimeSampleService>()
            .InterceptBy<RuntimeCountingInterceptor>();
        container.Build();

        await using var scope = container.CreateScope();
        var service = scope.GetService<IRuntimeSampleService>()!;
        service.DoWork();
        service.DoWork();

        await Assert.That(RuntimeCountingInterceptor.InvocationCount).IsEqualTo(2);
    }
}

public interface IRuntimeSampleService
{
    void DoWork();
}

public sealed class RuntimeSampleService : IRuntimeSampleService
{
    public void DoWork() { }
}

public sealed class RuntimeCountingInterceptor : InterceptorBase
{
    public static int InvocationCount;

    public override void InvokeVoid<TInvocation>(TInvocation inv, Func<TInvocation, object?> next)
        where TInvocation : struct
    {
        Interlocked.Increment(ref InvocationCount);
        next(inv);
    }
}
