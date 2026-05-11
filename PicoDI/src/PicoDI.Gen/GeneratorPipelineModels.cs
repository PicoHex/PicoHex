namespace PicoDI.Gen;

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
