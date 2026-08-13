namespace PicoDI.Gen;

internal static partial class InterceptionHelper
{
    public const string InterceptBy = "InterceptBy";
    public const string AddInterceptor = "AddInterceptor";
    public const string WithoutInterceptor = "WithoutInterceptor";
    public const string WithoutInterceptors = "WithoutInterceptors";

    /// <summary>
    /// Fast syntax check for the OUTERMOST call of an interception chain:
    /// <c>InterceptBy&lt;T&gt;()</c>, <c>WithoutInterceptor&lt;T&gt;()</c> and
    /// <c>WithoutInterceptors()</c>. Inner chain calls are excluded — they
    /// are projections of the same chain and would double-count interceptors
    /// during merging.
    /// </summary>
    public static bool IsInterceptionChainInvocation(SyntaxNode node)
    {
        if (
            node
            is not InvocationExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax { Name: GenericNameSyntax gn }
            }
        )
            return false;

        if (
            gn.Identifier.ValueText
            is not (InterceptBy or WithoutInterceptor or WithoutInterceptors)
        )
            return false;

        // Outermost call only: its parent must not be a member access whose
        // expression is this invocation (i.e. another chain call follows).
        return node.Parent
            is not MemberAccessExpressionSyntax { Expression: InvocationExpressionSyntax };
    }

    /// <summary>
    /// Walks a full interception chain and computes its final state:
    /// the interceptors that survive any <c>WithoutInterceptor&lt;T&gt;()</c>
    /// exclusions, plus a suppression flag for <c>WithoutInterceptors()</c>.
    /// </summary>
    public static InterceptionChainInfo? ExtractInterceptionChainInfo(
        GeneratorSyntaxContext ctx,
        CancellationToken ct
    )
    {
        var invocation = (InvocationExpressionSyntax)ctx.Node;
        var semanticModel = ctx.SemanticModel;

        if (semanticModel.GetSymbolInfo(invocation, ct).Symbol is not IMethodSymbol methodSymbol)
            return null;

        var chainName = methodSymbol.Name;
        if (chainName is not (InterceptBy or WithoutInterceptor or WithoutInterceptors))
            return null;

        var comparer = SymbolEqualityComparer.Default;
        var interceptors = new List<ITypeSymbol>();
        var removed = new List<ITypeSymbol>();
        var suppressed = false;

        // Walk up through the chain from the outermost call:
        // Register*().InterceptBy<A>().InterceptBy<B>().WithoutInterceptor<A>()
        // Exclusions are processed before the adds they apply to because the
        // walk runs in reverse source order. The loop consumes ONE pending
        // operation per iteration — seeded with the starting call itself,
        // then fed by each inner chain call.
        var pendingOp = chainName;
        ITypeSymbol? pendingArg =
            methodSymbol.TypeArguments.Length >= 1 ? methodSymbol.TypeArguments[0] : null;
        var current = invocation;
        while (true)
        {
            switch (pendingOp)
            {
                case InterceptBy:
                    if (pendingArg is null)
                        return null;
                    if (!removed.Any(r => comparer.Equals(r, pendingArg)))
                        interceptors.Insert(0, pendingArg);
                    break;

                case WithoutInterceptor:
                    if (pendingArg is null)
                        return null;
                    removed.Add(pendingArg);
                    break;

                case WithoutInterceptors:
                    suppressed = true;
                    break;
            }

            if (current.Expression is not MemberAccessExpressionSyntax outerMember)
                return null;
            if (outerMember.Expression is not InvocationExpressionSyntax innerInvocation)
                return null;

            var innerSymbol =
                semanticModel.GetSymbolInfo(innerInvocation, ct).Symbol as IMethodSymbol;
            if (innerSymbol == null)
                return null;

            if (innerSymbol.Name is InterceptBy or WithoutInterceptor or WithoutInterceptors)
            {
                pendingOp = innerSymbol.Name;
                pendingArg =
                    innerSymbol.TypeArguments.Length >= 1 ? innerSymbol.TypeArguments[0] : null;
                current = innerInvocation;
                continue;
            }

            // Found Register*() call
            if (innerSymbol.TypeArguments.Length < 1)
                return null;

            var serviceType = innerSymbol.TypeArguments[0];
            var implType =
                innerSymbol.TypeArguments.Length > 1 ? innerSymbol.TypeArguments[1] : serviceType;

            // Prefer an explicit SvcLifetime ARGUMENT (e.g.
            // Register<ISvc, Impl>(SvcLifetime.Transient)) over method-name
            // inference — "Register" alone would otherwise default to
            // Singleton and silently change the requested lifetime.
            var lifetime = PicoDiNames.InferLifetimeFromMethodName(innerSymbol.Name);
            foreach (var arg in innerInvocation.ArgumentList.Arguments)
            {
                if (
                    arg.Expression is MemberAccessExpressionSyntax memberAccess
                    && semanticModel.GetTypeInfo(arg.Expression).Type?.Name
                        == PicoDiNames.SvcLifetime
                )
                {
                    lifetime = PicoDiNames.ParseLifetimeFromExpression(
                        memberAccess.Name.Identifier.ValueText
                    );
                    break;
                }
            }

            return new InterceptionChainInfo(
                serviceType,
                implType,
                lifetime,
                [.. interceptors],
                suppressed
            );
        }
    }
}

internal record InterceptionChainInfo(
    ITypeSymbol ServiceType,
    ITypeSymbol ImplType,
    string Lifetime,
    ImmutableArray<ITypeSymbol> Interceptors,
    bool Suppressed
);

internal record GlobalInterceptorInfo(ITypeSymbol InterceptorType, ITypeSymbol? InterfaceFilter);

internal static class InterceptionHelperGlobals
{
    private const string AddInt = "AddInterceptor";

    public static bool IsAddInterceptorInvocation(SyntaxNode node) =>
        node switch
        {
            InvocationExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax
                {
                    Name: GenericNameSyntax { Identifier.ValueText: AddInt }
                }
            } => true,
            _ => false,
        };

    public static GlobalInterceptorInfo? ExtractGlobalInterceptorInfo(
        GeneratorSyntaxContext ctx,
        CancellationToken ct
    )
    {
        var invocation = (InvocationExpressionSyntax)ctx.Node;
        var semanticModel = ctx.SemanticModel;

        if (semanticModel.GetSymbolInfo(invocation, ct).Symbol is not IMethodSymbol methodSymbol)
            return null;
        if (methodSymbol.Name != AddInt)
            return null;
        if (methodSymbol.TypeArguments.Length != 1)
            return null;

        return new GlobalInterceptorInfo(methodSymbol.TypeArguments[0], null);
    }
}
