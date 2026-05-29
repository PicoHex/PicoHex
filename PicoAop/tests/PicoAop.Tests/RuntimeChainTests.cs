namespace PicoAop.Tests;

public class RuntimeChainTests
{
    public interface IStage
    {
        string Execute();
    }

    public sealed class Stage : IStage
    {
        public string Execute() => "impl";
    }

    public sealed class AppendInterceptor(string label) : InterceptorBase
    {
        public override TResult Invoke<TResult>(
            IInvocation<TResult> inv,
            Func<IInvocation<TResult>, TResult> next
        )
        {
            var inner = next(inv)!.ToString();
            return (TResult)(object)($"{label}->{inner}");
        }
    }

    public sealed class ShortCircuitInterceptor : InterceptorBase
    {
        public override TResult Invoke<TResult>(
            IInvocation<TResult> inv,
            Func<IInvocation<TResult>, TResult> next
        ) => (TResult)(object)"short";
    }

    public sealed class CountingInterceptor : InterceptorBase
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

    // Trigger the source generator to emit decorator types.
    public static void TriggerGen(SvcContainer c)
    {
        c.Register<IStage, Stage>(SvcLifetime.Scoped)
            .InterceptBy<AppendInterceptor>()
            .InterceptBy<ShortCircuitInterceptor>();
    }

    private static TDecorator CreateDecorator<TService, TDecorator>(
        TService inner,
        IInterceptor interceptor
    )
        where TDecorator : TService
    {
        var ctor = typeof(TDecorator).GetConstructors().First();
        return (TDecorator)ctor.Invoke([inner, interceptor]);
    }

    [Test]
    public async Task ChainOf3_OuterToInnerOrder()
    {
        var stage = new Stage();
        var innerDec = CreateDecorator<
            IStage,
            PicoAop_Tests_RuntimeChainTests_IStage_AppendInterceptorDecorator
        >(stage, new AppendInterceptor("Inner"));
        var middleDec = CreateDecorator<
            IStage,
            PicoAop_Tests_RuntimeChainTests_IStage_AppendInterceptorDecorator
        >(innerDec, new AppendInterceptor("Middle"));
        var outerDec = CreateDecorator<
            IStage,
            PicoAop_Tests_RuntimeChainTests_IStage_AppendInterceptorDecorator
        >(middleDec, new AppendInterceptor("Outer"));

        var result = outerDec.Execute();
        await Assert.That(result).IsEqualTo("Outer->Middle->Inner->impl");
    }

    [Test]
    public async Task ChainOf3_InnerToOuterResultFlow()
    {
        var stage = new Stage();
        var innerDec = CreateDecorator<
            IStage,
            PicoAop_Tests_RuntimeChainTests_IStage_AppendInterceptorDecorator
        >(stage, new AppendInterceptor("A"));
        var middleDec = CreateDecorator<
            IStage,
            PicoAop_Tests_RuntimeChainTests_IStage_AppendInterceptorDecorator
        >(innerDec, new AppendInterceptor("B"));
        var outerDec = CreateDecorator<
            IStage,
            PicoAop_Tests_RuntimeChainTests_IStage_AppendInterceptorDecorator
        >(middleDec, new AppendInterceptor("C"));

        var result = outerDec.Execute();
        await Assert.That(result).IsEqualTo("C->B->A->impl");
    }

    [Test]
    public async Task MiddleBreaksChain_InnerNotCalled()
    {
        var stage = new Stage();
        var innerDec = CreateDecorator<
            IStage,
            PicoAop_Tests_RuntimeChainTests_IStage_AppendInterceptorDecorator
        >(stage, new AppendInterceptor("Inner"));
        var shortDec = CreateDecorator<
            IStage,
            PicoAop_Tests_RuntimeChainTests_IStage_ShortCircuitInterceptorDecorator
        >(innerDec, new ShortCircuitInterceptor());

        var result = shortDec.Execute();
        await Assert.That(result).IsEqualTo("short");
    }

    [Test]
    public async Task ScopedInterceptor_DifferentPerScope()
    {
        var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterScoped<CountingInterceptor>(_ => new CountingInterceptor());
        container.Register<IStage>(scope => new Stage(), SvcLifetime.Scoped);
        container.Build();

        await using var s1 = container.CreateScope();
        await using var s2 = container.CreateScope();
        var i1 = s1.GetService<CountingInterceptor>();
        var i2 = s2.GetService<CountingInterceptor>();
        await Assert.That(i1).IsNotSameReferenceAs(i2);
    }

    [Test]
    public async Task SingletonInterceptor_SameAcrossScopes()
    {
        var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterSingleton<CountingInterceptor>(_ => new CountingInterceptor());
        container.Register<IStage>(scope => new Stage(), SvcLifetime.Scoped);
        container.Build();

        await using var s1 = container.CreateScope();
        await using var s2 = container.CreateScope();
        var i1 = s1.GetService<CountingInterceptor>();
        var i2 = s2.GetService<CountingInterceptor>();
        await Assert.That(i1).IsSameReferenceAs(i2);
    }
}
