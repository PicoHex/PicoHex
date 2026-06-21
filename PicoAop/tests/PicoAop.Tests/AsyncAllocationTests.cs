namespace PicoAop.Tests;

public class AsyncAllocationTests : GeneratorTestBase
{
    [Test]
    public async Task TaskReturningMethod_InvokeTargetAsync_NoAsyncKeyword()
    {
        // 🔴 RED: For Task (non-generic) returning methods, the generated
        // Invocation struct's InvokeTargetAsync() uses async/await,
        // which allocates a state machine. The fix replaces this with
        // `new ValueTask(_target.Method(args))` — no async keyword,
        // no allocation.
        var source = """
            using PicoAop.Abs;
            using System.Threading.Tasks;

            interface IMyService
            {
                Task DoAsync();
            }

            class MyService : IMyService
            {
                public Task DoAsync() => Task.CompletedTask;
            }

            class MyInterceptor : InterceptorBase { }

            interface IDummyContainer
            {
                IDummyContainer Register<T, TImpl>() where T : class where TImpl : class;
            }

            static class Ext
            {
                internal static IDummyContainer InterceptBy<T>(this IDummyContainer c) where T : class => c;
            }

            static class Registration
            {
                static void Do(IDummyContainer c)
                {
                    c.Register<IMyService, MyService>().InterceptBy<MyInterceptor>();
                }
            }
            """;

        await RunGenerator(
            source,
            async result =>
            {
                var output = GetGeneratedOutput(result);
                Console.WriteLine("=== GENERATED OUTPUT ===");
                Console.WriteLine(output);

                // The generated code should NOT contain async InvokeTargetAsync
                // It should use: `internal ValueTask InvokeTargetAsync() => new(_target.DoAsync());`
                var asyncPattern = "async ValueTask InvokeTargetAsync";
                var hasAsync = output.Contains(asyncPattern);
                Console.WriteLine($"Contains '{asyncPattern}': {hasAsync}");

                // 🔴 RED ASSERT: This will FAIL with current code (which generates async/await).
                await Assert.That(hasAsync).IsFalse();

                // ✅ GREEN ASSERT: The generated code should use the new ValueTask wrapper
                var newValueTaskPattern = "=> new(_target.DoAsync())";
                await Assert.That(output.Contains(newValueTaskPattern)).IsTrue();
            }
        );
    }

    [Test]
    public async Task TaskOfTReturningMethod_InvokeTargetAsync_NoAsyncKeyword()
    {
        // Regression: Task<T> should also work correctly (wraps as new ValueTask<T>(...))
        var source = """
            using PicoAop.Abs;
            using System.Threading.Tasks;

            interface IMyService
            {
                Task<int> GetValueAsync();
            }

            class MyService : IMyService
            {
                public Task<int> GetValueAsync() => Task.FromResult(42);
            }

            class MyInterceptor : InterceptorBase { }

            interface IDummyContainer
            {
                IDummyContainer Register<T, TImpl>() where T : class where TImpl : class;
            }

            static class Ext
            {
                internal static IDummyContainer InterceptBy<T>(this IDummyContainer c) where T : class => c;
            }

            static class Registration
            {
                static void Do(IDummyContainer c)
                {
                    c.Register<IMyService, MyService>().InterceptBy<MyInterceptor>();
                }
            }
            """;

        await RunGenerator(
            source,
            async result =>
            {
                var output = GetGeneratedOutput(result);
                Console.WriteLine("=== GENERATED OUTPUT ===");
                Console.WriteLine(output);

                // Task<T> should wrap as new ValueTask<T>(_target.Method())
                var newValueTaskPattern = "=> new(";
                await Assert.That(output.Contains(newValueTaskPattern)).IsTrue();

                // Should NOT have async keyword
                await Assert.That(output.Contains("async ")).IsFalse();
            }
        );
    }

    [Test]
    public async Task ValueTaskMethod_InvokeTargetAsync_DirectPassThrough()
    {
        // Regression: ValueTask (both void and generic) should pass through directly
        var source = """
            using PicoAop.Abs;
            using System.Threading.Tasks;

            interface IMyService
            {
                ValueTask DoAsync();
            }

            class MyService : IMyService
            {
                public ValueTask DoAsync() => ValueTask.CompletedTask;
            }

            class MyInterceptor : InterceptorBase { }

            interface IDummyContainer
            {
                IDummyContainer Register<T, TImpl>() where T : class where TImpl : class;
            }

            static class Ext
            {
                internal static IDummyContainer InterceptBy<T>(this IDummyContainer c) where T : class => c;
            }

            static class Registration
            {
                static void Do(IDummyContainer c)
                {
                    c.Register<IMyService, MyService>().InterceptBy<MyInterceptor>();
                }
            }
            """;

        await RunGenerator(
            source,
            async result =>
            {
                var output = GetGeneratedOutput(result);
                // ValueTask should pass through directly (no new, no await, no async)
                await Assert.That(output.Contains("=> _target.DoAsync()")).IsTrue();
                await Assert.That(output.Contains("= new(")).IsFalse();
                await Assert.That(output.Contains("async ")).IsFalse();
            }
        );
    }
}
