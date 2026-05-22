namespace PicoDI.Gen;

/// <summary>
/// Source Generator that scans all ISvcContainer.Register* method calls
/// and generates AOT-compatible factory methods at compile time.
/// Also handles open generic registrations by detecting closed generic usages.
/// </summary>
[Generator(LanguageNames.CSharp)]
public partial class ServiceRegistrationGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var registerInvocations = context
            .SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) =>
                    RegistrationSyntaxPipeline.IsRegisterMethodInvocation(node),
                transform: static (ctx, _) => RegistrationSemanticPipeline.GetInvocationInfo(ctx)
            )
            .Where(static x => x is not null);

        var openGenericRegistrations = context
            .SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) =>
                    RegistrationSyntaxPipeline.IsOpenGenericRegisterInvocation(node),
                transform: static (ctx, _) =>
                    OpenGenericScanner.Default.ScanOpenGenericInvocation(ctx)
            )
            .Where(static x => x is not null);

        var closedGenericUsages = context
            .SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) =>
                    RegistrationSyntaxPipeline.IsGetServiceInvocation(node),
                transform: static (ctx, _) =>
                    ClosedGenericCollector.Default.GetClosedGenericUsageInfo(ctx)
            )
            .Where(static x => x is not null);

        var closedGenericDeclarations = context
            .SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) =>
                    RegistrationSyntaxPipeline.IsClosedGenericTypeDeclaration(node),
                transform: static (ctx, _) =>
                    ClosedGenericCollector.Default.GetClosedGenericFromDeclaration(ctx)
            )
            .Where(static x => x is not null);

        var closedGenericCtorParams = context
            .SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) =>
                    RegistrationSyntaxPipeline.IsConstructorWithGenericParameter(node),
                transform: static (ctx, _) =>
                    ClosedGenericCollector.Default.GetClosedGenericsFromConstructor(ctx)
            )
            .Where(static x => x is not null)
            .SelectMany(static (x, _) => x);

        var combinedSources = registerInvocations
            .Collect()
            .Combine(openGenericRegistrations.Collect())
            .Combine(closedGenericUsages.Collect())
            .Combine(closedGenericDeclarations.Collect())
            .Combine(closedGenericCtorParams.Collect())
            .Combine(context.CompilationProvider);

        context.RegisterSourceOutput(
            combinedSources,
            static (spc, source) =>
            {
                var (
                    (
                        (((invocations, openGenerics), closedUsages), closedDeclarations),
                        ctorClosedGenerics
                    ),
                    compilation
                ) = source;

                Execute(
                    invocations,
                    openGenerics,
                    closedUsages,
                    closedDeclarations,
                    ctorClosedGenerics,
                    compilation,
                    spc
                );
            }
        );
    }

    private static void Execute(
        ImmutableArray<RegistrationInvocationCandidate?> invocations,
        ImmutableArray<OpenGenericInvocationCandidate?> openGenericInvocations,
        ImmutableArray<ITypeSymbol?> closedGenericUsages,
        ImmutableArray<ITypeSymbol?> closedGenericDeclarations,
        ImmutableArray<ITypeSymbol> ctorClosedGenerics,
        Compilation compilation,
        SourceProductionContext context
    )
    {
        var normalizedRegistrations = RegistrationSemanticPipeline.NormalizeRegistrations(
            invocations
        );
        ReportRegistrationDiagnostics(normalizedRegistrations.Diagnostics, context);

        var generationPlan = RegistrationPlanBuilder.Build(
            normalizedRegistrations,
            openGenericInvocations,
            closedGenericUsages,
            closedGenericDeclarations,
            ctorClosedGenerics,
            compilation
        );
        ReportRegistrationDiagnostics(generationPlan.Diagnostics, context);

        if (!generationPlan.HasSourcesToEmit)
            return;

        ReportCircularDependencyDiagnostics(generationPlan.Registrations, context);
        ServiceRegistrationSourceEmitter.EmitSources(generationPlan, compilation, context);
    }

    private static void ReportRegistrationDiagnostics(
        IEnumerable<Diagnostic> diagnostics,
        SourceProductionContext context
    )
    {
        foreach (var diagnostic in diagnostics)
        {
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void ReportCircularDependencyDiagnostics(
        IEnumerable<ServiceRegistration> registrations,
        SourceProductionContext context
    )
    {
        var circularDependencies = DetectCircularDependencies(registrations);
        foreach (var cycle in circularDependencies)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(DiagnosticDescriptors.CircularDependency, Location.None, cycle)
            );
        }
    }

    /// <summary>
    /// Detects circular dependencies at compile-time by analyzing the dependency graph.
    /// </summary>
    private static List<string> DetectCircularDependencies(
        IEnumerable<ServiceRegistration> registrations
    )
    {
        var cycles = new List<string>();
        var dependencyGraph = new Dictionary<string, HashSet<string>>();
        var serviceTypes = new HashSet<string>();

        foreach (var reg in registrations)
        {
            serviceTypes.Add(reg.ServiceTypeFullName);
            if (!dependencyGraph.ContainsKey(reg.ServiceTypeFullName))
                dependencyGraph[reg.ServiceTypeFullName] = [];

            foreach (var paramTypeFullName in reg.ConstructorParameters)
            {
                dependencyGraph[reg.ServiceTypeFullName].Add(paramTypeFullName);
            }
        }

        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();
        var path = new List<string>();

        foreach (var serviceType in serviceTypes)
        {
            DetectCycleDfs(serviceType, dependencyGraph, visited, recursionStack, path, cycles);
        }

        return cycles;
    }

    private static void DetectCycleDfs(
        string current,
        Dictionary<string, HashSet<string>> graph,
        HashSet<string> visited,
        HashSet<string> recursionStack,
        List<string> path,
        List<string> cycles
    )
    {
        if (recursionStack.Contains(current))
        {
            var cycleStart = path.IndexOf(current);
            if (cycleStart < 0)
                return;

            var cyclePath = path.Skip(cycleStart).Append(current).ToList();
            var cycleStr = string.Join(" -> ", cyclePath.Select(TypeNameDisplay.GetSimpleName));
            if (!cycles.Contains(cycleStr))
                cycles.Add(cycleStr);

            return;
        }

        if (!visited.Add(current))
            return;

        recursionStack.Add(current);
        path.Add(current);

        if (graph.TryGetValue(current, out var dependencies))
        {
            foreach (var dep in dependencies)
            {
                DetectCycleDfs(dep, graph, visited, recursionStack, path, cycles);
            }
        }

        path.RemoveAt(path.Count - 1);
        recursionStack.Remove(current);
    }
}
