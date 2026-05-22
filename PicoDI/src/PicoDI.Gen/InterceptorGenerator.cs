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

        return new InterceptionInfo(
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
        if (perService.IsEmpty && globals.IsEmpty)
            return;

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("namespace PicoDI;");
        sb.AppendLine();
        sb.AppendLine("internal static class GeneratedInterceptorRegistrations");
        sb.AppendLine("{");
        sb.AppendLine("    internal static void ConfigureInterceptors(SvcContainer container)");
        sb.AppendLine("    {");
        sb.AppendLine(
            "        // Interceptor registrations will be emitted here by the source generator."
        );
        sb.AppendLine("        // For now, InterceptBy<T>() calls are collected but full code");
        sb.AppendLine("        // emission (decorator classes + invocation structs) is not yet");
        sb.AppendLine("        // implemented in this v1 stub.");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        spc.AddSource("InterceptorRegistrations.g.cs", sb.ToString());
    }

    private sealed record InterceptionInfo(
        IReadOnlyList<ITypeSymbol> InterceptorTypes,
        IReadOnlyList<ITypeSymbol> WithoutInterceptorTypes,
        bool WithoutInterceptors
    );

    private sealed record GlobalInterceptorInfo(ITypeSymbol InterceptorType);
}
