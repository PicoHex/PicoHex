namespace PicoAop.Gen;

internal static class InterceptorSyntax
{
    /// <summary>
    /// Fast syntax check for the OUTERMOST call of an interception chain:
    /// <c>InterceptBy&lt;T&gt;()</c>, <c>WithoutInterceptor&lt;T&gt;()</c> and
    /// <c>WithoutInterceptors()</c>. Inner chain calls are excluded — they
    /// are projections of the same chain and would double-count interceptors.
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
            is not (
                PicoAopNames.InterceptBy
                or PicoAopNames.WithoutInterceptor
                or PicoAopNames.WithoutInterceptors
            )
        )
            return false;

        // Outermost call only: its parent must not be a member access whose
        // expression is this invocation (i.e. another chain call follows).
        return node.Parent
            is not MemberAccessExpressionSyntax { Expression: InvocationExpressionSyntax };
    }

    public static bool IsAddInterceptorInvocation(SyntaxNode node) =>
        node switch
        {
            InvocationExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax
                {
                    Name: GenericNameSyntax { Identifier.ValueText: PicoAopNames.AddInterceptor }
                }
            } => true,
            _ => false,
        };

    /// <summary>
    /// Walks a full interception chain and computes its final state: the
    /// interceptors that survive any <c>WithoutInterceptor&lt;T&gt;()</c>
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
        if (
            chainName
            is not (
                PicoAopNames.InterceptBy
                or PicoAopNames.WithoutInterceptor
                or PicoAopNames.WithoutInterceptors
            )
        )
            return null;

        var comparer = SymbolEqualityComparer.Default;
        var interceptors = new List<ITypeSymbol>();
        var removed = new List<ITypeSymbol>();
        var suppressed = false;

        // Walk from the outermost call backwards. Exclusions are processed
        // before the adds they apply to because the walk runs in reverse
        // source order. The loop consumes ONE pending operation per
        // iteration — seeded with the starting call itself, then fed by each
        // inner chain call.
        var pendingOp = chainName;
        ITypeSymbol? pendingArg =
            methodSymbol.TypeArguments.Length >= 1 ? methodSymbol.TypeArguments[0] : null;
        var current = invocation;
        while (true)
        {
            switch (pendingOp)
            {
                case PicoAopNames.InterceptBy:
                    if (pendingArg is null)
                        return null;
                    if (!removed.Any(r => comparer.Equals(r, pendingArg)))
                        interceptors.Insert(0, pendingArg);
                    break;

                case PicoAopNames.WithoutInterceptor:
                    if (pendingArg is null)
                        return null;
                    removed.Add(pendingArg);
                    break;

                case PicoAopNames.WithoutInterceptors:
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

            if (
                innerSymbol.Name
                is PicoAopNames.InterceptBy
                    or PicoAopNames.WithoutInterceptor
                    or PicoAopNames.WithoutInterceptors
            )
            {
                pendingOp = innerSymbol.Name;
                pendingArg =
                    innerSymbol.TypeArguments.Length >= 1 ? innerSymbol.TypeArguments[0] : null;
                current = innerInvocation;
                continue;
            }

            // Found Register*() call — extract the service type.
            if (innerSymbol.TypeArguments.Length < 1)
                return null;

            return new InterceptionChainInfo(
                innerSymbol.TypeArguments[0],
                [.. interceptors],
                suppressed
            );
        }
    }

    public static GlobalInterceptorInfo? ExtractGlobalInterceptorInfo(
        GeneratorSyntaxContext ctx,
        CancellationToken ct
    )
    {
        var invocation = (InvocationExpressionSyntax)ctx.Node;
        var semanticModel = ctx.SemanticModel;

        if (semanticModel.GetSymbolInfo(invocation, ct).Symbol is not IMethodSymbol methodSymbol)
            return null;
        if (methodSymbol.Name != PicoAopNames.AddInterceptor)
            return null;
        if (methodSymbol.TypeArguments.Length != 1)
            return null;

        return new GlobalInterceptorInfo(methodSymbol.TypeArguments[0]);
    }
}
