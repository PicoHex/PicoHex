namespace PicoDI.Gen.OpenGeneric;

/// <summary>
/// Orchestrates the open generic closure process by coordinating the scanner, collector, analyzer, substitutor, and generator.
/// </summary>
internal sealed class OpenGenericClosureOrchestrator
{
    private readonly OpenGenericScanner _scanner;
    private readonly ClosedGenericCollector _collector;
    private readonly ClosedGenericAnalyzer _analyzer;
    private readonly ClosedGenericGenerator _generator;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenGenericClosureOrchestrator"/> class.
    /// </summary>
    /// <param name="scanner">The open generic scanner.</param>
    /// <param name="collector">The closed generic collector.</param>
    /// <param name="analyzer">The closed generic analyzer.</param>
    /// <param name="generator">The closed generic generator.</param>
    public OpenGenericClosureOrchestrator(
        OpenGenericScanner scanner,
        ClosedGenericCollector collector,
        ClosedGenericAnalyzer analyzer,
        ClosedGenericGenerator generator
    )
    {
        _scanner = scanner;
        _collector = collector;
        _analyzer = analyzer;
        _generator = generator;
    }

    /// <summary>
    /// Scans for open generic registration invocations in the source code.
    /// </summary>
    /// <param name="context">The generator syntax context.</param>
    /// <returns>An <see cref="OpenGenericInvocationCandidate"/> if found, otherwise null.</returns>
    public OpenGenericInvocationCandidate? ScanOpenGenericInvocation(GeneratorSyntaxContext context)
    {
        return _scanner.ScanOpenGenericInvocation(context);
    }

    /// <summary>
    /// Analyzes an open generic invocation to extract registration information.
    /// </summary>
    /// <param name="invocation">The invocation expression syntax.</param>
    /// <param name="semanticModel">The semantic model.</param>
    /// <returns>An <see cref="OpenGenericSemanticOutcome"/> containing registration and diagnostic information.</returns>
    public OpenGenericSemanticOutcome AnalyzeOpenGenericInvocation(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel
    )
    {
        return _scanner.AnalyzeOpenGenericInvocation(invocation, semanticModel);
    }

    /// <summary>
    /// Gets closed generic type symbols from generic type declarations.
    /// </summary>
    /// <param name="context">The generator syntax context.</param>
    /// <returns>A closed generic type symbol if found, otherwise null.</returns>
    public ITypeSymbol? GetClosedGenericFromDeclaration(GeneratorSyntaxContext context)
    {
        return _collector.GetClosedGenericFromDeclaration(context);
    }

    /// <summary>
    /// Gets closed generic type symbols from GetService/GetServices invocation usages.
    /// </summary>
    /// <param name="context">The generator syntax context.</param>
    /// <returns>A closed generic type symbol if found, otherwise null.</returns>
    public ITypeSymbol? GetClosedGenericUsageInfo(GeneratorSyntaxContext context)
    {
        return _collector.GetClosedGenericUsageInfo(context);
    }

    /// <summary>
    /// Gets closed generic type symbols from constructor parameters.
    /// </summary>
    /// <param name="context">The generator syntax context.</param>
    /// <returns>A collection of closed generic type symbols found in constructor parameters.</returns>
    public IEnumerable<ITypeSymbol> GetClosedGenericsFromConstructor(GeneratorSyntaxContext context)
    {
        return _collector.GetClosedGenericsFromConstructor(context);
    }

    /// <summary>
    /// Collects closed generic usages from multiple sources.
    /// </summary>
    /// <param name="closedGenericUsages">Closed generic usages from GetService/GetServices invocations.</param>
    /// <param name="closedGenericDeclarations">Closed generic declarations from type declarations.</param>
    /// <param name="ctorClosedGenerics">Closed generics from constructor parameters.</param>
    /// <param name="registrations">Existing service registrations to scan for constructor dependencies.</param>
    /// <returns>A distinct list of closed generic usages.</returns>
    public List<ClosedGenericUsage> CollectClosedGenericUsages(
        ImmutableArray<ITypeSymbol?> closedGenericUsages,
        ImmutableArray<ITypeSymbol?> closedGenericDeclarations,
        ImmutableArray<ITypeSymbol> ctorClosedGenerics,
        List<ServiceRegistration> registrations
    )
    {
        return _analyzer.CollectClosedGenericUsages(
            closedGenericUsages,
            closedGenericDeclarations,
            ctorClosedGenerics,
            registrations
        );
    }

    /// <summary>
    /// Generates closed generic service registrations by combining open generic registrations with closed generic usages.
    /// </summary>
    /// <param name="openGenerics">Open generic registrations discovered in the codebase.</param>
    /// <param name="closedUsages">Closed generic type usages found in the codebase.</param>
    /// <returns>A list of service registrations for closed generic types.</returns>
    public List<ServiceRegistration> GenerateClosedGenericRegistrations(
        List<OpenGenericRegistration> openGenerics,
        List<ClosedGenericUsage> closedUsages
    )
    {
        return _generator.GenerateClosedGenericRegistrations(openGenerics, closedUsages);
    }

    /// <summary>
    /// Analyzes a closed generic type symbol and converts it to a <see cref="ClosedGenericUsage"/> model.
    /// </summary>
    /// <param name="closedType">The closed generic type symbol to analyze.</param>
    /// <returns>A <see cref="ClosedGenericUsage"/> model if the type is a valid closed generic, otherwise null.</returns>
    public ClosedGenericUsage? AnalyzeClosedGenericUsage(ITypeSymbol closedType)
    {
        return _analyzer.AnalyzeClosedGenericUsage(closedType);
    }
}
