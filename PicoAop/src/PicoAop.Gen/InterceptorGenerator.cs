namespace PicoAop.Gen;

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
                                        Identifier.ValueText: "InterceptBy" or "WithoutInterceptor"
                                    }
                                }
                            }
                            or InvocationExpressionSyntax
                            {
                                Expression: MemberAccessExpressionSyntax
                                {
                                    Name: IdentifierNameSyntax
                                    {
                                        Identifier.ValueText: "WithoutInterceptors"
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
                                    Name: GenericNameSyntax
                                    {
                                        Identifier.ValueText: "AddInterceptor"
                                    }
                                }
                            }
                            or InvocationExpressionSyntax
                            {
                                Expression: MemberAccessExpressionSyntax
                                {
                                    Expression: InvocationExpressionSyntax
                                    {
                                        Expression: MemberAccessExpressionSyntax
                                        {
                                            Name: GenericNameSyntax
                                            {
                                                Identifier.ValueText: "AddInterceptor"
                                            }
                                        }
                                    }
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
            .GetTypeByMetadataName(PicoAopNames.IInterceptorFull);

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
        if (
            current.Expression is MemberAccessExpressionSyntax memAccess
            && memAccess.Name.Identifier.ValueText == "Register"
        )
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
            implType =
                registerSymbol.TypeArguments.Length >= 2 ? registerSymbol.TypeArguments[1] : null;
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
            serviceType,
            implType,
            interceptorArgTypes,
            withoutInterceptorTypes,
            hasWithoutInterceptors
        );
    }

    private static GlobalInterceptorInfo? ExtractGlobalInterceptorInfo(
        GeneratorSyntaxContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.Node is not InvocationExpressionSyntax invocation)
            return null;

        var current = invocation;
        ITypeSymbol? interceptorType = null;
        string? namespaceFilter = null;
        ITypeSymbol? interfaceFilter = null;
        var excludedTypes = new List<ITypeSymbol>();

        // Check the outermost invocation first (standalone AddInterceptor call).
        if (
            invocation.Expression
                is MemberAccessExpressionSyntax
                {
                    Name: GenericNameSyntax { Identifier.ValueText: "AddInterceptor" } addGen
                }
            && addGen.TypeArgumentList.Arguments.Count > 0
        )
        {
            interceptorType = ctx.SemanticModel
                .GetTypeInfo(addGen.TypeArgumentList.Arguments[0])
                .Type;
        }

        while (
            current.Expression
                is MemberAccessExpressionSyntax
                {
                    Expression: InvocationExpressionSyntax next
                } memberAccess
        )
        {
            var methodName = memberAccess.Name.Identifier.ValueText;

            if (
                methodName == "AddInterceptor"
                && memberAccess.Name is GenericNameSyntax genName
                && genName.TypeArgumentList.Arguments.Count > 0
            )
            {
                interceptorType = ctx.SemanticModel
                    .GetTypeInfo(genName.TypeArgumentList.Arguments[0])
                    .Type;
                break;
            }

            if (methodName == "WhereNamespace" && current.ArgumentList.Arguments.Count > 0)
            {
                var arg = current.ArgumentList.Arguments[0].Expression;
                if (arg is LiteralExpressionSyntax { Token.ValueText: var ns })
                    namespaceFilter = ns;
            }
            else if (
                methodName == "WhereImplements"
                && memberAccess.Name is GenericNameSyntax whereGen
                && whereGen.TypeArgumentList.Arguments.Count > 0
            )
            {
                interfaceFilter = ctx.SemanticModel
                    .GetTypeInfo(whereGen.TypeArgumentList.Arguments[0])
                    .Type;
            }
            else if (
                methodName == "Except"
                && memberAccess.Name is GenericNameSyntax exceptGen
                && exceptGen.TypeArgumentList.Arguments.Count > 0
            )
            {
                var excluded = ctx.SemanticModel
                    .GetTypeInfo(exceptGen.TypeArgumentList.Arguments[0])
                    .Type;
                if (excluded is not null)
                    excludedTypes.Add(excluded);
            }

            current = next;
        }

        if (interceptorType is null)
        {
            // Check the innermost invocation (AddInterceptor as the first call in a chain).
            if (
                current.Expression
                    is MemberAccessExpressionSyntax
                    {
                        Name: GenericNameSyntax { Identifier.ValueText: "AddInterceptor" } innerGen
                    }
                && innerGen.TypeArgumentList.Arguments.Count > 0
            )
            {
                interceptorType = ctx.SemanticModel
                    .GetTypeInfo(innerGen.TypeArgumentList.Arguments[0])
                    .Type;
            }
        }

        if (interceptorType is null)
            return null;

        return new GlobalInterceptorInfo(
            interceptorType,
            namespaceFilter,
            interfaceFilter,
            excludedTypes.Count > 0 ? excludedTypes : null
        );
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
                    existing
                        .InterceptorTypes
                        .Concat(info.InterceptorTypes)
                        .Distinct(SymbolEqualityComparer.Default)
                        .OfType<ITypeSymbol>()
                        .ToList(),
                    existing
                        .WithoutInterceptorTypes
                        .Concat(info.WithoutInterceptorTypes)
                        .Distinct(SymbolEqualityComparer.Default)
                        .OfType<ITypeSymbol>()
                        .ToList(),
                    existing.WithoutInterceptors || info.WithoutInterceptors
                );
            }
            else
            {
                perServiceMap[key] = info;
            }
        }
        var deduped = perServiceMap.Values.ToList();
        if (deduped.Count == 0)
            return;

        var sb = new StringBuilder();
        var regSb = new StringBuilder();

        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine($"namespace {PicoAopNames.GeneratedNamespace};");
        sb.AppendLine();
        sb.AppendLine("using PicoDI;");
        sb.AppendLine("using PicoDI.Abs;");
        sb.AppendLine("using PicoAop.Abs;");
        sb.AppendLine();

        foreach (var info in deduped)
        {
            if (info.ServiceType is not INamedTypeSymbol serviceType)
                continue;

            // Merge global interceptors (matching) with per-service interceptors.
            // Filter out any per-service interceptors that appear in the without-list.
            var interceptorList = new List<ITypeSymbol>(
                info.InterceptorTypes.Where(
                    t => !info.WithoutInterceptorTypes.Contains(t, SymbolEqualityComparer.Default)
                )
            );

            // Add matching global interceptors (outermost first)
            var globalMatches = new List<ITypeSymbol>();
            foreach (var g in globals.OfType<GlobalInterceptorInfo>())
            {
                // PICO011: validate interface filter type is actually an interface
                if (
                    g.InterfaceFilter is INamedTypeSymbol ifaceSym
                    && ifaceSym.TypeKind != TypeKind.Interface
                )
                {
                    spc.ReportDiagnostic(
                        Diagnostic.Create(
                            InterceptorDiagParams.FilterRequiresInterface,
                            Location.None,
                            g.InterfaceFilter.ToDisplayString()
                        )
                    );
                    continue;
                }

                if (
                    MatchesGlobalFilter(serviceType, g)
                    && !info.WithoutInterceptorTypes.Contains(
                        g.InterceptorType,
                        SymbolEqualityComparer.Default
                    )
                )
                {
                    globalMatches.Add(g.InterceptorType);
                }
            }

            if (info.WithoutInterceptors)
            {
                globalMatches.Clear();
                interceptorList.Clear();
            }

            // Globals first (outer), per-service second (inner)
            interceptorList.InsertRange(0, globalMatches);

            // PICO013: check for conflicting global+per-service declarations
            foreach (var g in globalMatches)
            {
                if (info.WithoutInterceptorTypes.Contains(g, SymbolEqualityComparer.Default))
                {
                    spc.ReportDiagnostic(
                        Diagnostic.Create(
                            InterceptorDiagParams.ConflictingInterceptorDeclaration,
                            Location.None,
                            g.ToDisplayString(),
                            serviceType.ToDisplayString()
                        )
                    );
                }
            }

            if (interceptorList.Count == 0)
            {
                // PICO012: warn that no interceptors matched
                spc.ReportDiagnostic(
                    Diagnostic.Create(
                        InterceptorDiagParams.ZeroInterceptorsMatched,
                        Location.None,
                        serviceType.ToDisplayString()
                    )
                );
                continue;
            }

            for (var i = 0; i < interceptorList.Count; i++)
            {
                if (interceptorList[i] is not INamedTypeSymbol interceptorNamed)
                    continue;

                if (!ImplementsIInterceptor(interceptorNamed))
                {
                    spc.ReportDiagnostic(
                        Diagnostic.Create(
                            InterceptorDiagParams.InterceptorTypeMismatch,
                            Location.None,
                            interceptorNamed.Name,
                            "IInterceptor"
                        )
                    );
                    continue;
                }

                var isLast = i == interceptorList.Count - 1;
                EmitInvocationStruct(sb, serviceType, interceptorNamed);
                EmitDecoratorClass(
                    sb,
                    serviceType,
                    info.ImplType,
                    interceptorNamed,
                    interceptorList,
                    i,
                    isLast
                );
                EmitDiRegistration(regSb, serviceType, info.ImplType, interceptorNamed, isLast);
            }
        }

        sb.AppendLine("internal static class GeneratedInterceptorRegistrations");
        sb.AppendLine("{");
        sb.AppendLine("    internal static void Configure(SvcContainer container)");
        sb.AppendLine("    {");
        sb.Append(regSb);
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("internal static class AutoRegisterInterceptorConfigurator");
        sb.AppendLine("{");
        sb.AppendLine("    [global::System.Runtime.CompilerServices.ModuleInitializer]");
        sb.AppendLine("    internal static void Init()");
        sb.AppendLine("    {");
        sb.AppendLine("        SvcContainerAutoConfiguration.RegisterConfigurator(");
        sb.AppendLine("            \"intercepted::PicoAop\",");
        sb.AppendLine(
            "            static container => GeneratedInterceptorRegistrations.Configure((SvcContainer)container));"
        );
        sb.AppendLine("    }");
        sb.AppendLine("}");

        spc.AddSource("InterceptorRegistrations.g.cs", sb.ToString());
    }

    /// <summary>
    /// Generates the invocation struct for a single method. Currently does NOT support:
    /// - <c>ref</c> / <c>out</c> / <c>in</c> parameters (would require parameter modifiers)
    /// - Generic methods (type parameter substitution not implemented)
    /// These are future enhancements tracked in the AOP roadmap.
    /// </summary>
    private static void EmitInvocationStruct(
        StringBuilder sb,
        INamedTypeSymbol serviceType,
        INamedTypeSymbol interceptorType
    )
    {
        var safeSvc = Sanitize(serviceType.ToDisplayString());
        var safeInt = Sanitize(interceptorType.Name);

        foreach (
            var method in serviceType
                .GetMembers()
                .OfType<IMethodSymbol>()
                .Where(m => m.MethodKind == MethodKind.Ordinary)
        )
        {
            var retType = method.ReturnType;
            var resultName = retType
                is INamedTypeSymbol { MetadataName: "ValueTask`1" or "Task`1" } t
                ? t.TypeArguments[0].ToDisplayString()
                : retType.SpecialType == SpecialType.System_Void
                || retType is INamedTypeSymbol { MetadataName: "ValueTask" or "Task" }
                    ? PicoAopNames.VoidResultFull
                    : retType.ToDisplayString();

            var structName = $"{safeSvc}_{safeInt}_{method.Name}_Invocation";
            var svcName = serviceType.ToDisplayString();
            var paramList = method.Parameters.ToList();

            sb.AppendLine($"internal struct {structName} : IInvocation<{resultName}>");
            sb.AppendLine("{");
            sb.AppendLine($"    private readonly {svcName} _target;");
            foreach (var p in paramList)
                sb.AppendLine($"    private readonly {p.Type.ToDisplayString()} _{p.Name};");

            sb.AppendLine($"    public string MethodName => \"{method.Name}\";");
            sb.AppendLine($"    public System.Type ServiceType => typeof({svcName});");
            sb.AppendLine("    public ISvcScope? Scope { get; }");
            sb.AppendLine($"    public {resultName} Result {{ get; set; }}");
            sb.AppendLine();

            var paramDecl =
                paramList.Count > 0
                    ? ", "
                        + string.Join(
                            ", ",
                            paramList.Select(p => $"{p.Type.ToDisplayString()} {p.Name}")
                        )
                    : "";
            sb.AppendLine(
                $"    public {structName}({svcName} target{paramDecl}, ISvcScope? scope)"
            );
            sb.AppendLine("    {");
            sb.AppendLine("        _target = target;");
            foreach (var p in paramList)
                sb.AppendLine($"        _{p.Name} = {p.Name};");
            sb.AppendLine("        Scope = scope;");
            sb.AppendLine("        Result = default!;");
            sb.AppendLine("    }");
            sb.AppendLine();

            var paramArgs = string.Join(", ", paramList.Select(p => $"_{p.Name}"));
            // For async return types, InvokeTarget needs the original return type
            // (e.g. Task<int>), while InvokeTargetAsync uses the unwrapped type.
            var isAsyncReturn =
                retType
                    is INamedTypeSymbol
                    {
                        MetadataName: "ValueTask`1" or "Task`1" or "ValueTask" or "Task"
                    };
            var isVoidAsync = retType is INamedTypeSymbol { MetadataName: "ValueTask" or "Task" };
            var invokeTargetReturnType = isAsyncReturn ? retType.ToDisplayString() : resultName;
            sb.AppendLine(
                $"    public {invokeTargetReturnType} InvokeTarget() => _target.{method.Name}({paramArgs});"
            );

            if (isAsyncReturn)
            {
                // InvokeTargetAsync must return an awaitable type.
                // For ValueTask<T>/Task<T>: returns ValueTask<T>.
                // For ValueTask/Task: returns ValueTask.
                var asyncReturn = isVoidAsync
                    ? "global::System.Threading.Tasks.ValueTask"
                    : $"global::System.Threading.Tasks.ValueTask<{resultName}>";
                sb.AppendLine(
                    $"    public async {asyncReturn} InvokeTargetAsync() => await _target.{method.Name}({paramArgs});"
                );
            }

            sb.AppendLine("}");
            sb.AppendLine();
        }
    }

    private static void EmitDecoratorClass(
        StringBuilder sb,
        INamedTypeSymbol serviceType,
        ITypeSymbol? implType,
        INamedTypeSymbol interceptorType,
        IReadOnlyList<ITypeSymbol> allInterceptors,
        int index,
        bool isLast
    )
    {
        var safeSvc = Sanitize(serviceType.ToDisplayString());
        var safeInt = Sanitize(interceptorType.Name);
        var className = $"{safeSvc}_{safeInt}Decorator";
        var svcName = serviceType.ToDisplayString();
        var intName = interceptorType.ToDisplayString();
        var innerTypeName = isLast
            ? (implType?.ToDisplayString() ?? svcName)
            : Sanitize(serviceType.ToDisplayString())
                + "_"
                + Sanitize(((INamedTypeSymbol)allInterceptors[index + 1]).Name)
                + "Decorator";

        sb.AppendLine($"sealed class {className} : {svcName}");
        sb.AppendLine("{");
        sb.AppendLine($"    private readonly {svcName} _inner;");
        sb.AppendLine($"    private readonly {intName} _i0;");
        sb.AppendLine();
        sb.AppendLine($"    public {className}({svcName} inner, {intName} i0)");
        sb.AppendLine("    {");
        sb.AppendLine("        _inner = inner;");
        sb.AppendLine("        _i0 = i0;");
        sb.AppendLine("    }");
        sb.AppendLine();

        foreach (
            var method in serviceType
                .GetMembers()
                .OfType<IMethodSymbol>()
                .Where(m => m.MethodKind == MethodKind.Ordinary)
        )
        {
            var retType = method.ReturnType.ToDisplayString();
            var isVoidTask =
                method.ReturnType is INamedTypeSymbol { MetadataName: "ValueTask" or "Task" };
            var isTaskOf =
                method.ReturnType is INamedTypeSymbol { MetadataName: "ValueTask`1" or "Task`1" };
            var isSystemTask =
                method.ReturnType is INamedTypeSymbol { MetadataName: "Task" or "Task`1" };
            var isVoid = isVoidTask || method.ReturnType.SpecialType == SpecialType.System_Void;

            var paramDecl = string.Join(
                ", ",
                method.Parameters.Select(p => $"{p.Type.ToDisplayString()} {p.Name}")
            );
            var paramArgs = method.Parameters.Any()
                ? ", " + string.Join(", ", method.Parameters.Select(p => p.Name))
                : "";
            var structRef = $"{safeSvc}_{safeInt}_{method.Name}_Invocation";

            sb.AppendLine($"    public {retType} {method.Name}({paramDecl})");
            sb.AppendLine("    {");
            // Note: scope is always null in the generated decorator because interceptors
            // should obtain any required services through constructor injection (DI).
            // IInvocation.Scope is provided for advanced scenarios where interception-time
            // resolution is unavoidable; it is the consumer's responsibility to supply a
            // scope when constructing the invocation manually.
            sb.AppendLine($"        var inv = new {structRef}(_inner{paramArgs}, scope: null);");
            if (isVoidTask)
            {
                if (isSystemTask)
                    sb.AppendLine(
                        "        return _i0.InvokeAsyncVoid(inv, async _ => { await inv.InvokeTargetAsync(); }).AsTask();"
                    );
                else
                    sb.AppendLine(
                        "        return _i0.InvokeAsyncVoid(inv, async _ => { await inv.InvokeTargetAsync(); });"
                    );
            }
            else if (isTaskOf)
            {
                if (isSystemTask)
                    sb.AppendLine(
                        "        return _i0.InvokeAsync(inv, async _ => await inv.InvokeTargetAsync()).AsTask();"
                    );
                else
                    sb.AppendLine(
                        "        return _i0.InvokeAsync(inv, async _ => await inv.InvokeTargetAsync());"
                    );
            }
            else if (isVoid)
                sb.AppendLine("        _i0.InvokeVoid(inv, _ => inv.InvokeTarget());");
            else
                sb.AppendLine("        return _i0.Invoke(inv, _ => inv.InvokeTarget());");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        sb.AppendLine("}");
        sb.AppendLine();
    }

    /// <summary>
    /// Produces a legal C# identifier from a type name by replacing special characters
    /// with underscores. Two distinct types may produce the same sanitized name
    /// (e.g. <c>Dictionary_A_B_C</c> and <c>Dictionary_A_B_C</c> from different
    /// generic parameter combinations). This is expected to be extremely rare in
    /// practice. When it occurs, the second type silently falls back to manual
    /// decorator instantiation.
    /// </summary>
    private static string Sanitize(string name)
    {
        return name.Replace("<", "_")
            .Replace(">", "_")
            .Replace(",", "_")
            .Replace(".", "_")
            .Replace(" ", "");
    }

    private static void EmitDiRegistration(
        StringBuilder sb,
        INamedTypeSymbol serviceType,
        ITypeSymbol? implType,
        INamedTypeSymbol interceptorType,
        bool isLast
    )
    {
        // implType may be null for single-type-arg registrations like Register<Greeter>(lifetime).
        // In that case, the service type IS the implementation type.
        var effectiveImplType = implType ?? serviceType;

        var safeSvc = Sanitize(serviceType.ToDisplayString());
        var safeInt = Sanitize(interceptorType.Name);
        var decoratorClass = $"{safeSvc}_{safeInt}Decorator";
        var svcName = serviceType.ToDisplayString();
        var intName = interceptorType.ToDisplayString();
        var implName = effectiveImplType.ToDisplayString();
        var resolveType = isLast ? implName : svcName;

        sb.AppendLine($"        container.Register<{svcName}>(scope =>");
        sb.AppendLine("        {");
        sb.AppendLine($"            var inner = scope.GetService<{resolveType}>();");
        sb.AppendLine($"            var i0 = scope.GetService<{intName}>();");
        sb.AppendLine($"            return new {decoratorClass}(inner, i0);");
        sb.AppendLine("        }, SvcLifetime.Scoped);");
        sb.AppendLine();
    }

    private static bool MatchesGlobalFilter(
        INamedTypeSymbol serviceType,
        GlobalInterceptorInfo filter
    )
    {
        // Excluded?
        if (
            filter.ExcludedTypes?.Any(e => SymbolEqualityComparer.Default.Equals(e, serviceType))
            == true
        )
            return false;

        // Namespace filter (prefix match so "MyApp" matches "MyApp.Services" etc.)
        if (filter.NamespaceFilter is not null)
        {
            var ns = serviceType.ContainingNamespace?.ToDisplayString() ?? "";
            if (
                !ns.StartsWith(filter.NamespaceFilter, StringComparison.Ordinal)
                && ns != filter.NamespaceFilter
            )
                return false;
        }

        // Interface filter
        if (filter.InterfaceFilter is not null)
        {
            if (
                !serviceType
                    .AllInterfaces
                    .Any(i => SymbolEqualityComparer.Default.Equals(i, filter.InterfaceFilter))
            )
                return false;
        }

        return true;
    }

    private static bool ImplementsIInterceptor(INamedTypeSymbol type)
    {
        foreach (var iface in type.AllInterfaces)
        {
            if (iface.ToDisplayString() == PicoAopNames.IInterceptorFull)
                return true;
        }

        // Walk base type chain for InterceptorBase inheritance.
        for (var baseType = type.BaseType; baseType is not null; baseType = baseType.BaseType)
        {
            if (baseType.ToDisplayString() == PicoAopNames.InterceptorBaseFull)
                return true;
        }

        return type.ToDisplayString() == PicoAopNames.IInterceptorFull;
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
        List<ITypeSymbol>? ExcludedTypes = null
    );
}
