namespace PicoDI.Gen.OpenGeneric;

/// <summary>
/// Collects closed generic type usage information from various sources in the source code.
/// </summary>
internal sealed class ClosedGenericCollector
{
    public static readonly ClosedGenericCollector Default = new();
    /// <summary>
    /// Gets closed generic type symbols from generic type declarations.
    /// </summary>
    /// <param name="context">The generator syntax context containing the node to analyze.</param>
    /// <returns>A closed generic type symbol if found, otherwise null.</returns>
    public ITypeSymbol? GetClosedGenericFromDeclaration(GeneratorSyntaxContext context)
    {
        if (context.Node is not GenericNameSyntax genericName)
            return null;

        var semanticModel = context.SemanticModel;
        var typeInfo = semanticModel.GetTypeInfo(genericName);
        var typeSymbol = typeInfo.Type;

        if (
            typeSymbol
            is not INamedTypeSymbol { IsGenericType: true, IsUnboundGenericType: false } namedType
        )
            return null;

        if (namedType.TypeArguments.Any(ta => ta is ITypeParameterSymbol))
            return null;

        var ns = namedType.ContainingNamespace?.ToDisplayString() ?? "";
        return ns.StartsWith(PicoDiNames.SystemNamespace) ? null : namedType;
    }

    /// <summary>
    /// Gets closed generic type symbols from GetService/GetServices invocation usages.
    /// </summary>
    /// <param name="context">The generator syntax context containing the node to analyze.</param>
    /// <returns>A closed generic type symbol if found, otherwise null.</returns>
    public ITypeSymbol? GetClosedGenericUsageInfo(GeneratorSyntaxContext context)
    {
        if (context.Node is not InvocationExpressionSyntax invocation)
            return null;

        var semanticModel = context.SemanticModel;
        var symbolInfo = semanticModel.GetSymbolInfo(invocation);

        if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
            return null;

        if (methodSymbol.Name is not (PicoDiNames.GetService or PicoDiNames.GetServices))
            return null;

        if (!methodSymbol.IsGenericMethod || methodSymbol.TypeArguments.Length is 0)
            return null;

        var typeArg = methodSymbol.TypeArguments[0];
        if (
            typeArg
            is not INamedTypeSymbol { IsGenericType: true, IsUnboundGenericType: false } namedType
        )
            return null;

        var ns = namedType.ContainingNamespace?.ToDisplayString() ?? "";
        return ns.StartsWith(PicoDiNames.SystemNamespace) ? null : namedType;
    }

    /// <summary>
    /// Gets closed generic type symbols from constructor parameters.
    /// </summary>
    /// <param name="context">The generator syntax context containing the node to analyze.</param>
    /// <returns>A collection of closed generic type symbols found in constructor parameters.</returns>
    public IEnumerable<ITypeSymbol> GetClosedGenericsFromConstructor(GeneratorSyntaxContext context)
    {
        var semanticModel = context.SemanticModel;
        IEnumerable<ParameterSyntax> parameters;

        switch (context.Node)
        {
            case ConstructorDeclarationSyntax ctorDecl:
                parameters = ctorDecl.ParameterList.Parameters;
                break;
            case TypeDeclarationSyntax { ParameterList: not null } typeDecl:
                parameters = typeDecl.ParameterList.Parameters;
                break;
            default:
                yield break;
        }

        foreach (var param in parameters)
        {
            if (param.Type is null)
                continue;

            var typeInfo = semanticModel.GetTypeInfo(param.Type);
            var typeSymbol = typeInfo.Type;

            if (
                typeSymbol
                is not INamedTypeSymbol
                {
                    IsGenericType: true,
                    IsUnboundGenericType: false
                } namedType
            )
                continue;

            if (namedType.TypeArguments.Any(ta => ta is ITypeParameterSymbol))
                continue;

            var ns = namedType.ContainingNamespace?.ToDisplayString() ?? "";
            if (ns.StartsWith(PicoDiNames.SystemNamespace))
                continue;

            yield return namedType;
        }
    }
}
