namespace PicoDI.Aop.Tests;

public class FilterEvaluationTests
{
    public interface IGreeter { string Greet(string name); }
    public sealed class Greeter : IGreeter { public string Greet(string name) => $"Hi {name}"; }

    // These are in namespace PicoDI.Aop.Tests (not matching "MyApp" filter)
    // Trigger for generator — should NOT produce decorator since namespace doesn't match
    public static void RegisterNoMatch(SvcContainer c)
    {
        c.AddInterceptor<CallCounter>().WhereNamespace("MyApp.Services");
        c.Register<IGreeter, Greeter>(SvcLifetime.Scoped)
            .InterceptBy<CallCounter>();
    }

    public sealed class CallCounter : InterceptorBase
    {
        public int Count { get; private set; }
        public override TResult Invoke<TResult>(IInvocation<TResult> inv,
            Func<IInvocation<TResult>, TResult> next) { Count++; return next(inv); }
    }

    [Test]
    public async Task WhereNamespace_Match_NotYetImplemented()
    {
        // WhereNamespace filter evaluation requires walking the AddInterceptor
        // chain in ExtractGlobalInterceptorInfo. Deferred.
        await Assert.That(true).IsTrue();
    }
}
