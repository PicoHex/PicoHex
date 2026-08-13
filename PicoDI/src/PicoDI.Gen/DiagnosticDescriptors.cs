namespace PicoDI.Gen;

internal static class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor CircularDependency = new(
        "PICO002",
        "Circular dependency detected",
        "Circular dependency detected at compile-time: {0}",
        PicoDiNames.RootNamespace,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A circular dependency chain was detected which will cause a runtime exception. Fix the dependency cycle."
    );

    public static readonly DiagnosticDescriptor AbstractTypeRegistration = new(
        "PICO003",
        "Abstract type registration",
        "Cannot register abstract type or interface '{0}' as implementation",
        PicoDiNames.RootNamespace,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Abstract types and interfaces cannot be instantiated. Provide a concrete implementation type."
    );

    public static readonly DiagnosticDescriptor MissingPublicConstructor = new(
        "PICO004",
        "Missing public constructor",
        "Type '{0}' has no public constructor",
        PicoDiNames.RootNamespace,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The implementation type must have at least one public constructor for dependency injection."
    );

    public static readonly DiagnosticDescriptor MultipleMarkedConstructors = new(
        "PICO005",
        "Multiple preferred constructors",
        "Type '{0}' has multiple public constructors marked with [SvcConstructor]",
        PicoDiNames.RootNamespace,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Only one public constructor can be marked with [SvcConstructor] for PicoDI source-generated activation."
    );

    public static readonly DiagnosticDescriptor FactoryInterceptionUnsupported = new(
        "PICO006",
        "Factory-based registration cannot be intercepted",
        "Interception of '{0}' requires a type-based registration (Register<TService, TImplementation>()); "
            + "factory-based registrations cannot be intercepted at compile time",
        PicoDiNames.RootNamespace,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "PicoAop interception needs the implementation type to generate the wrapper. Use a type-based registration instead of a factory delegate."
    );
}
