namespace PicoCfg.Gen;

// Orchestrates the incremental generator pipeline and target aggregation.
[Generator(LanguageNames.CSharp)]
public sealed partial class PicoCfgBindGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var calls = context
            .SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => PicoCfgBindGenerator.IsCandidateInvocation(node),
                static (ctx, _) => PicoCfgBindGenerator.TransformInvocation(ctx)
            )
            .Where(static call => call is not null)
            .Select(static (call, _) => call!);

        context.RegisterSourceOutput(
            calls.Collect(),
            static (spc, collectedCalls) => Execute(spc, collectedCalls)
        );
    }

    private static void Execute(SourceProductionContext context, ImmutableArray<BindCall> calls)
    {
        if (calls.IsDefaultOrEmpty)
            return;

        var targets = new Dictionary<ITypeSymbol, TargetRegistration>(
            SymbolEqualityComparer.Default
        );

        // Phase 1: Collect explicit targets from Bind<T>/TryBind<T>/BindInto<T> calls
        foreach (var call in calls)
        {
            if (!targets.TryGetValue(call.TargetType, out var registration))
            {
                registration = new TargetRegistration(call.TargetType);
                targets.Add(call.TargetType, registration);
            }

            registration.Operations |= call.Operation;
            registration.Locations.Add(call.Location);
        }

        // Phase 2: Recursively discover nested bindable targets from property types
        DiscoverNestedTargets(targets, context);

        // Phase 3: Analyze all targets
        var validTargets = new List<TargetModel>(targets.Count);
        foreach (var registration in targets.Values)
        {
            if (!TryAnalyzeTarget(context, registration, out var model))
                continue;

            validTargets.Add(model);
        }

        if (validTargets.Count == 0)
            return;

        // Phase 4: Topological sort — nested types before their dependents
        validTargets = TopologicalSort(validTargets, context);
        if (validTargets.Count == 0)
            return; // cycle detected

        // Fill in NestedModelIndex for each nested property
        var sortedTypeToIndex = new Dictionary<ITypeSymbol, int>(SymbolEqualityComparer.Default);
        for (var i = 0; i < validTargets.Count; i++)
            sortedTypeToIndex[validTargets[i].TargetType] = i;

        foreach (var target in validTargets)
        {
            foreach (var prop in target.Properties)
            {
                if (
                    prop.ScalarKind == ScalarKind.Nested
                    && prop.NestedType is not null
                    && sortedTypeToIndex.TryGetValue(prop.NestedType, out var nestedIdx)
                )
                {
                    prop.NestedModelIndex = nestedIdx;
                }
            }
        }

        // Also fill collection element nested indices
        foreach (var target in validTargets)
        {
            foreach (var prop in target.Properties)
            {
                if (
                    prop.ElementType is INamedTypeSymbol elemNamedType
                    && IsNestedBindableType(elemNamedType)
                    && sortedTypeToIndex.TryGetValue(elemNamedType, out var elemNestedIdx)
                )
                {
                    prop.CollectionElementNestedIndex = elemNestedIdx;
                }
            }
        }

        // Phase 5: Render
        context.AddSource(
            "PicoCfgBindRegistrations.g.cs",
            SourceText.From(Render(validTargets), Encoding.UTF8)
        );
    }

    private static void DiscoverNestedTargets(
        Dictionary<ITypeSymbol, TargetRegistration> targets,
        SourceProductionContext context
    )
    {
        const int maxDepth = 5;
        var queue = new Queue<ITypeSymbol>(targets.Keys);
        var depth = new Dictionary<ITypeSymbol, int>(SymbolEqualityComparer.Default);
        var reportedTruncated = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var key in targets.Keys)
            depth[key] = 0;

        while (queue.Count > 0)
        {
            var type = queue.Dequeue();
            var currentDepth = depth.TryGetValue(type, out var d) ? d : 0;

            if (currentDepth >= maxDepth)
            {
                if (reportedTruncated.Add(type))
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            Diagnostics.NestingTruncated,
                            Location.None,
                            maxDepth,
                            type.ToDisplayString()
                        )
                    );
                }
                continue;
            }

            if (type is not INamedTypeSymbol namedType)
                continue;

            foreach (var member in namedType.GetMembers())
            {
                if (member is not IPropertySymbol prop)
                    continue;

                if (
                    IsNestedBindableType(prop.Type)
                    && !targets.ContainsKey(prop.Type)
                    && !IsSilentlySkippablePropertyType(prop.Type)
                )
                {
                    var nestedType = (INamedTypeSymbol)prop.Type;
                    targets.Add(nestedType, new TargetRegistration(nestedType));
                    depth[nestedType] = currentDepth + 1;
                    queue.Enqueue(nestedType);
                }

                if (
                    TryGetCollectionKind(prop.Type, out _, out var elementType)
                    && elementType is INamedTypeSymbol nestedElementType
                    && IsNestedBindableType(nestedElementType)
                    && !targets.ContainsKey(nestedElementType)
                )
                {
                    targets.Add(nestedElementType, new TargetRegistration(nestedElementType));
                    depth[nestedElementType] = currentDepth + 1;
                    queue.Enqueue(nestedElementType);
                }
            }
        }
    }

    private static List<TargetModel> TopologicalSort(
        List<TargetModel> targets,
        SourceProductionContext context
    )
    {
        if (targets.Count <= 1)
            return targets;

        // Map type symbols to their positions
        var typeToIndex = new Dictionary<ITypeSymbol, int>(SymbolEqualityComparer.Default);
        for (var i = 0; i < targets.Count; i++)
            typeToIndex[targets[i].TargetType] = i;

        // Build adjacency: adj[dep] = [dependent, ...]  (nested types first!)
        var inDegree = new int[targets.Count];
        var adj = new List<int>[targets.Count];
        for (var i = 0; i < targets.Count; i++)
            adj[i] = new List<int>();

        for (var parentIdx = 0; parentIdx < targets.Count; parentIdx++)
        {
            var target = targets[parentIdx];
            foreach (var prop in target.Properties)
            {
                if (
                    typeToIndex.TryGetValue(prop.Type, out var nestedTypeIdx)
                    && nestedTypeIdx != parentIdx
                )
                {
                    // parentIdx depends on nestedTypeIdx being rendered first
                    adj[nestedTypeIdx].Add(parentIdx);
                    inDegree[parentIdx]++;
                }
            }
        }

        // Kahn's algorithm
        var queue = new Queue<int>();
        for (var i = 0; i < targets.Count; i++)
        {
            if (inDegree[i] == 0)
                queue.Enqueue(i);
        }

        var sorted = new List<TargetModel>(targets.Count);
        while (queue.Count > 0)
        {
            var idx = queue.Dequeue();
            sorted.Add(targets[idx]);
            foreach (var dependent in adj[idx])
            {
                inDegree[dependent]--;
                if (inDegree[dependent] == 0)
                    queue.Enqueue(dependent);
            }
        }

        if (sorted.Count != targets.Count)
        {
            var cycleIndices = Enumerable
                .Range(0, targets.Count)
                .Where(i => inDegree[i] > 0)
                .ToArray();
            if (cycleIndices.Length > 0)
            {
                var cyclePath = FormatCyclePath(targets, adj, cycleIndices);
                context.ReportDiagnostic(
                    Diagnostic.Create(Diagnostics.CycleInNestedTypes, Location.None, cyclePath)
                );
            }
            return new List<TargetModel>(0); // Empty — don't generate
        }

        return sorted;
    }

    private static string FormatCyclePath(
        IReadOnlyList<TargetModel> targets,
        List<int>[] adj,
        int[] cycleIndices
    )
    {
        var dependsOn = new List<int>[adj.Length];
        for (var i = 0; i < adj.Length; i++)
            dependsOn[i] = new List<int>();
        for (var dep = 0; dep < adj.Length; dep++)
            foreach (var dependent in adj[dep])
                dependsOn[dependent].Add(dep);

        var cycleSet = new HashSet<int>(cycleIndices);
        var start = cycleIndices[0];
        var path = new List<int>();
        var inPath = new HashSet<int>();

        var current = start;
        while (!inPath.Contains(current))
        {
            inPath.Add(current);
            path.Add(current);

            var next = dependsOn[current].Find(
                n => cycleSet.Contains(n) && !inPath.Contains(n)
            );

            if (next < 0)
            {
                if (dependsOn[current].Contains(start))
                    path.Add(start);
                break;
            }
            current = next;
        }

        return string.Join(" → ", path.Select(i => targets[i].TargetType.Name));
    }
}
