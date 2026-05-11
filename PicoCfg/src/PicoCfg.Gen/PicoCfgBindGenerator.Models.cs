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
        bool hasPrimaryConstructor
    )
    {
        public INamedTypeSymbol TargetType { get; } = targetType;
        public BindOperation Operations { get; } = operations;
        public ImmutableArray<PropertyModel> Properties { get; } = properties;
        public bool HasPublicParameterlessConstructor { get; } = hasPublicParameterlessConstructor;
        public bool IsRecordClass { get; } = isRecordClass;
        public bool HasPrimaryConstructor { get; } = hasPrimaryConstructor;
    }

    private sealed class PropertyModel(
        string name,
        ITypeSymbol type,
        ScalarKind scalarKind,
        ITypeSymbol underlyingType,
        bool isNullable,
        bool requiresInitializerSyntax,
        INamedTypeSymbol? nestedType = null,
        ITypeSymbol? elementType = null,
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
        public int CollectionElementNestedIndex { get; set; } = -1;
        public ITypeSymbol? ElementType { get; } = elementType;
        public bool IsRequired { get; } = isRequired;
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
