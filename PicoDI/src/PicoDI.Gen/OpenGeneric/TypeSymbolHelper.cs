namespace PicoDI.Gen.OpenGeneric;

/// <summary>
/// Provides utility methods for working with type symbols.
/// </summary>
internal static class TypeSymbolHelper
{
    /// <summary>
    /// Determines whether a type symbol represents a closed generic type (not open, not containing type parameters).
    /// </summary>
    /// <param name="typeSymbol">The type symbol to check.</param>
    /// <returns>True if the type is a closed generic type; otherwise false.</returns>
    public static bool IsClosedGenericType(ITypeSymbol? typeSymbol)
    {
        if (
            typeSymbol
            is not INamedTypeSymbol { IsGenericType: true, IsUnboundGenericType: false } namedType
        )
            return false;

        return !namedType.TypeArguments.Any(ta => ta is ITypeParameterSymbol);
    }

    /// <summary>
    /// Determines whether a type symbol represents an open generic type (unbound generic type).
    /// </summary>
    /// <param name="typeSymbol">The type symbol to check.</param>
    /// <returns>True if the type is an open generic type; otherwise false.</returns>
    public static bool IsOpenGenericType(ITypeSymbol? typeSymbol)
    {
        return typeSymbol is INamedTypeSymbol { IsUnboundGenericType: true };
    }

    /// <summary>
    /// Gets the open generic type (unbound generic type) from a closed generic type.
    /// </summary>
    /// <param name="closedType">The closed generic type symbol.</param>
    /// <returns>The unbound generic type symbol, or null if the input is not a closed generic type.</returns>
    public static INamedTypeSymbol? GetOpenGenericType(INamedTypeSymbol closedType)
    {
        if (!closedType.IsGenericType || closedType.IsUnboundGenericType)
            return null;

        return closedType.ConstructUnboundGenericType();
    }

    /// <summary>
    /// Checks if a type is in the System namespace (or its sub-namespaces).
    /// </summary>
    /// <param name="typeSymbol">The type symbol to check.</param>
    /// <returns>True if the type is in the System namespace; otherwise false.</returns>
    public static bool IsSystemType(ITypeSymbol typeSymbol)
    {
        var ns = typeSymbol.ContainingNamespace?.ToDisplayString() ?? "";
        return ns.StartsWith(PicoDiNames.SystemNamespace);
    }

    /// <summary>
    /// Gets the fully qualified display name of a type symbol.
    /// </summary>
    /// <param name="typeSymbol">The type symbol.</param>
    /// <returns>The fully qualified display name.</returns>
    public static string GetFullyQualifiedName(ITypeSymbol typeSymbol)
    {
        return typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }

    /// <summary>
    /// Gets the simple name of a type (without namespace or generic arguments).
    /// </summary>
    /// <param name="typeSymbol">The type symbol.</param>
    /// <returns>The simple name.</returns>
    public static string GetSimpleName(ITypeSymbol typeSymbol)
    {
        return typeSymbol.Name;
    }
}
