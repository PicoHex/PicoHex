namespace PicoCfg.Gen;

// Defines the shipped diagnostics exposed by the source generator.
public sealed partial class PicoCfgBindGenerator
{
    private static class Diagnostics
    {
        public static readonly DiagnosticDescriptor TargetMustBeClosedNamedType = new(
            id: "PCFGGEN001",
            title: "Binding target must be a closed named type",
            messageFormat: "Direct closed named target type required; '{0}' is not supported",
            category: "PicoCfg.Gen",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        public static readonly DiagnosticDescriptor MissingPublicParameterlessConstructor = new(
            id: "PCFGGEN002",
            title: "Binding target must have a usable constructor",
            messageFormat: "Binding '{0}' requires a public parameterless constructor or a positional record primary constructor",
            category: "PicoCfg.Gen",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        public static readonly DiagnosticDescriptor UnsupportedComplexProperty = new(
            id: "PCFGGEN003",
            title: "Complex properties are not supported",
            messageFormat: "PicoCfg.Gen v1 does not support property '{0}.{1}' of complex type '{2}'; supported property types: scalars, List<T>/T[], Dictionary<string,T>, IReadOnlyList<T>/IReadOnlyCollection<T>/IEnumerable<T>, and nested classes",
            category: "PicoCfg.Gen",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        public static readonly DiagnosticDescriptor UnsupportedCollectionProperty = new(
            id: "PCFGGEN004",
            title: "Collection properties are not supported",
            messageFormat: "PicoCfg.Gen v1 does not support collection property '{0}.{1}' of type '{2}'",
            category: "PicoCfg.Gen",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        public static readonly DiagnosticDescriptor UnsupportedPropertyType = new(
            id: "PCFGGEN005",
            title: "Property type is not supported",
            messageFormat: "PicoCfg.Gen v1 does not support property '{0}.{1}' of type '{2}'",
            category: "PicoCfg.Gen",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        public static readonly DiagnosticDescriptor UnsupportedProperty = new(
            id: "PCFGGEN006",
            title: "Property shape is not supported",
            messageFormat: "PicoCfg.Gen v1 only supports {2}; '{0}.{1}' is not supported",
            category: "PicoCfg.Gen",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        public static readonly DiagnosticDescriptor TargetMustBeReferenceType = new(
            id: "PCFGGEN007",
            title: "Binding target must be a concrete class",
            messageFormat: "PicoCfg.Gen v1 only supports concrete class targets; '{0}' is not supported",
            category: "PicoCfg.Gen",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        public static readonly DiagnosticDescriptor CycleInNestedTypes = new(
            id: "PCFGGEN008",
            title: "Circular dependency detected in nested binding types",
            messageFormat: "Circular dependency detected: {0}",
            category: "PicoCfg.Gen",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        public static readonly DiagnosticDescriptor NestingTruncated = new(
            id: "PCFGGEN009",
            title: "Nesting depth limit reached",
            messageFormat: "Nesting depth limit of {0} reached; type '{1}' and its nested properties will not be bound",
            category: "PicoCfg.Gen",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true
        );

        public static readonly DiagnosticDescriptor UnsupportedCollectionElementType = new(
            id: "PCFGGEN010",
            title: "Collection element type is not supported",
            messageFormat: "PicoCfg.Gen v1 does not support collection element type '{2}' of property '{0}.{1}'; supported element types are scalars, nested classes, and supported collections (List<T>, T[], Dictionary<string,T>, IReadOnlyList<T>, IReadOnlyCollection<T>, IEnumerable<T>)",
            category: "PicoCfg.Gen",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );
    }
}
