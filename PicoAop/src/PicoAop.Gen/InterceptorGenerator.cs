namespace PicoAop.Gen;

[Generator(LanguageNames.CSharp)]
public sealed partial class InterceptorGenerator : IIncrementalGenerator
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

    private static void GenerateInterceptorRegistrations(
        SourceProductionContext spc,
        ImmutableArray<InterceptionInfo?> perService,
        ImmutableArray<GlobalInterceptorInfo?> globals
    )
    {
        var perServiceMap = new Dictionary<string, InterceptionInfo>(StringComparer.Ordinal);
        foreach (var info in perService.OfType<InterceptionInfo>())
        {
            var key = info.ServiceType.ToDisplayString();
            if (perServiceMap.TryGetValue(key, out var existing))
            {
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

        var sb = new StringBuilder(8192);
        var regSb = new StringBuilder(4096);

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

            var interceptorList = new List<ITypeSymbol>(
                info.InterceptorTypes.Where(
                    t => !info.WithoutInterceptorTypes.Contains(t, SymbolEqualityComparer.Default)
                )
            );

            var globalMatches = new List<ITypeSymbol>();
            foreach (var g in globals.OfType<GlobalInterceptorInfo>())
            {
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

            interceptorList.InsertRange(0, globalMatches);

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
                EmitRegistration(regSb, serviceType, info.ImplType, interceptorNamed, isLast);
            }
        }

        EmitBootstrapper(sb, regSb);
        spc.AddSource("InterceptorRegistrations.g.cs", sb.ToString());
    }

    private static void EmitBootstrapper(StringBuilder sb, StringBuilder regSb)
    {
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
    }
}
