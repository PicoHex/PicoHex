namespace PicoDI.Gen;

/// <summary>
/// Represents a service registration found in the source code.
/// </summary>
internal record ServiceRegistration(
    string ServiceTypeName,
    string ServiceTypeFullName,
    string ImplementationTypeName,
    string ImplementationTypeFullName,
    string Lifetime,
    bool HasFactory,
    ImmutableArray<string> ConstructorParameters
);

/// <summary>
/// Represents an open generic registration (e.g., IRepository&lt;&gt; -&gt; Repository&lt;&gt;).
/// </summary>
internal record OpenGenericRegistration(
    string OpenServiceTypeFullName,
    string OpenImplementationTypeFullName,
    string Lifetime,
    int TypeParameterCount,
    ImmutableArray<string> TypeParameterNames,
    ImmutableArray<string> ConstructorParameters,
    ImmutableArray<ITypeSymbol> ConstructorParameterTypeSymbols
);

/// <summary>
/// Represents a closed generic type usage that needs to be pre-generated for AOT.
/// </summary>
internal record ClosedGenericUsage(
    string ClosedServiceTypeFullName,
    string OpenServiceTypeFullName,
    ImmutableArray<string> TypeArgumentsFullNames,
    ImmutableArray<ITypeSymbol> TypeArgumentSymbols
);
