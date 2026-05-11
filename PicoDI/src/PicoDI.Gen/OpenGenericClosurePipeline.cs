namespace PicoDI.Gen;

internal static class OpenGenericClosurePipeline
{
    private static OpenGenericClosureOrchestrator? _defaultOrchestrator;

    private static OpenGenericClosureOrchestrator DefaultOrchestrator =>
        _defaultOrchestrator ??= OpenGenericClosureFactory.DefaultOrchestrator;

    public static IEnumerable<ITypeSymbol> GetClosedGenericsFromConstructor(
        GeneratorSyntaxContext context
    ) => DefaultOrchestrator.GetClosedGenericsFromConstructor(context);

    public static ITypeSymbol? GetClosedGenericFromDeclaration(GeneratorSyntaxContext context) =>
        DefaultOrchestrator.GetClosedGenericFromDeclaration(context);

    public static OpenGenericInvocationCandidate? GetOpenGenericInvocationInfo(
        GeneratorSyntaxContext context
    ) => DefaultOrchestrator.ScanOpenGenericInvocation(context);

    public static ITypeSymbol? GetClosedGenericUsageInfo(GeneratorSyntaxContext context) =>
        DefaultOrchestrator.GetClosedGenericUsageInfo(context);

    public static List<ClosedGenericUsage> CollectClosedGenericUsages(
        ImmutableArray<ITypeSymbol?> closedGenericUsages,
        ImmutableArray<ITypeSymbol?> closedGenericDeclarations,
        ImmutableArray<ITypeSymbol> ctorClosedGenerics,
        List<ServiceRegistration> registrations
    ) =>
        DefaultOrchestrator.CollectClosedGenericUsages(
            closedGenericUsages,
            closedGenericDeclarations,
            ctorClosedGenerics,
            registrations
        );

    public static OpenGenericSemanticOutcome AnalyzeOpenGenericInvocation(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel
    ) => DefaultOrchestrator.AnalyzeOpenGenericInvocation(invocation, semanticModel);

    public static ClosedGenericUsage? AnalyzeClosedGenericUsage(ITypeSymbol closedType)
    {
        // Delegate to the orchestrator's analyzer
        return DefaultOrchestrator.AnalyzeClosedGenericUsage(closedType);
    }

    public static List<ServiceRegistration> GenerateClosedGenericRegistrations(
        List<OpenGenericRegistration> openGenerics,
        List<ClosedGenericUsage> closedUsages
    ) => DefaultOrchestrator.GenerateClosedGenericRegistrations(openGenerics, closedUsages);
}
