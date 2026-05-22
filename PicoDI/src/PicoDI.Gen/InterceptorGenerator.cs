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
        if (
            current.Expression is MemberAccessExpressionSyntax
            {
                Name: GenericNameSyntax { Identifier.ValueText: "Register" } registerName
            }
        )
        {
            registerCall = current;
        }

        if (registerCall is null || interceptorArgTypes.Count == 0)
            return null;

        // Extract service and implementation types from Register<T, Impl>()
        var registerSymbol = semanticModel.GetSymbolInfo(registerCall).Symbol as IMethodSymbol;
        if (registerSymbol is not { TypeArguments.Length: >= 1 })
            return null;

        var serviceType = registerSymbol.TypeArguments[0];
        var implType = registerSymbol.TypeArguments.Length >= 2
            ? registerSymbol.TypeArguments[1]
            : null;

        return new InterceptionInfo(
            serviceType, implType, interceptorArgTypes,
            withoutInterceptorTypes, hasWithoutInterceptors
        );
    }

    private static GlobalInterceptorInfo? ExtractGlobalInterceptorInfo(
        GeneratorSyntaxContext ctx,
        CancellationToken ct
    )
    {
        // Stub: detect AddInterceptor<T>().Where*() patterns
        // For v1, just detect the AddInterceptor call
        if (ctx.Node is not InvocationExpressionSyntax invocation)
            return null;

        while (
            invocation.Expression
                is MemberAccessExpressionSyntax
                {
                    Expression: InvocationExpressionSyntax next
                } memberAccess
        )
        {
            if (
                memberAccess.Name is GenericNameSyntax { Identifier.ValueText: "AddInterceptor" }
                && memberAccess.Name is GenericNameSyntax genName
                && genName.TypeArgumentList.Arguments.Count > 0
            )
            {
                var typeArg = ctx.SemanticModel
                    .GetTypeInfo(genName.TypeArgumentList.Arguments[0])
                    .Type;
                if (typeArg is not null)
                    return new GlobalInterceptorInfo(typeArg);
            }

            invocation = next;
        }

        return null;
    }

    private static void GenerateInterceptorRegistrations(
        SourceProductionContext spc,
        ImmutableArray<InterceptionInfo?> perService,
        ImmutableArray<GlobalInterceptorInfo?> globals
    )
    {
        var valid = perService.OfType<InterceptionInfo>().ToList();
        if (valid.Count == 0)
            return;

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("namespace PicoDI.Generated.Aop;");
        sb.AppendLine();
        sb.AppendLine("using PicoDI;");
        sb.AppendLine("using PicoDI.Abs;");
        sb.AppendLine();

        foreach (var info in valid)
        {
            if (info.ServiceType is not INamedTypeSymbol serviceType)
                continue;

            foreach (var interceptorType in info.InterceptorTypes)
            {
                if (interceptorType is not INamedTypeSymbol interceptorNamed)
                    continue;

                EmitInvocationStruct(sb, serviceType, interceptorNamed);
                EmitDecoratorClass(sb, serviceType, info.ImplType, interceptorNamed);
            }
        }

        sb.AppendLine("internal static class GeneratedInterceptorRegistrations");
        sb.AppendLine("{");
        sb.AppendLine(
            "    internal static void Configure(SvcContainer container) { }");
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

            var paramDecl = string.Join(", ", paramList.Select(
                p => $"{p.Type.ToDisplayString()} {p.Name}"));
            sb.AppendLine(
                $"    public {structName}({svcName} target, {paramDecl}, ISvcScope? scope)");
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
        ITypeSymbol? implType, INamedTypeSymbol interceptorType)
    {
        var safeSvc = Sanitize(serviceType.ToDisplayString());
        var safeInt = Sanitize(interceptorType.Name);
        var className = $"{safeSvc}_{safeInt}Decorator";
        var svcName = serviceType.ToDisplayString();
        var intName = interceptorType.ToDisplayString();

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
            var isVoid = method.ReturnType is INamedTypeSymbol
            {
                MetadataName: "ValueTask" or "Task"
            } || method.ReturnType.SpecialType == SpecialType.System_Void;

            var paramDecl = string.Join(", ", method.Parameters.Select(
                p => $"{p.Type.ToDisplayString()} {p.Name}"));
            var paramArgs = string.Join(", ", method.Parameters.Select(p => p.Name));
            var structRef =
                $"{safeSvc}_{safeInt}_{method.Name}_Invocation";

            sb.AppendLine(
                $"    public {retType} {method.Name}({paramDecl})");
            sb.AppendLine("    {");
            sb.AppendLine(
                $"        var inv = new {structRef}(_inner, {paramArgs}, scope: null);");
            if (isVoid)
                sb.AppendLine(
                    "        _i0.InvokeVoid(inv, _ => inv.InvokeTarget());");
            else
                sb.AppendLine(
                    "        return _i0.Invoke(inv, _ => inv.InvokeTarget());");
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

    private sealed record InterceptionInfo(
        ITypeSymbol ServiceType,
        ITypeSymbol? ImplType,
        IReadOnlyList<ITypeSymbol> InterceptorTypes,
        IReadOnlyList<ITypeSymbol> WithoutInterceptorTypes,
        bool WithoutInterceptors
    );

    private sealed record GlobalInterceptorInfo(ITypeSymbol InterceptorType);
}
