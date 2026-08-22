namespace PicoCfg.Gen;

// Holds the internal models shared across discovery, analysis, and rendering.
public sealed partial class PicoCfgBindGenerator
{
    private sealed class TargetRegistration(ITypeSymbol targetType)
    {
        public ITypeSymbol TargetType { get; } = targetType;
        public BindOperation Operations { get; set; }
        public ImmutableArray<Location>.Builder Locations { get; } =
            ImmutableArray.CreateBuilder<Location>();
    }

    private sealed class BindCall(
        ITypeSymbol targetType,
        BindOperation operation,
        Location location
    )
    {
        public ITypeSymbol TargetType { get; } = targetType;
        public BindOperation Operation { get; } = operation;
        public Location Location { get; } = location;
    }

    private sealed class TargetModel(
        INamedTypeSymbol targetType,
        BindOperation operations,
        ImmutableArray<PropertyModel> properties,
        bool hasPublicParameterlessConstructor,
        bool isRecordClass,
        bool hasPrimaryConstructor,
        IMethodSymbol? primaryConstructor = null
    )
    {
        public INamedTypeSymbol TargetType { get; } = targetType;
        public BindOperation Operations { get; } = operations;
        public ImmutableArray<PropertyModel> Properties { get; } = properties;
        public bool HasPublicParameterlessConstructor { get; } = hasPublicParameterlessConstructor;
        public bool IsRecordClass { get; } = isRecordClass;
        public bool HasPrimaryConstructor { get; } = hasPrimaryConstructor;

        /// <summary>
        /// The primary constructor of a positional record (e.g. <c>record R(int A)</c>).
        /// <see langword="null" /> for classes and body-only records.
        /// </summary>
        public IMethodSymbol? PrimaryConstructor { get; } = primaryConstructor;
    }

    private sealed class PropertyModel(
        string name,
        ITypeSymbol type,
        ScalarKind scalarKind,
        ITypeSymbol underlyingType,
        bool isNullable,
        bool requiresInitializerSyntax,
        INamedTypeSymbol? nestedType = null,
        ElementBindingModel? elementBinding = null,
        bool isRequired = false
    )
    {
        public string Name { get; } = name;
        public ITypeSymbol Type { get; } = type;
        public ScalarKind ScalarKind { get; } = scalarKind;
        public ITypeSymbol UnderlyingType { get; } = underlyingType;
        public bool IsNullable { get; } = isNullable;
        public bool RequiresInitializerSyntax { get; } = requiresInitializerSyntax;
        public INamedTypeSymbol? NestedType { get; } = nestedType;
        public int NestedModelIndex { get; set; } = -1;
        public ElementBindingModel? ElementBinding { get; } = elementBinding;
        public bool IsRequired { get; } = isRequired;
    }

    /// <summary>
    /// Recursive description of a collection element. An element is either a
    /// scalar (<see cref="Kind"/> is a scalar kind), a nested class
    /// (<see cref="Kind"/> is <see cref="ScalarKind.Nested"/>), or another
    /// collection (<see cref="Kind"/> is a collection kind and
    /// <see cref="Element"/> describes its own element).
    /// </summary>
    private sealed class ElementBindingModel(
        ScalarKind kind,
        ITypeSymbol type,
        ElementBindingModel? element = null
    )
    {
        /// <summary>Scalar → underlying (non-nullable) type; nested → the class; collection → the collection type itself.</summary>
        public ScalarKind Kind { get; } = kind;
        public ITypeSymbol Type { get; } = type;
        public int NestedModelIndex { get; set; } = -1;
        public ElementBindingModel? Element { get; } = element;
    }

    [Flags]
    private enum BindOperation
    {
        None = 0,
        Bind = 1,
        TryBind = 2,
        BindInto = 4,
    }

    private enum ScalarKind
    {
        String,
        Boolean,
        Byte,
        SByte,
        Int16,
        UInt16,
        Int32,
        UInt32,
        Int64,
        UInt64,
        Single,
        Double,
        Decimal,
        Guid,
        Enum,
        DateTime,
        DateTimeOffset,
        DateOnly,
        TimeOnly,
        TimeSpan,
        Uri,
        Version,
        BigInteger,
        Nested,
        Collection_List,
        Collection_Array,
        Collection_Dictionary,
    }
}
