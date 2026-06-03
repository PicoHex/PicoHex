namespace PicoDI.Gen.Constants;

/// <summary>
/// Centralized constants for PicoDI type and method names.
/// </summary>
internal static class PicoDiNames
{
    // Namespace
    public const string RootNamespace = "PicoDI";

    // Type names
    public const string SvcContainer = "SvcContainer";
    public const string ISvcContainer = "ISvcContainer";
    public const string SvcScope = "SvcScope";
    public const string ISvcScope = "ISvcScope";
    public const string SvcLifetime = "SvcLifetime";
    public const string SvcConstructorAttributeFullName = "PicoDI.Abs.SvcConstructorAttribute";

    // Fully qualified type names for semantic matching
    public const string SvcContainerFullName = "PicoDI.SvcContainer";
    public const string ISvcContainerFullName = "PicoDI.Abs.ISvcContainer";
    public const string SvcScopeFullName = "PicoDI.SvcScope";
    public const string ISvcScopeFullName = "PicoDI.Abs.ISvcScope";
    public const string Func = nameof(Func);
    public const string Type = nameof(System.Type);

    // Method names
    public const string Register = "Register";
    public const string RegisterTransient = "RegisterTransient";
    public const string RegisterScoped = "RegisterScoped";
    public const string RegisterSingleton = "RegisterSingleton";
    public const string GetService = "GetService";
    public const string GetServices = "GetServices";

    // Lifetime keywords
    public const string Transient = "Transient";
    public const string Scoped = "Scoped";
    public const string Singleton = "Singleton";

    // Namespace prefixes
    public const string SystemNamespace = nameof(System);
    public const string GlobalPrefix = "global::";
    public const string GlobalSystemPrefix = $"{GlobalPrefix}{nameof(System)}";

    // Method name collection
    public static readonly string[] RegisterMethodNames =
    [
        Register,
        RegisterTransient,
        RegisterScoped,
        RegisterSingleton,
    ];

    /// <summary>
    /// Determines whether the given method symbol belongs to PicoDI.
    /// Uses namespace checks and semantic interface resolution instead of string matching.
    /// </summary>
    public static bool IsPicoDiMethod(IMethodSymbol methodSymbol)
    {
        var containingNs = methodSymbol.ContainingNamespace?.ToDisplayString() ?? "";
        if (IsPicoDiNamespace(containingNs))
            return true;

        var reducedFromNs = methodSymbol.ReducedFrom?.ContainingNamespace?.ToDisplayString() ?? "";
        if (IsPicoDiNamespace(reducedFromNs))
            return true;

        if (methodSymbol.ReceiverType is { } receiverType && IsPicoDiReceiverType(receiverType))
            return true;

        if (methodSymbol.ContainingType is { } ct && IsPicoDiReceiverType(ct))
            return true;

        return false;
    }

    private static bool IsPicoDiReceiverType(ITypeSymbol type)
    {
        var displayName = type.ToDisplayString();
        if (
            displayName
            is SvcContainerFullName
                or ISvcContainerFullName
                or SvcScopeFullName
                or ISvcScopeFullName
        )
            return true;

        foreach (var iface in type.AllInterfaces)
        {
            var name = iface.ToDisplayString();
            if (name is ISvcContainerFullName or ISvcScopeFullName)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Infers lifetime from a Register* method name (e.g., RegisterTransient → Transient).
    /// Returns <paramref name="fallback"/> if no lifetime keyword is found.
    /// </summary>
    public static string InferLifetimeFromMethodName(string methodName, string fallback = Singleton)
    {
        if (methodName == RegisterTransient)
            return Transient;
        if (methodName == RegisterScoped)
            return Scoped;
        if (methodName == RegisterSingleton)
            return Singleton;
        return fallback;
    }

    /// <summary>
    /// Tests whether a namespace display string belongs to PicoDI.
    /// Matches exactly "PicoDI" or any sub-namespace "PicoDI.*".
    /// </summary>
    private static bool IsPicoDiNamespace(string ns)
    {
        return ns == RootNamespace
            || (
                ns.Length > RootNamespace.Length
                && ns.StartsWith(RootNamespace, StringComparison.Ordinal)
                && ns[RootNamespace.Length] == '.'
            );
    }

    /// <summary>
    /// Parses a SvcLifetime value from its expression text (e.g., "SvcLifetime.Transient").
    /// Uses precise member-access-aware matching to avoid false positives.
    /// </summary>
    public static string ParseLifetimeFromExpression(string expressionText)
    {
        if (EndsWithLifetime(expressionText, Transient))
            return Transient;
        if (EndsWithLifetime(expressionText, Scoped))
            return Scoped;
        return Singleton;
    }

    private static bool EndsWithLifetime(string text, string lifetime)
    {
        // Match "SvcLifetime.Transient" or standalone "Transient"
        if (text.EndsWith(lifetime, StringComparison.Ordinal))
        {
            int start = text.Length - lifetime.Length;
            return start is 0 || text[start - 1] == '.';
        }
        return false;
    }
}
