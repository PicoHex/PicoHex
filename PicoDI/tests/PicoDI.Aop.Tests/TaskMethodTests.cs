namespace PicoDI.Aop.Tests;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PicoDI.Gen;

public class TaskMethodTests
{
    [Test]
    public async Task TaskInt_Method_GeneratesValidCompilation()
    {
        var inputSource = """
            using System.Threading.Tasks;
            using PicoDI;
            using PicoDI.Abs;
            using PicoDI.Aop;

            public interface IAsyncWorker { Task<int> FetchAsync(int id); }
            public sealed class AsyncWorker : IAsyncWorker {
                public Task<int> FetchAsync(int id) => Task.FromResult(id * 2);
            }
            public sealed class LogInterceptor : InterceptorBase { }

            public static class Setup {
                public static void Configure(SvcContainer c) {
                    c.Register<IAsyncWorker, AsyncWorker>(SvcLifetime.Scoped)
                        .InterceptBy<LogInterceptor>();
                }
            }
            """;

        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var inputTree = CSharpSyntaxTree.ParseText(inputSource, parseOptions);
        var references = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))!
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Concat([typeof(SvcContainer).Assembly.Location, typeof(ISvcContainer).Assembly.Location,
                     typeof(IInterceptor).Assembly.Location, typeof(SvcContainerInterceptorExtensions).Assembly.Location])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(static p => MetadataReference.CreateFromFile(p)).ToArray();

        var comp = CSharpCompilation.Create("TaskTest", [inputTree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var driver = CSharpGeneratorDriver.Create(
            [new InterceptorGenerator().AsSourceGenerator()], parseOptions: parseOptions);
        driver.RunGeneratorsAndUpdateCompilation(comp, out var oc, out _);

        using var ms = new MemoryStream();
        await Assert.That(oc.Emit(ms).Success).IsTrue();
    }
}
