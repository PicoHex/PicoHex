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
