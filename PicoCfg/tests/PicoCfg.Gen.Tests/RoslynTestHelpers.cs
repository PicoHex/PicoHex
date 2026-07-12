using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using PicoCfg;

namespace PicoCfg.Gen.Tests;

/// <summary>
/// Shared test helpers for Roslyn-based source generator tests.
/// </summary>
internal static class RoslynTestHelpers
{
    [UnconditionalSuppressMessage(
        "AOT",
        "IL3000",
        Justification = "Roslyn-based generator tests construct metadata references from file-backed assemblies during test execution."
    )]
    public static MetadataReference[] GetMetadataReferences(params Type[] additionalExplicitTypes)
    {
        var trustedPlatformAssemblies = (
            (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")
        )!.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        var explicitAssemblies = new[] { typeof(CfgBind).Assembly.Location }
            .Concat(additionalExplicitTypes.Select(t => t.Assembly.Location))
            .ToArray();

        return trustedPlatformAssemblies
            .Concat(explicitAssemblies)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(static path => MetadataReference.CreateFromFile(path))
            .ToArray();
    }
}
