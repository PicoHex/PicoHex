namespace PicoDI.Gen;

[Generator(LanguageNames.CSharp)]
public sealed class InterceptorGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var interceptionCalls = context
            .SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) =>
                    node
                        is InvocationExpressionSyntax
                        {
                            Expression: MemberAccessExpressionSyntax
                            {
                                Name: GenericNameSyntax
                                {
                                    Identifier.ValueText: "InterceptBy"
                                        or "WithoutInterceptor"
                                        or "WithoutInterceptors"
                                }
                            }
                        },
                transform: static (ctx, ct) => ExtractInterceptionInfo(ctx, ct)
            )
            .Where(static info => info is not null);

        var globalCalls = context
            .SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) =>
                    node
                        is InvocationExpressionSyntax
                        {
                            Expression: MemberAccessExpressionSyntax
                            {
                                Name: GenericNameSyntax { Identifier.ValueText: "AddInterceptor" }
                            }
                        },
                transform: static (ctx, ct) => ExtractGlobalInterceptorInfo(ctx, ct)
            )
            .Where(static info => info is not null);

        context.RegisterSourceOutput(
            interceptionCalls.Collect().Combine(globalCalls.Collect()),
            static (spc, source) => GenerateInterceptorRegistrations(spc, source.Left, source.Right)
        );
    }

    private static InterceptionInfo? ExtractInterceptionInfo(
        GeneratorSyntaxContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.Node is not InvocationExpressionSyntax invocation)
            return null;

        var semanticModel = ctx.SemanticModel;
        var iinterceptorType = semanticModel
            .Compilation
            .GetTypeByMetadataName("PicoDI.Abs.IInterceptor");

        // Walk from InterceptBy back to the Register call
        var current = invocation;
        InvocationExpressionSyntax? registerCall = null;
        var interceptorArgTypes = new List<ITypeSymbol>();
        var hasWithoutInterceptors = false;
        var withoutInterceptorTypes = new List<ITypeSymbol>();

        while (
            current.Expression
                is MemberAccessExpressionSyntax
                {
                    Expression: InvocationExpressionSyntax nextInvocation
                } memberAccess
        )
        {
            var methodName = memberAccess.Name is GenericNameSyntax gen
                ? gen.Identifier.ValueText
                : (memberAccess.Name as IdentifierNameSyntax)?.Identifier.ValueText;

            if (methodName is "InterceptBy" or "WithoutInterceptor")
            {
                if (
                    memberAccess.Name is GenericNameSyntax genericName
                    && genericName.TypeArgumentList.Arguments.Count > 0
                )
                {
                    var typeArg = semanticModel
                        .GetTypeInfo(genericName.TypeArgumentList.Arguments[0])
                        .Type;
                    if (typeArg is not null)
                    {
                        if (methodName == "InterceptBy")
                            interceptorArgTypes.Insert(0, typeArg); // outermost first
                        else
                            withoutInterceptorTypes.Add(typeArg);
                    }
                }
            }
            else if (methodName == "WithoutInterceptors")
            {
                hasWithoutInterceptors = true;
            }

            current = nextInvocation;
        }

        // current should now be the Register call or end of chain
        if (current.Expression is MemberAccessExpressionSyntax memAccess
            && memAccess.Name.Identifier.ValueText == "Register")
        {
            registerCall = current;
        }

        if (registerCall is null || interceptorArgTypes.Count == 0)
            return null;

        // Extract service and implementation types from Register call
        var registerSymbol = semanticModel.GetSymbolInfo(registerCall).Symbol as IMethodSymbol;
        ITypeSymbol? serviceType = null;
        ITypeSymbol? implType = null;

        if (registerSymbol is { TypeArguments.Length: >= 1 })
        {
            // Pattern: Register<T, Impl>(lifetime)
            serviceType = registerSymbol.TypeArguments[0];
            implType = registerSymbol.TypeArguments.Length >= 2
                ? registerSymbol.TypeArguments[1] : null;
        }
        else if (registerCall.ArgumentList.Arguments.Count >= 2)
        {
            // Pattern: Register(typeof(T), typeof(Impl), lifetime)
            var firstArg = registerCall.ArgumentList.Arguments[0].Expression;
            if (firstArg is TypeOfExpressionSyntax { Type: var typeSyntax })
                serviceType = semanticModel.GetTypeInfo(typeSyntax).Type;

            var secondArg = registerCall.ArgumentList.Arguments[1].Expression;
            if (secondArg is TypeOfExpressionSyntax { Type: var implSyntax })
                implType = semanticModel.GetTypeInfo(implSyntax).Type;
        }

        if (serviceType is null)
            return null;

        return new InterceptionInfo(
            serviceType, implType, interceptorArgTypes,
            withoutInterceptorTypes, hasWithoutInterceptors
        );
    }

    private static GlobalInterceptorInfo? ExtractGlobalInterceptorInfo(
        GeneratorSyntaxContext ctx, CancellationToken ct)
    {
        if (ctx.Node is not InvocationExpressionSyntax invocation)
            return null;

        var current = invocation;
        ITypeSymbol? interceptorType = null;
        string? namespaceFilter = null;
        ITypeSymbol? interfaceFilter = null;
        var excludedTypes = new List<ITypeSymbol>();

        while (current.Expression is MemberAccessExpressionSyntax
               {
                   Expression: InvocationExpressionSyntax next
               } memberAccess)
        {
            var methodName = memberAccess.Name.Identifier.ValueText;

            if (methodName == "AddInterceptor"
                && memberAccess.Name is GenericNameSyntax genName
                && genName.TypeArgumentList.Arguments.Count > 0)
            {
                interceptorType = ctx.SemanticModel
                    .GetTypeInfo(genName.TypeArgumentList.Arguments[0]).Type;
                break;
            }

            if (methodName == "WhereNamespace"
                && current.ArgumentList.Arguments.Count > 0)
            {
                var arg = current.ArgumentList.Arguments[0].Expression;
                if (arg is LiteralExpressionSyntax { Token.ValueText: var ns })
                    namespaceFilter = ns;
            }
            else if (methodName == "WhereImplements"
                && memberAccess.Name is GenericNameSyntax whereGen
                && whereGen.TypeArgumentList.Arguments.Count > 0)
            {
                interfaceFilter = ctx.SemanticModel
                    .GetTypeInfo(whereGen.TypeArgumentList.Arguments[0]).Type;
            }
            else if (methodName == "Except"
                && memberAccess.Name is GenericNameSyntax exceptGen
                && exceptGen.TypeArgumentList.Arguments.Count > 0)
            {
                var excluded = ctx.SemanticModel
                    .GetTypeInfo(exceptGen.TypeArgumentList.Arguments[0]).Type;
                if (excluded is not null) excludedTypes.Add(excluded);
            }

            current = next;
        }

        if (interceptorType is null) return null;

        return new GlobalInterceptorInfo(
            interceptorType, namespaceFilter, interfaceFilter,
            excludedTypes.Count > 0 ? excludedTypes : null);
    }

    private static void GenerateInterceptorRegistrations(
        SourceProductionContext spc,
        ImmutableArray<InterceptionInfo?> perService,
        ImmutableArray<GlobalInterceptorInfo?> globals
    )
    {
        // Collect all interceptor types per service, ordered by declaration
        var perServiceMap = new Dictionary<string, InterceptionInfo>(StringComparer.Ordinal);
        foreach (var info in perService.OfType<InterceptionInfo>())
        {
            var key = info.ServiceType.ToDisplayString();
            if (perServiceMap.TryGetValue(key, out var existing))
            {
                // Merge: append interceptors, combine exclusions
                perServiceMap[key] = new InterceptionInfo(
                    existing.ServiceType,
                    existing.ImplType ?? info.ImplType,
                    existing.InterceptorTypes.Concat(info.InterceptorTypes)
                        .Distinct(SymbolEqualityComparer.Default).OfType<ITypeSymbol>().ToList(),
                    existing.WithoutInterceptorTypes.Concat(info.WithoutInterceptorTypes)
                        .Distinct(SymbolEqualityComparer.Default).OfType<ITypeSymbol>().ToList(),
                    existing.WithoutInterceptors || info.WithoutInterceptors);
            }
            else
            {
                perServiceMap[key] = info;
            }
        }
        var deduped = perServiceMap.Values.ToList();
        if (deduped.Count == 0) return;

        var sb = new StringBuilder();
        var regSb = new StringBuilder();

        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("namespace PicoDI.Generated.Aop;");
        sb.AppendLine();
        sb.AppendLine("using PicoDI;");
        sb.AppendLine("using PicoDI.Abs;");
        sb.AppendLine();

        foreach (var info in deduped)
        {
            if (info.ServiceType is not INamedTypeSymbol serviceType)
                continue;

            // Merge global interceptors (matching) with per-service interceptors
            var interceptorList = new List<ITypeSymbol>(info.InterceptorTypes);

            // Add matching global interceptors (outermost first)
            var globalMatches = new List<ITypeSymbol>();
            foreach (var g in globals.OfType<GlobalInterceptorInfo>())
            {
                // PICO011: validate interface filter type is actually an interface
                if (g.InterfaceFilter is INamedTypeSymbol ifaceSym
                    && ifaceSym.TypeKind != TypeKind.Interface)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(
                        InterceptorDiagnostics.FilterRequiresInterface,
                        Location.None, g.InterfaceFilter.ToDisplayString()));
                    continue;
                }

                if (MatchesGlobalFilter(serviceType, g)
                    && !info.WithoutInterceptorTypes.Contains(g.InterceptorType, SymbolEqualityComparer.Default))
                {
                    globalMatches.Add(g.InterceptorType);
                }
            }

            if (info.WithoutInterceptors)
                globalMatches.Clear();

            // Globals first (outer), per-service second (inner)
            interceptorList.InsertRange(0, globalMatches);

            // PICO013: check for conflicting global+per-service declarations
            foreach (var g in globalMatches)
            {
                if (info.WithoutInterceptorTypes.Contains(g, SymbolEqualityComparer.Default))
                {
                    spc.ReportDiagnostic(Diagnostic.Create(
                        InterceptorDiagnostics.ConflictingInterceptorDeclaration,
                        Location.None, g.ToDisplayString(), serviceType.ToDisplayString()));
                }
            }

            if (interceptorList.Count == 0)
            {
                // PICO012: warn that no interceptors matched
                spc.ReportDiagnostic(Diagnostic.Create(
                    InterceptorDiagnostics.ZeroInterceptorsMatched,
                    Location.None, serviceType.ToDisplayString()));
                continue;
            }

            for (var i = 0; i < interceptorList.Count; i++)
            {
                if (interceptorList[i] is not INamedTypeSymbol interceptorNamed)
                    continue;

                if (!ImplementsIInterceptor(interceptorNamed))
                {
                    spc.ReportDiagnostic(Diagnostic.Create(
                        InterceptorDiagnostics.InterceptorTypeMismatch,
                        Location.None, interceptorNamed.Name, "IInterceptor"));
                    continue;
                }

                var isLast = i == interceptorList.Count - 1;
                EmitInvocationStruct(sb, serviceType, interceptorNamed);
                EmitDecoratorClass(sb, serviceType, info.ImplType,
                    interceptorNamed, interceptorList, i, isLast);
                EmitDiRegistration(regSb, serviceType, info.ImplType,
                    interceptorNamed, isLast);
            }
        }

        sb.AppendLine("internal static class GeneratedInterceptorRegistrations");
        sb.AppendLine("{");
        sb.AppendLine(
            "    internal static void Configure(SvcContainer container)");
        sb.AppendLine("    {");
        sb.Append(regSb);
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine(
            "internal static class AutoRegisterInterceptorConfigurator");
        sb.AppendLine("{");
        sb.AppendLine(
            "    [global::System.Runtime.CompilerServices.ModuleInitializer]");
        sb.AppendLine(
            "    internal static void Init()");
        sb.AppendLine("    {");
        sb.AppendLine(
            "        SvcContainerAutoConfiguration.RegisterConfigurator(");
        sb.AppendLine(
            "            \"intercepted::PicoDI.Aop\",");
        sb.AppendLine(
            "            static container => GeneratedInterceptorRegistrations.Configure((SvcContainer)container));");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        spc.AddSource("InterceptorRegistrations.g.cs", sb.ToString());
    }

    private static void EmitInvocationStruct(
        StringBuilder sb, INamedTypeSymbol serviceType, INamedTypeSymbol interceptorType)
    {
        var safeSvc = Sanitize(serviceType.ToDisplayString());
        var safeInt = Sanitize(interceptorType.Name);

        foreach (var method in serviceType.GetMembers().OfType<IMethodSymbol>()
            .Where(m => m.MethodKind == MethodKind.Ordinary))
        {
            var retType = method.ReturnType;
            var resultName = retType is INamedTypeSymbol { MetadataName: "ValueTask`1" or "Task`1" } t
                ? t.TypeArguments[0].ToDisplayString()
                : retType.SpecialType == SpecialType.System_Void
                || retType is INamedTypeSymbol { MetadataName: "ValueTask" or "Task" }
                    ? "PicoDI.Abs.VoidResult"
                    : retType.ToDisplayString();

            var structName = $"{safeSvc}_{safeInt}_{method.Name}_Invocation";
            var svcName = serviceType.ToDisplayString();
            var paramList = method.Parameters.ToList();

            sb.AppendLine(
                $"internal struct {structName} : IInvocation<{resultName}>");
            sb.AppendLine("{");
            sb.AppendLine($"    private readonly {svcName} _target;");
            foreach (var p in paramList)
                sb.AppendLine(
                    $"    private readonly {p.Type.ToDisplayString()} _{p.Name};");

            sb.AppendLine($"    public string MethodName => \"{method.Name}\";");
            sb.AppendLine(
                $"    public System.Type ServiceType => typeof({svcName});");
            sb.AppendLine("    public ISvcScope? Scope { get; }");
            sb.AppendLine($"    public {resultName} Result {{ get; set; }}");
            sb.AppendLine();

            var paramDecl = paramList.Count > 0
                ? ", " + string.Join(", ", paramList.Select(
                    p => $"{p.Type.ToDisplayString()} {p.Name}"))
                : "";
            sb.AppendLine(
                $"    public {structName}({svcName} target{paramDecl}, ISvcScope? scope)");
            sb.AppendLine("    {");
            sb.AppendLine("        _target = target;");
            foreach (var p in paramList)
                sb.AppendLine($"        _{p.Name} = {p.Name};");
            sb.AppendLine("        Scope = scope;");
            sb.AppendLine("        Result = default!;");
            sb.AppendLine("    }");
            sb.AppendLine();

            var paramArgs = string.Join(", ", paramList.Select(p => $"_{p.Name}"));
            sb.AppendLine(
                $"    public {resultName} InvokeTarget() => _target.{method.Name}({paramArgs});");
            sb.AppendLine("}");
            sb.AppendLine();
        }
    }

    private static void EmitDecoratorClass(
        StringBuilder sb, INamedTypeSymbol serviceType,
        ITypeSymbol? implType, INamedTypeSymbol interceptorType,
        IReadOnlyList<ITypeSymbol> allInterceptors, int index, bool isLast)
    {
        var safeSvc = Sanitize(serviceType.ToDisplayString());
        var safeInt = Sanitize(interceptorType.Name);
        var className = $"{safeSvc}_{safeInt}Decorator";
        var svcName = serviceType.ToDisplayString();
        var intName = interceptorType.ToDisplayString();
        var innerTypeName = isLast
            ? (implType?.ToDisplayString() ?? svcName)
            : Sanitize(serviceType.ToDisplayString()) + "_"
                + Sanitize(((INamedTypeSymbol)allInterceptors[index + 1]).Name)
                + "Decorator";

        sb.AppendLine($"sealed class {className} : {svcName}");
        sb.AppendLine("{");
        sb.AppendLine($"    private readonly {svcName} _inner;");
        sb.AppendLine($"    private readonly {intName} _i0;");
        sb.AppendLine();
        sb.AppendLine(
            $"    public {className}({svcName} inner, {intName} i0)");
        sb.AppendLine("    {");
        sb.AppendLine("        _inner = inner;");
        sb.AppendLine("        _i0 = i0;");
        sb.AppendLine("    }");
        sb.AppendLine();

        foreach (var method in serviceType.GetMembers().OfType<IMethodSymbol>()
            .Where(m => m.MethodKind == MethodKind.Ordinary))
        {
            var retType = method.ReturnType.ToDisplayString();
            var isVoidTask = method.ReturnType is INamedTypeSymbol { MetadataName: "ValueTask" or "Task" };
            var isTaskOf = method.ReturnType is INamedTypeSymbol { MetadataName: "ValueTask`1" or "Task`1" };
            var isVoid = isVoidTask || method.ReturnType.SpecialType == SpecialType.System_Void;

            var paramDecl = string.Join(", ", method.Parameters.Select(
                p => $"{p.Type.ToDisplayString()} {p.Name}"));
            var paramArgs = method.Parameters.Any()
                ? ", " + string.Join(", ", method.Parameters.Select(p => p.Name))
                : "";
            var structRef = $"{safeSvc}_{safeInt}_{method.Name}_Invocation";

            sb.AppendLine($"    public {retType} {method.Name}({paramDecl})");
            sb.AppendLine("    {");
            sb.AppendLine($"        var inv = new {structRef}(_inner{paramArgs}, scope: null);");
            if (isVoidTask) sb.AppendLine("        _i0.InvokeVoid(inv, _ => inv.InvokeTarget()); return global::System.Threading.Tasks.Task.CompletedTask;");
            else if (isTaskOf) sb.AppendLine("        var r = _i0.Invoke(inv, _ => inv.InvokeTarget()); return global::System.Threading.Tasks.Task.FromResult(r);");
            else if (isVoid) sb.AppendLine("        _i0.InvokeVoid(inv, _ => inv.InvokeTarget());");
            else sb.AppendLine("        return _i0.Invoke(inv, _ => inv.InvokeTarget());");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        sb.AppendLine("}");
        sb.AppendLine();
    }

    private static string Sanitize(string name)
    {
        return name.Replace("<", "_")
            .Replace(">", "_")
            .Replace(",", "_")
            .Replace(".", "_")
            .Replace(" ", "");
    }

    private static void EmitDiRegistration(
        StringBuilder sb, INamedTypeSymbol serviceType,
        ITypeSymbol? implType, INamedTypeSymbol interceptorType,
        bool isLast)
    {
        if (implType is null
            || SymbolEqualityComparer.Default.Equals(serviceType, implType))
            return;

        var safeSvc = Sanitize(serviceType.ToDisplayString());
        var safeInt = Sanitize(interceptorType.Name);
        var decoratorClass = $"{safeSvc}_{safeInt}Decorator";
        var svcName = serviceType.ToDisplayString();
        var intName = interceptorType.ToDisplayString();
        var implName = implType.ToDisplayString();
        var resolveType = isLast ? implName : svcName;

        sb.AppendLine(
            $"        container.Register<{svcName}>(scope =>");
        sb.AppendLine("        {");
        sb.AppendLine(
            $"            var inner = scope.GetService<{resolveType}>();");
        sb.AppendLine(
            $"            var i0 = scope.GetService<{intName}>();");
        sb.AppendLine(
            $"            return new {decoratorClass}(inner, i0);");
        sb.AppendLine("        }, SvcLifetime.Scoped);");
        sb.AppendLine();
    }

    private static bool MatchesGlobalFilter(
        INamedTypeSymbol serviceType, GlobalInterceptorInfo filter)
    {
        // Excluded?
        if (filter.ExcludedTypes?.Any(
                e => SymbolEqualityComparer.Default.Equals(e, serviceType)) == true)
            return false;

        // Namespace filter
        if (filter.NamespaceFilter is not null)
        {
            var ns = serviceType.ContainingNamespace?.ToDisplayString() ?? "";
            if (ns != filter.NamespaceFilter)
                return false;
        }

        // Interface filter
        if (filter.InterfaceFilter is not null)
        {
            if (!serviceType.AllInterfaces.Any(
                    i => SymbolEqualityComparer.Default.Equals(i, filter.InterfaceFilter)))
                return false;
        }

        return true;
    }

    private static bool ImplementsIInterceptor(INamedTypeSymbol type)
    {
        foreach (var iface in type.AllInterfaces)
        {
            if (iface.ToDisplayString() == "PicoDI.Abs.IInterceptor")
                return true;
        }
        return type.ToDisplayString() == "PicoDI.Abs.IInterceptor"
            || type.BaseType?.ToDisplayString() == "PicoDI.Abs.InterceptorBase";
    }

    private sealed record InterceptionInfo(
        ITypeSymbol ServiceType,
        ITypeSymbol? ImplType,
        IReadOnlyList<ITypeSymbol> InterceptorTypes,
        IReadOnlyList<ITypeSymbol> WithoutInterceptorTypes,
        bool WithoutInterceptors
    );

    private sealed record GlobalInterceptorInfo(
        ITypeSymbol InterceptorType,
        string? NamespaceFilter = null,
        ITypeSymbol? InterfaceFilter = null,
        List<ITypeSymbol>? ExcludedTypes = null);
}
