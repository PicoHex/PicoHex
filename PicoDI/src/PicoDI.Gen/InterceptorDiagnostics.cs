namespace PicoDI.Gen;

internal static class InterceptorDiagnostics
{
    public const string Category = "PicoDI.Aop";

    public static readonly DiagnosticDescriptor InterceptorTypeMismatch =
        new(
            "PICO010",
            "Interceptor type does not implement IInterceptor",
            "Type '{0}' is used in InterceptBy<T>() but does not implement IInterceptor.",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

    public static readonly DiagnosticDescriptor FilterRequiresInterface =
        new(
            "PICO011",
            "Filter type is not an interface",
            "WhereImplements<{0}> requires an interface type.",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

    public static readonly DiagnosticDescriptor ZeroInterceptorsMatched =
        new(
            "PICO012",
            "No interceptors matched for service",
            "Service '{0}' has no matching interceptors.",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true
        );

    public static readonly DiagnosticDescriptor ConflictingInterceptorDeclaration =
        new(
            "PICO013",
            "Interceptor both globally declared and per-service excluded",
            "Interceptor '{0}' for service '{1}' appears in both global and per-service exclusion.",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

    public static readonly DiagnosticDescriptor AmbiguousInterceptBy =
        new(
            "PICO014",
            "InterceptBy<T>() follows multiple Register calls",
            "InterceptBy<{0}> follows multiple Register calls. Only the most recent is intercepted.",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true
        );
}
