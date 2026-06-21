namespace PicoAop.Gen.Emission;

internal static class InvocationEmitter
{
    public static string BuildStructName(
        string safeSvcName,
        IMethodSymbol method,
        ITypeSymbol? interceptorType = null
    )
    {
        var name = $"Invocation_{safeSvcName}_{method.Name}";
        foreach (var p in method.Parameters)
            name += $"_{Sanitize(p.Type.Name)}";
        if (interceptorType != null)
            name +=
                $"_{Sanitize(interceptorType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))}";
        return name;
    }

    public static string Sanitize(string name) =>
        name.Replace("global::", "")
            .Replace("::", "_")
            .Replace('.', '_')
            .Replace('<', '_')
            .Replace('>', '_')
            .Replace(',', '_')
            .Replace(' ', '_')
            .Replace('+', '_');

    public static string BuildSafeServiceName(ITypeSymbol serviceType) =>
        Sanitize(serviceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
}
