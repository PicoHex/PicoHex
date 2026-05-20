namespace PicoDI.Gen.OpenGeneric;

/// <summary>
/// Handles type parameter substitution logic for generic type names.
/// </summary>
internal sealed class TypeParameterSubstitutor
{
    /// <summary>
    /// Substitutes type parameters in a type name with actual type arguments.
    /// Parameters are processed in deterministic order (longest name first) to
    /// prevent shorter parameter names from matching within longer ones'
    /// replacement values.
    /// </summary>
    /// <param name="typeFullName">The fully qualified type name with type parameters.</param>
    /// <param name="typeParameterMap">Mapping from type parameter names to actual type names.</param>
    /// <returns>The type name with parameters substituted.</returns>
    public string SubstituteTypeParameters(
        string typeFullName,
        Dictionary<string, string> typeParameterMap
    )
    {
        var result = typeFullName;

        // Sort by key length descending so longer parameter names are substituted first.
        // This prevents shorter names (e.g. "T") from matching within longer replacement
        // values that happen to contain the same letters (e.g. "T" in "TValue").
        foreach (var kvp in typeParameterMap.OrderByDescending(k => k.Key.Length))
        {
            var parameterName = kvp.Key;
            var actualType = kvp.Value;

            if (result == parameterName)
            {
                result = actualType;
                continue;
            }

            result = SubstituteTypeParameterInGeneric(result, parameterName, actualType);
        }

        return result;
    }

    /// <summary>
    /// Substitutes type parameters in constructor parameter types using Roslyn semantic model.
    /// Resolves type parameters via <see cref="INamedTypeSymbol.Construct"/> for accurate
    /// handling of nested generics (e.g., <c>Dictionary&lt;string, List&lt;T&gt;&gt;</c>)
    /// which string-based substitution cannot handle correctly.
    /// </summary>
    /// <param name="parameterTypeSymbols">The constructor parameter type symbols (may contain type parameters).</param>
    /// <param name="typeArgumentSymbols">The closed generic type arguments to substitute.</param>
    /// <param name="typeParameterNames">The type parameter names of the open generic.</param>
    /// <returns>The substituted constructor parameter type full names.</returns>
    public ImmutableArray<string> SubstituteTypeParametersWithSymbols(
        ImmutableArray<ITypeSymbol> parameterTypeSymbols,
        ImmutableArray<ITypeSymbol> typeArgumentSymbols,
        ImmutableArray<string> typeParameterNames
    )
    {
        return parameterTypeSymbols
            .Select(
                paramType =>
                    ResolveTypeParametersRecursive(
                        paramType,
                        typeArgumentSymbols,
                        typeParameterNames
                    )
            )
            .Select(resolved => resolved.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            .ToImmutableArray();
    }

    /// <summary>
    /// Recursively resolves type parameters within an <see cref="ITypeSymbol"/> tree.
    /// </summary>
    private static ITypeSymbol ResolveTypeParametersRecursive(
        ITypeSymbol type,
        ImmutableArray<ITypeSymbol> typeArgumentSymbols,
        ImmutableArray<string> typeParameterNames
    )
    {
        if (type is ITypeParameterSymbol typeParam)
        {
            for (var i = 0; i < typeParameterNames.Length; i++)
            {
                if (string.Equals(typeParameterNames[i], typeParam.Name, StringComparison.Ordinal))
                {
                    if (i < typeArgumentSymbols.Length)
                        return typeArgumentSymbols[i];
                    break;
                }
            }
            return type;
        }

        if (type is INamedTypeSymbol namedType && namedType.TypeArguments.Length > 0)
        {
            var resolvedArgs = namedType
                .TypeArguments
                .Select(
                    ta =>
                        ResolveTypeParametersRecursive(ta, typeArgumentSymbols, typeParameterNames)
                )
                .ToArray();

            return namedType.OriginalDefinition.Construct(resolvedArgs);
        }

        return type;
    }

    /// <summary>
    /// Builds a closed generic type name from an open generic type name and type arguments.
    /// </summary>
    /// <param name="openTypeFullName">The fully qualified open generic type name.</param>
    /// <param name="typeArguments">The type arguments to apply.</param>
    /// <returns>The closed generic type name.</returns>
    public string BuildClosedGenericTypeName(
        string openTypeFullName,
        ImmutableArray<string> typeArguments
    )
    {
        var angleBracketIndex = openTypeFullName.IndexOf('<');
        if (angleBracketIndex < 0)
            return openTypeFullName;

        var baseName = openTypeFullName.Substring(0, angleBracketIndex);
        var typeArgumentsString = string.Join(", ", typeArguments);
        return $"{baseName}<{typeArgumentsString}>";
    }

    private static string SubstituteTypeParameterInGeneric(
        string typeFullName,
        string parameterName,
        string actualType
    )
    {
        var patterns = new[]
        {
            ($"<{parameterName}>", $"<{actualType}>"),
            ($"<{parameterName},", $"<{actualType},"),
            ($", {parameterName}>", $", {actualType}>"),
            ($", {parameterName},", $", {actualType},")
        };

        var result = typeFullName;
        foreach (var (pattern, replacement) in patterns)
        {
            result = result.Replace(pattern, replacement);
        }

        return result;
    }
}
