namespace PicoDI.Gen;

internal static class RegistrationPlanBuilder
{
    public static ServiceRegistrationGenerationPlan Build(
        RegistrationSemanticBatch normalizedRegistrations,
        ImmutableArray<OpenGenericInvocationCandidate?> openGenericInvocations,
        ImmutableArray<ITypeSymbol?> closedGenericUsages,
        ImmutableArray<ITypeSymbol?> closedGenericDeclarations,
        ImmutableArray<ITypeSymbol> ctorClosedGenerics,
        Compilation compilation
    )
    {
        var registrations = normalizedRegistrations.Registrations.ToList();
        var openGenericBatch = CollectOpenGenericRegistrations(openGenericInvocations);
        var openGenerics = openGenericBatch.OpenGenerics.ToList();
        var closedUsages = ClosedGenericAnalyzer.Default.CollectClosedGenericUsages(
            closedGenericUsages,
            closedGenericDeclarations,
            ctorClosedGenerics,
            registrations
        );

        var discoveredOpenGenerics =
            OpenGenericMetadataContract.DiscoverOpenGenericsFromReferencedAssemblies(
                compilation,
                openGenerics
            );
        MergeDistinct(openGenerics, discoveredOpenGenerics);

        var generatedClosedGenerics = new ClosedGenericGenerator(
                new TypeParameterSubstitutor()
            )
            .GenerateClosedGenericRegistrations(openGenerics, closedUsages);
        var allRegistrations = registrations
            .Concat(generatedClosedGenerics)
            .Distinct()
            .ToImmutableArray();

        return new ServiceRegistrationGenerationPlan(
            allRegistrations,
            [.. openGenerics],
            openGenericBatch.Diagnostics
        );
    }

    private static OpenGenericSemanticBatch CollectOpenGenericRegistrations(
        ImmutableArray<OpenGenericInvocationCandidate?> openGenericInvocations
    )
    {
        var registrations = new List<OpenGenericRegistration>();
        var diagnostics = new List<Diagnostic>();
        var seenRegistrations = new HashSet<OpenGenericRegistration>();
        var seenDiagnostics = new HashSet<Diagnostic>(
            OpenGenericDiagnosticIdentityComparer.Instance
        );

        foreach (var openGenericInvocation in openGenericInvocations)
        {
            if (openGenericInvocation is not { } candidate)
                continue;

            var outcome = OpenGenericScanner.Default.AnalyzeOpenGenericInvocation(
                candidate.Invocation,
                candidate.SemanticModel
            );

            if (outcome.Registration is { } registration && seenRegistrations.Add(registration))
                registrations.Add(registration);

            if (outcome.Diagnostic is { } diagnostic && seenDiagnostics.Add(diagnostic))
                diagnostics.Add(diagnostic);
        }

        return new OpenGenericSemanticBatch([.. registrations], [.. diagnostics]);
    }

    private static void MergeDistinct<T>(List<T> target, IEnumerable<T> source)
    {
        foreach (var item in source)
        {
            if (!target.Contains(item))
                target.Add(item);
        }
    }

    private sealed class OpenGenericDiagnosticIdentityComparer : IEqualityComparer<Diagnostic>
    {
        public static readonly OpenGenericDiagnosticIdentityComparer Instance = new();

        public bool Equals(Diagnostic? x, Diagnostic? y)
        {
            if (ReferenceEquals(x, y))
                return true;

            if (x is null || y is null)
                return false;

            return x.Id == y.Id
                && x.Location.SourceSpan.Equals(y.Location.SourceSpan)
                && string.Equals(x.GetMessage(), y.GetMessage(), StringComparison.Ordinal);
        }

        public int GetHashCode(Diagnostic obj)
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 23) + obj.Id.GetHashCode();
                hash = (hash * 23) + obj.Location.SourceSpan.GetHashCode();
                hash = (hash * 23) + obj.GetMessage().GetHashCode();
                return hash;
            }
        }
    }
}
