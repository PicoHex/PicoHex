using Microsoft.CodeAnalysis;
using PicoAop.Abs;
using PicoAop.DI;
using PicoDI;
using PicoDI.Abs;
using System.Collections.Immutable;

namespace PicoAop.Tests;

internal static class TestMetadata
{
    public static MetadataReference[] GetReferences()
    {
        var trustedPlatformAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))!
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        var explicitAssemblies = new[]
        {
            typeof(SvcContainer).Assembly.Location,
            typeof(ISvcContainer).Assembly.Location,
            typeof(IInterceptor).Assembly.Location,
            typeof(SvcContainerInterceptorExtensions).Assembly.Location,
        };

        return trustedPlatformAssemblies
            .Concat(explicitAssemblies)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(static path => MetadataReference.CreateFromFile(path))
            .ToArray();
    }
}
