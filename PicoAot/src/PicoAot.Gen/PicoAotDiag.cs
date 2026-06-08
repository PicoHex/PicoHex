namespace PicoAot.Gen;

internal static class PicoAotDiag
{
    private const string Category = "PicoAot";

    public static readonly DiagnosticDescriptor SealedType = new(
        "PICO101", "Sealed type cannot be intercepted",
        "Type '{0}' is sealed. Extract an interface for interception",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ValueType = new(
        "PICO102", "Value type cannot be intercepted",
        "Type '{0}' is a value type. Extract an interface for interception",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NotAnInterceptor = new(
        "PICO105", "Type does not implement IInterceptor",
        "Type '{0}' does not implement IInterceptor",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor RefOutMethod = new(
        "PICO110", "Method with ref/out parameters delegated without interception",
        "Method '{0}' on '{1}' has ref/out parameters and will not be intercepted (direct delegation)",
        Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);
}
