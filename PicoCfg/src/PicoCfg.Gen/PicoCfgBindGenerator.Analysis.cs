namespace PicoCfg.Gen;

// Validates target shapes and translates symbols into generation models.
public sealed partial class PicoCfgBindGenerator
{
    private static bool TryAnalyzeTarget(
        SourceProductionContext context,
        TargetRegistration registration,
        IReadOnlyDictionary<ITypeSymbol, TargetRegistration> knownTargets,
        ISet<ITypeSymbol> truncatedTypes,
        out TargetModel model
    )
    {
        model = null!;

        if (registration.TargetType is not INamedTypeSymbol namedType)
        {
            ReportAll(
                context,
                GetTypeLevelLocations(registration, registration.TargetType),
                Diagnostics.TargetMustBeClosedNamedType,
                registration.TargetType.ToDisplayString()
            );
            return false;
        }

        if (ContainsTypeParameter(namedType))
        {
            ReportAll(
                context,
                GetTypeLevelLocations(registration, namedType),
                Diagnostics.TargetMustBeClosedNamedType,
                namedType.ToDisplayString()
            );
            return false;
        }

        if (namedType.TypeKind != TypeKind.Class || namedType.IsAbstract)
        {
            ReportAll(
                context,
                GetTypeLevelLocations(registration, namedType),
                Diagnostics.TargetMustBeReferenceType,
                namedType.ToDisplayString()
            );
            return false;
        }

        var isRecordClass = namedType.IsRecord && namedType.TypeKind == TypeKind.Class;
        var primaryConstructor = isRecordClass ? FindPrimaryConstructor(namedType) : null;
        var hasPrimaryCtor = primaryConstructor is not null;

        var properties = new List<PropertyModel>();
        foreach (var member in namedType.GetMembers())
        {
            if (member is not IPropertySymbol property)
                continue;

            if (property.IsStatic || property.DeclaredAccessibility != Accessibility.Public)
                continue;

            // A nested type that hit the nesting depth limit (PCFGGEN009) was never
            // expanded, so its nested property graph is unknown. Skip such properties
            // instead of generating Bind_-1 references (CS0103).
            if (ContainsTruncatedNestedType(property.Type, knownTargets, truncatedTypes))
            {
                ReportAll(
                    context,
                    registration.Locations.Concat([property.Locations[0]]),
                    Diagnostics.NestingTruncated,
                    NestingDepthLimit,
                    property.Type.ToDisplayString()
                );
                continue;
            }

            if (property.IsIndexer)
            {
                ReportAll(
                    context,
                    registration.Locations.Concat([property.Locations[0]]),
                    Diagnostics.UnsupportedProperty,
                    namedType.ToDisplayString(),
                    property.Name,
                    "public writable scalar properties"
                );
                continue;
            }

            var isSettable =
                property.SetMethod is { DeclaredAccessibility: Accessibility.Public } setter
                && !setter.IsStatic;
            var isInitOnly = isSettable && property.SetMethod!.IsInitOnly;
            var isWritable = isSettable || isInitOnly;
            var isRequired = property.IsRequired;

            if (!isWritable)
            {
                ReportAll(
                    context,
                    registration.Locations.Concat([property.Locations[0]]),
                    Diagnostics.UnsupportedProperty,
                    namedType.ToDisplayString(),
                    property.Name,
                    "public writable scalar properties"
                );
                continue;
            }

            // Silently skip unsupported complex property types — they are "code-only".
            if (IsSilentlySkippablePropertyType(property.Type))
                continue;

            if (
                !TryCreatePropertyModel(
                    context,
                    registration,
                    namedType,
                    property,
                    isRequired,
                    out var propertyModel
                )
            )
            {
                continue;
            }

            properties.Add(propertyModel);
        }

        var requiresCtor =
            (registration.Operations & (BindOperation.Bind | BindOperation.TryBind)) != 0;
        var hasPublicParameterlessCtor = HasPublicParameterlessConstructor(namedType);
        if (requiresCtor && !hasPublicParameterlessCtor && !hasPrimaryCtor)
        {
            ReportAll(
                context,
                GetTypeLevelLocations(registration, namedType),
                Diagnostics.MissingPublicParameterlessConstructor,
                namedType.ToDisplayString()
            );
            return false;
        }

        model = new TargetModel(
            namedType,
            registration.Operations,
            [.. properties],
            hasPublicParameterlessCtor,
            isRecordClass,
            hasPrimaryCtor,
            primaryConstructor
        );
        return true;
    }

    /// <summary>
    /// Locations for a type-level diagnostic: the explicit bind call sites when the
    /// type was requested directly, otherwise the type's own declaration (discovered
    /// nested types carry no call-site locations and would otherwise fail silently).
    /// </summary>
    private static IEnumerable<Location> GetTypeLevelLocations(
        TargetRegistration registration,
        ITypeSymbol type
    )
    {
        if (registration.Locations.Count > 0)
            return registration.Locations;

        return type.Locations.Length > 0 ? [type.Locations[0]] : [Location.None];
    }

    /// <summary>
    /// Finds a positional record's primary constructor: the instance constructor
    /// whose parameters match the record declaration's positional parameter list
    /// (names and order). Returns <see langword="null"/> for body-only records.
    /// </summary>
    private static IMethodSymbol? FindPrimaryConstructor(INamedTypeSymbol type)
    {
        var recordSyntax = type
            .DeclaringSyntaxReferences.Select(static r => r.GetSyntax())
            .OfType<RecordDeclarationSyntax>()
            .FirstOrDefault(static r => r.ParameterList is { Parameters.Count: > 0 });
        if (recordSyntax?.ParameterList is not { Parameters.Count: > 0 } parameterList)
            return null;

        var paramNames = parameterList
            .Parameters.Select(static p => p.Identifier.ValueText)
            .ToArray();

        foreach (var ctor in type.InstanceConstructors)
        {
            if (ctor.Parameters.Length != paramNames.Length)
                continue;

            var matches = true;
            for (var i = 0; i < paramNames.Length; i++)
            {
                if (
                    !string.Equals(ctor.Parameters[i].Name, paramNames[i], StringComparison.Ordinal)
                )
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
                return ctor;
        }

        return null;
    }

    /// <summary>
    /// True when <paramref name="type"/> is a nested class (or a collection whose
    /// element graph contains a nested class) that hit the nesting depth limit and
    /// was therefore never expanded by <see cref="DiscoverNestedTargets"/> — either
    /// because it sits at the truncation boundary or below it (in which case it was
    /// never even added to the known target set).
    /// </summary>
    private static bool ContainsTruncatedNestedType(
        ITypeSymbol type,
        IReadOnlyDictionary<ITypeSymbol, TargetRegistration> knownTargets,
        ISet<ITypeSymbol> truncatedTypes
    )
    {
        if (IsNestedBindableType(type))
            return truncatedTypes.Contains(type) || !knownTargets.ContainsKey(type);

        if (TryGetCollectionKind(type, out _, out var elementType) && elementType is not null)
            return ContainsTruncatedNestedType(elementType, knownTargets, truncatedTypes);

        return false;
    }

    private static bool TryCreatePropertyModel(
        SourceProductionContext context,
        TargetRegistration registration,
        INamedTypeSymbol namedType,
        IPropertySymbol property,
        bool isRequired,
        out PropertyModel propertyModel
    )
    {
        propertyModel = null!;
        if (TryGetScalarKind(property.Type, out var scalarKind, out var underlyingType))
        {
            propertyModel = new PropertyModel(
                property.Name,
                property.Type,
                scalarKind,
                underlyingType,
                IsNullable(property.Type),
                property.SetMethod?.IsInitOnly ?? false,
                isRequired: isRequired
            );
            return true;
        }

        if (TryGetCollectionKind(property.Type, out var collectionKind, out var elementType))
        {
            // Nested collection element types (e.g. Dictionary<string, Dictionary<string, string>>,
            // List<List<int>>) bind natively — the generator recurses through the full
            // element graph. Only truly unbindable elements (structs, unsupported
            // collection implementations like HashSet<T>) are rejected with PCFGGEN010.
            if (elementType is not null && !IsSupportedCollectionElementType(elementType))
            {
                ReportAll(
                    context,
                    registration.Locations.Concat([property.Locations[0]]),
                    Diagnostics.UnsupportedCollectionElementType,
                    namedType.ToDisplayString(),
                    property.Name,
                    elementType.ToDisplayString()
                );
                return false;
            }

            propertyModel = new PropertyModel(
                property.Name,
                property.Type,
                collectionKind,
                property.Type,
                IsNullable(property.Type),
                property.SetMethod?.IsInitOnly ?? false,
                elementBinding: elementType is null ? null : BuildElementBinding(elementType),
                isRequired: isRequired
            );
            return true;
        }

        if (IsCollectionType(property.Type))
        {
            ReportAll(
                context,
                registration.Locations.Concat([property.Locations[0]]),
                Diagnostics.UnsupportedCollectionProperty,
                namedType.ToDisplayString(),
                property.Name,
                property.Type.ToDisplayString()
            );
            return false;
        }

        if (IsNestedBindableType(property.Type))
        {
            // Self-reference guard — prevent runtime infinite recursion when a
            // type declares a property of its own type (e.g. class A { A? Self })
            if (SymbolEqualityComparer.Default.Equals(property.Type, namedType))
            {
                ReportAll(
                    context,
                    registration.Locations.Concat([property.Locations[0]]),
                    Diagnostics.UnsupportedProperty,
                    namedType.ToDisplayString(),
                    property.Name,
                    "non-self-referencing class types"
                );
                return false;
            }

            propertyModel = new PropertyModel(
                property.Name,
                property.Type,
                ScalarKind.Nested,
                property.Type,
                IsNullable(property.Type),
                property.SetMethod?.IsInitOnly ?? false,
                nestedType: (INamedTypeSymbol)property.Type,
                isRequired: isRequired
            );
            return true;
        }

        if (
            property.Type is INamedTypeSymbol
            {
                TypeKind: TypeKind.Class or TypeKind.Struct or TypeKind.Interface
            } complexType
        )
        {
            ReportAll(
                context,
                registration.Locations.Concat([property.Locations[0]]),
                Diagnostics.UnsupportedComplexProperty,
                namedType.ToDisplayString(),
                property.Name,
                complexType.ToDisplayString()
            );
            return false;
        }

        if (property.Type.TypeKind == TypeKind.Delegate)
        {
            ReportAll(
                context,
                registration.Locations.Concat([property.Locations[0]]),
                Diagnostics.UnsupportedPropertyType,
                namedType.ToDisplayString(),
                property.Name,
                property.Type.ToDisplayString()
            );
            return false;
        }

        ReportAll(
            context,
            registration.Locations.Concat([property.Locations[0]]),
            Diagnostics.UnsupportedPropertyType,
            namedType.ToDisplayString(),
            property.Name,
            property.Type.ToDisplayString()
        );
        return false;
    }

    private static bool TryGetScalarKind(
        ITypeSymbol type,
        out ScalarKind scalarKind,
        out ITypeSymbol underlyingType
    )
    {
        underlyingType = type;
        if (
            type is INamedTypeSymbol
            {
                OriginalDefinition.SpecialType: SpecialType.System_Nullable_T
            } namedType
        )
            underlyingType = namedType.TypeArguments[0];

        if (underlyingType.SpecialType == SpecialType.System_String)
        {
            scalarKind = ScalarKind.String;
            return true;
        }

        if (underlyingType.TypeKind == TypeKind.Enum)
        {
            scalarKind = ScalarKind.Enum;
            return true;
        }

        switch (underlyingType.SpecialType)
        {
            case SpecialType.System_Boolean:
                scalarKind = ScalarKind.Boolean;
                return true;
            case SpecialType.System_Byte:
                scalarKind = ScalarKind.Byte;
                return true;
            case SpecialType.System_SByte:
                scalarKind = ScalarKind.SByte;
                return true;
            case SpecialType.System_Int16:
                scalarKind = ScalarKind.Int16;
                return true;
            case SpecialType.System_UInt16:
                scalarKind = ScalarKind.UInt16;
                return true;
            case SpecialType.System_Int32:
                scalarKind = ScalarKind.Int32;
                return true;
            case SpecialType.System_UInt32:
                scalarKind = ScalarKind.UInt32;
                return true;
            case SpecialType.System_Int64:
                scalarKind = ScalarKind.Int64;
                return true;
            case SpecialType.System_UInt64:
                scalarKind = ScalarKind.UInt64;
                return true;
            case SpecialType.System_Single:
                scalarKind = ScalarKind.Single;
                return true;
            case SpecialType.System_Double:
                scalarKind = ScalarKind.Double;
                return true;
            case SpecialType.System_Decimal:
                scalarKind = ScalarKind.Decimal;
                return true;
            case SpecialType.System_DateTime:
                scalarKind = ScalarKind.DateTime;
                return true;
        }

        if (
            underlyingType is INamedTypeSymbol { Name: nameof(Guid) } namedGuidType
            && namedGuidType.ContainingNamespace.ToDisplayString() == "System"
        )
        {
            scalarKind = ScalarKind.Guid;
            return true;
        }

        if (
            underlyingType is INamedTypeSymbol { Name: nameof(DateTimeOffset) } dtoType
            && dtoType.ContainingNamespace.ToDisplayString() == "System"
        )
        {
            scalarKind = ScalarKind.DateTimeOffset;
            return true;
        }

        if (
            underlyingType is INamedTypeSymbol { Name: "DateOnly" } doType
            && doType.ContainingNamespace.ToDisplayString() == "System"
        )
        {
            scalarKind = ScalarKind.DateOnly;
            return true;
        }

        if (
            underlyingType is INamedTypeSymbol { Name: "TimeOnly" } toType
            && toType.ContainingNamespace.ToDisplayString() == "System"
        )
        {
            scalarKind = ScalarKind.TimeOnly;
            return true;
        }

        if (
            underlyingType is INamedTypeSymbol { Name: "TimeSpan" } tsType
            && tsType.ContainingNamespace.ToDisplayString() == "System"
        )
        {
            scalarKind = ScalarKind.TimeSpan;
            return true;
        }

        if (
            underlyingType is INamedTypeSymbol { Name: nameof(Uri) } uriType
            && uriType.ContainingNamespace.ToDisplayString() == "System"
        )
        {
            scalarKind = ScalarKind.Uri;
            return true;
        }

        if (
            underlyingType is INamedTypeSymbol { Name: nameof(Version) } verType
            && verType.ContainingNamespace.ToDisplayString() == "System"
        )
        {
            scalarKind = ScalarKind.Version;
            return true;
        }

        if (
            underlyingType is INamedTypeSymbol { Name: "BigInteger" } biType
            && biType.ContainingNamespace.ToDisplayString() == "System.Numerics"
        )
        {
            scalarKind = ScalarKind.BigInteger;
            return true;
        }

        scalarKind = default;
        return false;
    }

    private static bool IsNullable(ITypeSymbol type) =>
        type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T };

    private static bool ContainsTypeParameter(ITypeSymbol type)
    {
        while (true)
        {
            if (type.TypeKind == TypeKind.TypeParameter)
                return true;
            switch (type)
            {
                case IArrayTypeSymbol arrayType:
                    type = arrayType.ElementType;
                    continue;
                case INamedTypeSymbol { IsUnboundGenericType: true }:
                    return true;
                case INamedTypeSymbol namedType:
                {
                    if (Enumerable.Any(namedType.TypeArguments, ContainsTypeParameter))
                    {
                        return true;
                    }

                    break;
                }
            }

            return false;
        }
    }

    private static bool IsCollectionType(ITypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_String)
            return false;
        if (type is IArrayTypeSymbol)
            return true;
        if (type is not INamedTypeSymbol named)
            return false;

        foreach (var iface in named.AllInterfaces)
        {
            if (
                iface.OriginalDefinition.SpecialType
                    == SpecialType.System_Collections_Generic_ICollection_T
                || iface.OriginalDefinition.SpecialType
                    == SpecialType.System_Collections_Generic_IList_T
            )
            {
                if (named.TypeKind is TypeKind.Class or TypeKind.Interface)
                    return true;
            }
        }

        // Non-generic collection interfaces (IEnumerable, ICollection, IList) are
        // collections too — report them as unsupported collections, not "complex types".
        if (
            named.TypeArguments.Length == 0
            && named.OriginalDefinition.Name is "IEnumerable" or "ICollection" or "IList"
            && named.OriginalDefinition.ContainingNamespace.ToDisplayString()
                == "System.Collections"
        )
            return true;

        if (
            named.TypeArguments.Length == 2
            && named.OriginalDefinition.Name == "Dictionary"
            && named.OriginalDefinition.ContainingNamespace.ToDisplayString()
                == "System.Collections.Generic"
        )
            return true;

        return false;
    }

    private static bool TryGetCollectionKind(
        ITypeSymbol type,
        out ScalarKind scalarKind,
        out ITypeSymbol? elementType
    )
    {
        elementType = null;
        scalarKind = default;

        if (type is IArrayTypeSymbol arrayType)
        {
            scalarKind = ScalarKind.Collection_Array;
            elementType = arrayType.ElementType;
            return true;
        }

        if (type is not INamedTypeSymbol named)
            return false;

        if (
            named.TypeArguments.Length == 1
            && named.OriginalDefinition.Name == "List"
            && named.OriginalDefinition.ContainingNamespace.ToDisplayString()
                == "System.Collections.Generic"
        )
        {
            scalarKind = ScalarKind.Collection_List;
            elementType = named.TypeArguments[0];
            return true;
        }

        // Read-only collection interfaces bind like List<T>: the generated code
        // accumulates into a List<T> and assigns to the interface property.
        if (
            named.TypeArguments.Length == 1
            && named.OriginalDefinition.Name
                is "IReadOnlyList"
                    or "IReadOnlyCollection"
                    or "IEnumerable"
            && named.OriginalDefinition.ContainingNamespace.ToDisplayString()
                == "System.Collections.Generic"
        )
        {
            scalarKind = ScalarKind.Collection_List;
            elementType = named.TypeArguments[0];
            return true;
        }

        if (
            named.TypeArguments.Length == 2
            && named.OriginalDefinition.Name == "Dictionary"
            && named.OriginalDefinition.ContainingNamespace.ToDisplayString()
                == "System.Collections.Generic"
        )
        {
            scalarKind = ScalarKind.Collection_Dictionary;
            elementType = named.TypeArguments[1];
            return true;
        }

        return false;
    }

    private static bool IsSupportedCollectionElementType(ITypeSymbol type)
    {
        if (TryGetScalarKind(type, out _, out _))
            return true;
        if (IsNestedBindableType(type))
            return true;
        if (TryGetCollectionKind(type, out _, out var elementType))
            return elementType is not null && IsSupportedCollectionElementType(elementType);
        return false;
    }

    private static ElementBindingModel? BuildElementBinding(ITypeSymbol elementType)
    {
        if (TryGetScalarKind(elementType, out var scalarKind, out var underlyingType))
            return new ElementBindingModel(scalarKind, underlyingType);

        if (IsNestedBindableType(elementType))
            return new ElementBindingModel(ScalarKind.Nested, elementType);

        if (TryGetCollectionKind(elementType, out var collectionKind, out var nestedElement))
        {
            return new ElementBindingModel(
                collectionKind,
                elementType,
                nestedElement is null ? null : BuildElementBinding(nestedElement)
            );
        }

        return null;
    }

    private static bool IsSilentlySkippablePropertyType(ITypeSymbol type)
    {
        // Only delegates are always code-only — skip silently.
        // All other types go through the normal binding path and either bind or generate a diagnostic.
        return type.TypeKind is TypeKind.Delegate;
    }

    private static bool IsNestedBindableType(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol namedType)
            return false;
        if (namedType.TypeKind != TypeKind.Class)
            return false;
        if (namedType.SpecialType == SpecialType.System_String)
            return false;
        if (IsCollectionType(namedType))
            return false;
        if (TryGetScalarKind(namedType, out _, out _))
            return false;
        return true;
    }

    private static bool HasPublicParameterlessConstructor(INamedTypeSymbol type) =>
        type.InstanceConstructors.Any(ctor =>
            ctor.DeclaredAccessibility == Accessibility.Public && ctor.Parameters.Length == 0
        );

    private static void ReportAll(
        SourceProductionContext context,
        IEnumerable<Location> locations,
        DiagnosticDescriptor descriptor,
        params object[] args
    )
    {
        foreach (var location in locations)
            context.ReportDiagnostic(Diagnostic.Create(descriptor, location, args));
    }
}
