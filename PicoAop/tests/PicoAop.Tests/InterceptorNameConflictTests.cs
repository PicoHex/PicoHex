namespace PicoAop.Tests;

public class InterceptorNameConflictTests : GeneratorTestBase
{
    [Test]
    public async Task SameInterceptorName_DifferentNamespaces_NoCollision()
    {
        // 🔴 RED: Currently the generator uses only t.Name (short name) for the
        // interceptor type suffix. Two interceptors with the same name in different
        // namespaces would generate duplicate struct/wrapper identifiers.
        var source = """
            using PicoAop.Abs;
            namespace A
            {
                class MyInterceptor : InterceptorBase { }
            }
            namespace B
            {
                class MyInterceptor : InterceptorBase { }
            }
            interface ISvc1 { int GetValue(); }
            interface ISvc2 { string GetName(); }
            class Svc1 : ISvc1 { public int GetValue() => 42; }
            class Svc2 : ISvc2 { public string GetName() => "test"; }
            interface IDummyContainer
            {
                IDummyContainer Register<T, TImpl>() where T : class where TImpl : class;
                IDummyContainer InterceptBy<T>() where T : class;
            }
            static class Registration
            {
                static void Do(IDummyContainer c)
                {
                    c.Register<ISvc1, Svc1>().InterceptBy<A.MyInterceptor>();
                    c.Register<ISvc2, Svc2>().InterceptBy<B.MyInterceptor>();
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
                // The generated code should include namespace disambiguation.
                // Look for wrapper methods — they should have different names
                // (containing the namespace), not identical names.
                // Count wrapper method definitions for the conflicting type names
                var wrapCount = CountOccurrences(output, "Wrap_");
                // Each service + interceptor combo should have exactly one wrapper
                // If there's a collision, there would be duplicates or errors
                var lines = output.Split('\n');
                var wrapLines = lines.Where(l => l.Contains("Wrap_")).ToList();
                // We should have 2 wrapper methods (one per service+interceptor combo)
                await Assert.That(wrapLines.Count).IsEqualTo(2);
                // Each wrapper should have a unique name
                await Assert.That(wrapLines[0]).IsNotEqualTo(wrapLines[1]);
            }
        );
    }

    [Test]
    public async Task SameInterceptorName_SameNamespace_StillWorks()
    {
        // When two interceptors have different names in the same namespace,
        // things should still work as before (regression test)
        var source = """
            using PicoAop.Abs;
            class Int1 : InterceptorBase { }
            class Int2 : InterceptorBase { }
            interface ISvc { int GetValue(); }
            class Svc : ISvc { public int GetValue() => 42; }
            interface IDummyContainer
            {
                IDummyContainer Register<T, TImpl>() where T : class where TImpl : class;
                IDummyContainer InterceptBy<T>() where T : class;
            }
            static class Registration
            {
                static void Do(IDummyContainer c)
                {
                    c.Register<ISvc, Svc>().InterceptBy<Int1>().InterceptBy<Int2>();
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
                // Should have both interceptors in the output
                await Assert.That(output.Contains("Int1")).IsTrue();
                await Assert.That(output.Contains("Int2")).IsTrue();
                // No duplicate identifiers
                var lines = output.Split('\n');
                var structLines = lines.Where(l => l.TrimStart().StartsWith("struct ")).ToList();
                var uniqueStructs = structLines.Select(l => l.Trim()).Distinct().Count();
                await Assert.That(uniqueStructs).IsEqualTo(structLines.Count);
            }
        );
    }

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0,
            pos = 0;
        while ((pos = text.IndexOf(pattern, pos, StringComparison.Ordinal)) >= 0)
        {
            count++;
            pos += pattern.Length;
        }
        return count;
    }
}
