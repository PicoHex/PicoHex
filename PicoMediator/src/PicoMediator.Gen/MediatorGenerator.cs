namespace PicoMediator.Gen;

[Generator(LanguageNames.CSharp)]
public sealed class MediatorGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var handlerDeclarations = context
            .SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) =>
                    node is ClassDeclarationSyntax c && c.BaseList?.Types.Count > 0,
                transform: static (ctx, ct) => GetHandlerInfos(ctx, ct)
            )
            .Where(static x => x.Length > 0)
            .SelectMany(static (x, _) => x);

        context.RegisterSourceOutput(
            handlerDeclarations.Collect(),
            static (spc, handlers) => GenerateDispatch(spc, handlers)
        );

        var combined = context.CompilationProvider.Combine(handlerDeclarations.Collect());

        context.RegisterSourceOutput(
            combined,
            static (spc, pair) => GenerateRegistrations(spc, pair.Left, pair.Right)
        );

        var eventDeclarations = context
            .SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) =>
                    node is ClassDeclarationSyntax or RecordDeclarationSyntax
                    && ((TypeDeclarationSyntax)node).BaseList?.Types.Count > 0,
                transform: static (ctx, ct) => GetEventInfos(ctx, ct)
            )
            .Where(static x => x is not null);

        var combinedEvents = context.CompilationProvider.Combine(eventDeclarations.Collect());

        context.RegisterSourceOutput(
            combinedEvents,
            static (spc, pair) => GenerateEventDispatchers(spc, pair.Left, pair.Right)
        );

        var callSites = context
            .SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is InvocationExpressionSyntax,
                transform: static (ctx, ct) => AnalyzePublishCallSite(ctx, ct)
            )
            .Where(static x => x is not null);

        context.RegisterSourceOutput(
            context
                .CompilationProvider.Combine(eventDeclarations.Collect())
                .Combine(callSites.Collect()),
            static (spc, pair) => ReportBasePublishDiagnostics(spc, pair.Left.Right, pair.Right)
        );
    }

    private enum HandlerKind
    {
        Command,
        Subscribe,
    }

    private static ImmutableArray<HandlerInfo> GetHandlerInfos(
        GeneratorSyntaxContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.Node is not ClassDeclarationSyntax classDecl)
            return [];

        var typeSymbol = ctx.SemanticModel.GetDeclaredSymbol(classDecl, ct) as INamedTypeSymbol;
        var accessibility = typeSymbol?.DeclaredAccessibility;
        if (
            typeSymbol is null
            || typeSymbol.IsAbstract
            || typeSymbol.IsGenericType
            // Private/protected nested classes cannot be instantiated by the
            // generated code (same assembly access only) — skip them.
            || (accessibility != Accessibility.Public && accessibility != Accessibility.Internal)
        )
            return [];

        var results = ImmutableArray.CreateBuilder<HandlerInfo>();
        foreach (var iface in typeSymbol.AllInterfaces)
        {
            if (!iface.IsGenericType)
                continue;

            var constructed = iface.ConstructedFrom;
            if (constructed.ContainingNamespace.ToDisplayString() != "PicoMediator.Abs")
                continue;

            if (constructed.MetadataName == "ICommandHandler`2")
            {
                results.Add(CreateHandlerInfo(HandlerKind.Command, typeSymbol, iface));
            }
            else if (constructed.MetadataName == "ISubscriber`1")
            {
                results.Add(CreateHandlerInfo(HandlerKind.Subscribe, typeSymbol, iface));
            }
        }

        return results.ToImmutable();
    }

    private static HandlerInfo CreateHandlerInfo(
        HandlerKind kind,
        INamedTypeSymbol typeSymbol,
        INamedTypeSymbol iface
    )
    {
        // First public instance ctor wins; ambiguous ctors are the author's
        // responsibility. No public ctor (e.g. internal default ctor) -> no
        // arguments (generated code is in the same assembly, so `new Impl()`
        // still works).
        var ctor = typeSymbol.InstanceConstructors.FirstOrDefault(c =>
            c.DeclaredAccessibility == Accessibility.Public && !c.IsStatic
        );
        var ctorParams = ctor is null
            ? []
            : ctor
                .Parameters.Select(p =>
                    p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                )
                .ToImmutableArray();

        return new HandlerInfo(
            kind,
            iface.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            kind == HandlerKind.Command
                ? iface.TypeArguments[1].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                : null,
            typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            ctorParams
        );
    }

    private static void GenerateRegistrations(
        SourceProductionContext context,
        Compilation compilation,
        ImmutableArray<HandlerInfo> handlers
    )
    {
        if (handlers.IsDefaultOrEmpty)
            return;

        var assemblyName = compilation.AssemblyName ?? "Unknown";
        var safeAssemblyName = SanitizeIdentifier(assemblyName);
        var className = $"MediatorHandlerRegistrations_{safeAssemblyName}";
        var configuratorId = $"pico-mediator::0::{assemblyName}::{className}";

        var sb = new StringBuilder(8192);
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine();
        sb.AppendLine("namespace PicoMediator.Generated;");
        sb.AppendLine();
        sb.AppendLine("public static class " + className);
        sb.AppendLine("{");
        sb.AppendLine("    [ModuleInitializer]");
        sb.AppendLine("    internal static void AutoRegister()");
        sb.AppendLine("    {");
        sb.AppendLine("        global::PicoMediator.MediatorAutoSubscriptionRegistry.Register(");
        sb.AppendLine(
            $"            \"{EscapeStringLiteral(configuratorId)}\", static container => ConfigureGeneratedHandlers(container));"
        );
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine(
            "    internal static void ConfigureGeneratedHandlers(global::PicoDI.Abs.ISvcContainer container)"
        );
        sb.AppendLine("    {");

        foreach (var h in handlers)
        {
            var serviceType =
                h.Kind == HandlerKind.Command
                    ? $"global::PicoMediator.Abs.ICommandHandler<{h.RequestType}, {h.ResponseType}>"
                    : $"global::PicoMediator.Abs.ISubscriber<{h.RequestType}>";

            sb.AppendLine($"        if (!container.IsRegistered(typeof({serviceType})))");
            sb.AppendLine("        {");
            sb.AppendLine(
                "            container.Register(global::PicoDI.Abs.SvcDescriptor.Create("
            );
            sb.AppendLine($"                typeof({serviceType}),");
            sb.AppendLine($"                {EmitFactory(h)},");
            sb.AppendLine("                global::PicoDI.Abs.SvcLifetime.Transient));");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");
        context.AddSource($"MediatorHandlerRegistrations.{safeAssemblyName}.g.cs", sb.ToString());
    }

    private static string EmitFactory(HandlerInfo h)
    {
        if (h.ConstructorParameterTypes.IsEmpty)
            return $"static _ => new {h.ImplementationType}()";

        var args = string.Join(
            ", ",
            h.ConstructorParameterTypes.Select(p => $"({p})scope.GetService(typeof({p}))")
        );
        return $"static scope => new {h.ImplementationType}({args})";
    }

    private static readonly DiagnosticDescriptor BasePublishNoDerivedRule = new(
        id: "PMGEN001",
        title: "Base-typed publish cannot reach concrete subscribers",
        messageFormat: "Publish of base type '{0}' cannot reach concrete subscribers: "
            + "no concrete event type deriving from '{0}' is visible to PicoMediator.Gen",
        category: "PicoMediator.Gen",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    private sealed class CallSiteInfo
    {
        public Location Location { get; }
        public string ArgTypeFqn { get; }
        public bool IsEventFamily { get; }
        public bool IsConcrete { get; }

        public CallSiteInfo(
            Location location,
            string argTypeFqn,
            bool isEventFamily,
            bool isConcrete
        )
        {
            Location = location;
            ArgTypeFqn = argTypeFqn;
            IsEventFamily = isEventFamily;
            IsConcrete = isConcrete;
        }
    }

    private static CallSiteInfo? AnalyzePublishCallSite(
        GeneratorSyntaxContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.Node is not InvocationExpressionSyntax inv)
            return null;
        if (inv.ArgumentList.Arguments.Count < 1)
            return null;

        var methodSymbol = ctx.SemanticModel.GetSymbolInfo(inv, ct).Symbol as IMethodSymbol;
        if (methodSymbol is null)
            return null;
        if (methodSymbol.Name is not ("Publish" or "PublishParallel"))
            return null;
        // Matches both interface-typed receivers (symbol declaring type is
        // IPublisher itself, whose AllInterfaces is empty) and concrete
        // receivers (Mediator : IMediator : IPublisher).
        var containingType = methodSymbol.ContainingType;
        var isPublisher =
            containingType.ToDisplayString() == "PicoMediator.Abs.IPublisher"
            || containingType.AllInterfaces.Any(i =>
                i.ToDisplayString() == "PicoMediator.Abs.IPublisher"
            );
        if (!isPublisher)
            return null;

        var argType = ctx
            .SemanticModel.GetTypeInfo(inv.ArgumentList.Arguments[0].Expression, ct)
            .Type;

        if (argType is not INamedTypeSymbol named)
            return null; // type parameters and unresolved types are skipped

        var isEventFamily =
            named.ToDisplayString() == "PicoMediator.Abs.IEvent"
            || named.AllInterfaces.Any(i => i.ToDisplayString() == "PicoMediator.Abs.IEvent");
        if (!isEventFamily)
            return null; // not an event publish (e.g. ICommand) — no diagnostic

        var isConcrete = named.TypeKind == TypeKind.Class && !named.IsAbstract;

        return new CallSiteInfo(
            inv.GetLocation(),
            named.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            true,
            isConcrete
        );
    }

    private static void ReportBasePublishDiagnostics(
        SourceProductionContext context,
        ImmutableArray<INamedTypeSymbol?> events,
        ImmutableArray<CallSiteInfo?> callSites
    )
    {
        var baseMap = BuildBaseMap([
            .. events.Where(static e => e is not null).Cast<INamedTypeSymbol>(),
        ]);

        foreach (var cs in callSites)
        {
            if (cs is null || cs.IsConcrete)
                continue;
            if (baseMap.ContainsKey(cs.ArgTypeFqn))
                continue; // covered by a dispatcher

            context.ReportDiagnostic(
                Diagnostic.Create(BasePublishNoDerivedRule, cs.Location, cs.ArgTypeFqn)
            );
        }
    }

    private static INamedTypeSymbol? GetEventInfos(GeneratorSyntaxContext ctx, CancellationToken ct)
    {
        if (ctx.Node is not (ClassDeclarationSyntax or RecordDeclarationSyntax))
            return null;

        var typeSymbol = ctx.SemanticModel.GetDeclaredSymbol(ctx.Node, ct) as INamedTypeSymbol;
        if (typeSymbol is null || typeSymbol.IsAbstract || typeSymbol.IsGenericType)
            return null;
        // Class OR struct (record struct) events are both supported — the
        // dispatcher's switch matches boxed struct instances on the interface
        // parameter, and Forward<TEvent> handles struct TEvent.
        if (typeSymbol.TypeKind is not (TypeKind.Class or TypeKind.Struct))
            return null;
        if (!typeSymbol.AllInterfaces.Any(i => i.ToDisplayString() == "PicoMediator.Abs.IEvent"))
            return null;

        return typeSymbol;
    }

    /// <summary>
    /// base type FQN -> concrete derived event FQNs (in-compilation).
    /// Only non-concrete bases (interfaces + abstract classes) qualify —
    /// concrete intermediate bases keep direct-key semantics (recursion invariant).
    /// </summary>
    private static Dictionary<string, List<string>> BuildBaseMap(
        ImmutableArray<INamedTypeSymbol> events
    )
    {
        var map = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var e in events)
        {
            var eFqn = e.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            foreach (var b in GetEventBases(e))
            {
                var bFqn = b.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                if (!map.TryGetValue(bFqn, out var list))
                    map[bFqn] = list = [];
                if (!list.Contains(eFqn))
                    list.Add(eFqn);
            }
        }
        return map;
    }

    private static IEnumerable<INamedTypeSymbol> GetEventBases(INamedTypeSymbol e)
    {
        // interfaces in the chain (including IEvent itself)
        foreach (var i in e.AllInterfaces)
        {
            if (i.ToDisplayString() == "PicoMediator.Abs.IEvent")
            {
                yield return i;
                continue;
            }
            if (i.AllInterfaces.Any(x => x.ToDisplayString() == "PicoMediator.Abs.IEvent"))
                yield return i;
        }

        // abstract base classes in the chain
        for (
            var b = e.BaseType;
            b is not null && b.SpecialType != SpecialType.System_Object;
            b = b.BaseType
        )
        {
            if (
                b.IsAbstract
                && b.AllInterfaces.Any(x => x.ToDisplayString() == "PicoMediator.Abs.IEvent")
            )
                yield return b;
        }
    }

    private static void GenerateEventDispatchers(
        SourceProductionContext context,
        Compilation compilation,
        ImmutableArray<INamedTypeSymbol?> events
    )
    {
        var concrete = new List<INamedTypeSymbol>();
        foreach (var e in events)
            if (e is not null)
                concrete.Add(e);

        var baseMap = BuildBaseMap([.. concrete]);
        if (baseMap.Count is 0)
            return;

        var assemblyName = compilation.AssemblyName ?? "Unknown";
        var safeAssemblyName = SanitizeIdentifier(assemblyName);
        var className = $"MediatorEventDispatchers_{safeAssemblyName}";
        var configuratorId = $"pico-mediator::1::{assemblyName}::{className}";

        var sb = new StringBuilder(8192);
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("// Base-type publish bridges. Invariant: registered ONLY under");
        sb.AppendLine("// non-concrete base keys; switch cases target derived concrete types");
        sb.AppendLine("// only — forwarding keys and registration keys are disjoint, so");
        sb.AppendLine("// there is no double delivery and no self-forwarding recursion.");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine();
        sb.AppendLine("namespace PicoMediator.Generated;");
        sb.AppendLine();

        foreach (var pair in baseMap.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            EmitDispatcherClass(sb, pair.Key, pair.Value);
            sb.AppendLine();
        }

        sb.AppendLine($"public static class {className}");
        sb.AppendLine("{");
        sb.AppendLine("    [ModuleInitializer]");
        sb.AppendLine("    internal static void AutoRegister()");
        sb.AppendLine("    {");
        sb.AppendLine("        global::PicoMediator.MediatorAutoSubscriptionRegistry.Register(");
        sb.AppendLine(
            $"            \"{EscapeStringLiteral(configuratorId)}\", static container => ConfigureGeneratedHandlers(container));"
        );
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine(
            "    internal static void ConfigureGeneratedHandlers(global::PicoDI.Abs.ISvcContainer container)"
        );
        sb.AppendLine("    {");

        foreach (var baseFqn in baseMap.Keys.OrderBy(x => x, StringComparer.Ordinal))
        {
            var dispatcherName = "EventDispatcher_" + SanitizeIdentifier(baseFqn);
            // Infrastructure registration — intentionally no IsRegistered dedup:
            // bridges coexist with manual base-key registrations (disjoint keys).
            sb.AppendLine("        container.Register(global::PicoDI.Abs.SvcDescriptor.Create(");
            sb.AppendLine($"            typeof(global::PicoMediator.Abs.ISubscriber<{baseFqn}>),");
            sb.AppendLine($"            static scope => new {dispatcherName}(scope),");
            sb.AppendLine("            global::PicoDI.Abs.SvcLifetime.Transient));");
            sb.AppendLine();
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");
        context.AddSource($"MediatorEventDispatchers.{safeAssemblyName}.g.cs", sb.ToString());
    }

    private static void EmitDispatcherClass(StringBuilder sb, string baseFqn, List<string> derived)
    {
        var name = "EventDispatcher_" + SanitizeIdentifier(baseFqn);
        sb.AppendLine(
            $"internal sealed class {name} : global::PicoMediator.Abs.ISubscriber<{baseFqn}>"
        );
        sb.AppendLine("{");
        sb.AppendLine("    private readonly global::PicoDI.Abs.ISvcScope _scope;");
        sb.AppendLine();
        sb.AppendLine($"    public {name}(global::PicoDI.Abs.ISvcScope scope) => _scope = scope;");
        sb.AppendLine();
        sb.AppendLine("    public async global::System.Threading.Tasks.ValueTask Handle(");
        sb.AppendLine($"        {baseFqn} e,");
        sb.AppendLine("        global::System.Threading.CancellationToken ct)");
        sb.AppendLine("    {");
        sb.AppendLine("        switch (e)");
        sb.AppendLine("        {");

        foreach (var d in derived.OrderBy(x => x, StringComparer.Ordinal))
        {
            sb.AppendLine($"            case {d} typed:");
            sb.AppendLine("            {");
            sb.AppendLine(
                $"                if (!_scope.TryGetServices(typeof(global::PicoMediator.Abs.ISubscriber<{d}>), out var raws))"
            );
            sb.AppendLine("                    return;");
            sb.AppendLine("                await Forward(raws!, typed, ct);");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
        }

        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine(
            "    private static async global::System.Threading.Tasks.ValueTask Forward<TEvent>("
        );
        sb.AppendLine("        global::System.Collections.Generic.IReadOnlyList<object> raws,");
        sb.AppendLine("        TEvent e,");
        sb.AppendLine("        global::System.Threading.CancellationToken ct)");
        sb.AppendLine("        where TEvent : global::PicoMediator.Abs.IEvent");
        sb.AppendLine("    {");
        sb.AppendLine(
            "        global::System.Collections.Generic.List<global::System.Exception>? exceptions = null;"
        );
        sb.AppendLine("        foreach (var raw in raws)");
        sb.AppendLine("        {");
        sb.AppendLine("            var sub = (global::PicoMediator.Abs.ISubscriber<TEvent>)raw;");
        sb.AppendLine("            try { await sub.Handle(e, ct); }");
        sb.AppendLine(
            "            catch (global::System.Exception ex) { (exceptions ??= []).Add(ex); }"
        );
        sb.AppendLine("        }");
        sb.AppendLine("        if (exceptions is { Count: > 0 })");
        sb.AppendLine("            throw new global::System.AggregateException(exceptions);");
        sb.AppendLine("    }");
        sb.AppendLine("}");
    }

    private static string SanitizeIdentifier(string value) =>
        new(
            value
                .Select(c => c == '.' ? '_' : c)
                .Where(static c => char.IsLetterOrDigit(c) || c == '_')
                .ToArray()
        );

    private static string EscapeStringLiteral(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static void GenerateDispatch(
        SourceProductionContext context,
        ImmutableArray<HandlerInfo> handlers
    )
    {
        var commands = new List<HandlerInfo>();
        foreach (var h in handlers)
        {
            if (h.Kind == HandlerKind.Command)
                commands.Add(h);
        }

        // Subscribers only: no switch file in this task — the registrations
        // file (Task 3) carries them. Emit the switch only when commands exist.
        if (commands.Count is 0)
            return;

        var sb = new StringBuilder(4096);
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine($"// Found {commands.Count} command handler(s)");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine();
        sb.AppendLine("namespace PicoMediator.Generated;");
        sb.AppendLine();

        sb.AppendLine("internal static class MediatorSwitchDispatch");
        sb.AppendLine("{");
        sb.AppendLine("    internal static object? Dispatch(");
        sb.AppendLine("        global::System.Type requestType,");
        sb.AppendLine("        global::PicoDI.Abs.ISvcScope scope,");
        sb.AppendLine("        object request,");
        sb.AppendLine("        global::System.Threading.CancellationToken ct)");
        sb.AppendLine("    {");
        sb.AppendLine("        switch (request)");
        sb.AppendLine("        {");

        foreach (var h in commands)
        {
            AppendTypedCommandCase(sb, h);
        }

        sb.AppendLine("        }");
        sb.AppendLine("        return null;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();

        // ModuleInitializer wires it into GeneratedDispatch via RegisterSwitch
        sb.AppendLine("internal static class MediatorSwitchInitializer");
        sb.AppendLine("{");
        sb.AppendLine("    [global::System.Runtime.CompilerServices.ModuleInitializer]");
        sb.AppendLine("    internal static void Init()");
        sb.AppendLine("    {");
        sb.AppendLine(
            "        global::PicoMediator.GeneratedDispatch.RegisterSwitch(MediatorSwitchDispatch.Dispatch);"
        );
        sb.AppendLine("    }");
        sb.AppendLine("}");

        context.AddSource("MediatorSwitch.g.cs", sb.ToString());
    }

    private static void AppendTypedCommandCase(StringBuilder sb, HandlerInfo h)
    {
        var handlerType =
            $"global::PicoMediator.Abs.ICommandHandler<{h.RequestType}, {h.ResponseType}>";
        sb.AppendLine($"            case {h.RequestType} typed:");
        sb.AppendLine("            {");
        sb.AppendLine($"                var handler = ({handlerType})");
        sb.AppendLine($"                    scope.GetService(typeof({handlerType}));");
        sb.AppendLine("                if (handler is null)");
        sb.AppendLine("                    return null; // fall through to runtime dispatch");
        sb.AppendLine("                var vt = handler.Handle(typed, ct);");
        sb.AppendLine("                return vt; // boxed as object");
        sb.AppendLine("            }");
    }

    private sealed class HandlerInfo
    {
        public HandlerKind Kind { get; }
        public string RequestType { get; }
        public string? ResponseType { get; }
        public string ImplementationType { get; }
        public ImmutableArray<string> ConstructorParameterTypes { get; }

        public HandlerInfo(
            HandlerKind kind,
            string requestType,
            string? responseType,
            string implementationType,
            ImmutableArray<string> constructorParameterTypes
        )
        {
            Kind = kind;
            RequestType = requestType;
            ResponseType = responseType;
            ImplementationType = implementationType;
            ConstructorParameterTypes = constructorParameterTypes;
        }
    }
}
