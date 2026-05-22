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

// ── Pipeline Models ──

internal readonly record struct RegistrationInvocationCandidate(
    InvocationExpressionSyntax Invocation,
    SemanticModel SemanticModel
);

internal readonly record struct OpenGenericInvocationCandidate(
    InvocationExpressionSyntax Invocation,
    SemanticModel SemanticModel
);

internal readonly record struct OpenGenericSemanticOutcome(
    OpenGenericRegistration? Registration,
    Diagnostic? Diagnostic
);

internal readonly record struct RegistrationSemanticOutcome(
    ServiceRegistration? Registration,
    Diagnostic? Diagnostic
);

internal sealed record RegistrationSemanticBatch(
    ImmutableArray<ServiceRegistration> Registrations,
    ImmutableArray<Diagnostic> Diagnostics
);

internal sealed record OpenGenericSemanticBatch(
    ImmutableArray<OpenGenericRegistration> OpenGenerics,
    ImmutableArray<Diagnostic> Diagnostics
);

internal sealed record ServiceRegistrationGenerationPlan(
    ImmutableArray<ServiceRegistration> Registrations,
    ImmutableArray<OpenGenericRegistration> OpenGenerics,
    ImmutableArray<Diagnostic> Diagnostics
)
{
    public bool HasSourcesToEmit =>
        !Registrations.IsDefaultOrEmpty || !OpenGenerics.IsDefaultOrEmpty;
}
