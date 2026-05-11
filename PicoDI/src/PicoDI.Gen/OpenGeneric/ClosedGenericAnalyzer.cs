namespace PicoDI.Gen.OpenGeneric;

/// <summary>
/// Analyzes closed generic type usage and converts them to a unified model.
/// </summary>
internal sealed class ClosedGenericAnalyzer
{
    /// <summary>
    /// Analyzes a closed generic type symbol and converts it to a <see cref="ClosedGenericUsage"/> model.
    /// </summary>
    /// <param name="closedType">The closed generic type symbol to analyze.</param>
    /// <returns>A <see cref="ClosedGenericUsage"/> model if the type is a valid closed generic, otherwise null.</returns>
    public ClosedGenericUsage? AnalyzeClosedGenericUsage(ITypeSymbol closedType)
    {
        if (closedType is not INamedTypeSymbol { IsGenericType: true } namedType)
            return null;

        if (namedType.TypeArguments.Any(ta => ta is ITypeParameterSymbol))
            return null;

        var openType = namedType.ConstructUnboundGenericType();
        var typeArguments = namedType
            .TypeArguments
            .Select(t => t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            .ToImmutableArray();

        return new ClosedGenericUsage(
            namedType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            openType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            typeArguments
        );
    }

    /// <summary>
    /// Collects and merges closed generic usages from multiple sources.
    /// </summary>
    /// <param name="closedGenericUsages">Closed generic usages from GetService/GetServices invocations.</param>
    /// <param name="closedGenericDeclarations">Closed generic declarations from type declarations.</param>
    /// <param name="ctorClosedGenerics">Closed generics from constructor parameters.</param>
    /// <param name="registrations">Existing service registrations to scan for constructor dependencies.</param>
    /// <returns>A distinct list of closed generic usages.</returns>
    public List<ClosedGenericUsage> CollectClosedGenericUsages(
        ImmutableArray<ITypeSymbol?> closedGenericUsages,
        ImmutableArray<ITypeSymbol?> closedGenericDeclarations,
        ImmutableArray<ITypeSymbol> ctorClosedGenerics,
        List<ServiceRegistration> registrations
    )
    {
        var collected = closedGenericUsages
            .Where(x => x is not null)
            .Select(x => AnalyzeClosedGenericUsage(x!))
            .OfType<ClosedGenericUsage>()
            .Distinct()
            .ToList();

        MergeDistinct(
            collected,
            closedGenericDeclarations
                .Where(x => x is not null)
                .Select(x => AnalyzeClosedGenericUsage(x!))
                .OfType<ClosedGenericUsage>()
                .Distinct()
                .ToList()
        );

        MergeDistinct(
            collected,
            ctorClosedGenerics
                .Select(AnalyzeClosedGenericUsage)
                .OfType<ClosedGenericUsage>()
                .Distinct()
                .ToList()
        );

        MergeDistinct(collected, CollectConstructorClosedGenericUsages(registrations));
        return collected;
    }

    private static List<ClosedGenericUsage> CollectConstructorClosedGenericUsages(
        List<ServiceRegistration> registrations
    )
    {
        return registrations
            .SelectMany(r => r.ConstructorParameters)
            .Where(typeFullName => typeFullName.Contains("<"))
            .Select(CreateClosedGenericUsageFromTypeName)
            .Where(x => x is not null)
            .Cast<ClosedGenericUsage>()
            .Distinct()
            .ToList();
    }

    private static ClosedGenericUsage? CreateClosedGenericUsageFromTypeName(string typeFullName)
    {
        var angleIndex = typeFullName.IndexOf('<');
        if (angleIndex < 0)
            return null;

        var baseName = typeFullName.Substring(0, angleIndex);
        if (baseName.StartsWith(PicoDiNames.GlobalSystemPrefix))
            return null;

        var typeArgumentsSection = typeFullName.Substring(
            angleIndex + 1,
            typeFullName.Length - angleIndex - 2
        );
        var typeArguments = ParseTypeArguments(typeArgumentsSection).ToImmutableArray();
        var openGenericArityPlaceholder =
            typeArguments.Length > 0 ? new string(',', typeArguments.Length - 1) : string.Empty;

        return new ClosedGenericUsage(
            typeFullName,
            $"{baseName}<{openGenericArityPlaceholder}>",
            typeArguments
        );
    }

    private static List<string> ParseTypeArguments(string typeArgumentsSection)
    {
        var result = new List<string>();
        var depth = 0;
        var start = 0;

        for (var index = 0; index < typeArgumentsSection.Length; index++)
        {
            var character = typeArgumentsSection[index];
            switch (character)
            {
                case '<':
                    depth++;
                    break;
                case '>':
                    depth--;
                    break;
                case ',' when depth is 0:
                    result.Add(typeArgumentsSection.Substring(start, index - start).Trim());
                    start = index + 1;
                    break;
            }
        }

        if (start < typeArgumentsSection.Length)
            result.Add(typeArgumentsSection.Substring(start).Trim());

        return result;
    }

    private static void MergeDistinct<T>(List<T> target, IEnumerable<T> source)
    {
        foreach (var item in source)
        {
            if (!target.Contains(item))
                target.Add(item);
        }
    }
}
