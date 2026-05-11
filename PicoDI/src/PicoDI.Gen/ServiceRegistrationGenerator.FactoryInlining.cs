namespace PicoDI.Gen;

internal static partial class ServiceRegistrationSourceEmitter
{
    /// <summary>
    /// Generates an inlined factory expression for the given registration.
    /// For Transient dependencies, recursively inlines the construction.
    /// For Singleton/Scoped dependencies, calls scope.GetService().
    /// </summary>
    private static string GenerateInlinedFactory(
        ServiceRegistration reg,
        Dictionary<string, ServiceRegistration> registrationLookup,
        HashSet<string> visitedTypes,
        int indentLevel,
        string scopeVarName = "scope"
    )
    {
        if (reg.ConstructorParameters.IsEmpty)
            return $"new {reg.ImplementationTypeFullName}()";

        var paramExpressions = reg.ConstructorParameters
            .Select(
                paramTypeFullName =>
                    GenerateParameterExpression(
                        paramTypeFullName,
                        registrationLookup,
                        visitedTypes,
                        indentLevel + 1,
                        scopeVarName
                    )
            )
            .ToList();

        if (paramExpressions.Count == 1)
            return $"new {reg.ImplementationTypeFullName}({paramExpressions[0]})";

        // Manual comma join avoids string.Join allocations on the source-generator hot path.
        var sb = new StringBuilder();
        sb.Append($"new {reg.ImplementationTypeFullName}(");
        for (var i = 0; i < paramExpressions.Count; i++)
        {
            var comma = i < paramExpressions.Count - 1 ? "," : "";
            sb.Append(i is 0 ? paramExpressions[i] + comma : " " + paramExpressions[i] + comma);
        }
        sb.Append(")");

        return sb.ToString();
    }

    /// <summary>
    /// Generates the expression for a constructor parameter.
    /// Inlines Transient dependencies, uses GetService for Singleton/Scoped.
    /// </summary>
    private static string GenerateParameterExpression(
        string paramTypeFullName,
        Dictionary<string, ServiceRegistration> registrationLookup,
        HashSet<string> visitedTypes,
        int indentLevel,
        string scopeVarName = "scope"
    )
    {
        if (!registrationLookup.TryGetValue(paramTypeFullName, out var depReg))
            return $"({paramTypeFullName}){scopeVarName}.GetService(typeof({paramTypeFullName}))";

        var resolverName = GetResolverMethodName(paramTypeFullName);
        if (depReg.Lifetime != PicoDiNames.Transient)
            return $"Resolve.{resolverName}({scopeVarName})";

        if (visitedTypes.Contains(paramTypeFullName))
            return $"Resolve.{resolverName}({scopeVarName})";

        var newVisited = new HashSet<string>(visitedTypes) { paramTypeFullName };
        return GenerateInlinedFactory(
            depReg,
            registrationLookup,
            newVisited,
            indentLevel,
            scopeVarName
        );
    }
}
