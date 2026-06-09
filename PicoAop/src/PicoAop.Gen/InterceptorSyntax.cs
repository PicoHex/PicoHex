namespace PicoAop.Gen;

internal static class InterceptorSyntax
{
    public static bool IsInterceptByInvocation(SyntaxNode node) =>
        node switch
        {
            InvocationExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax
                {
                    Name: GenericNameSyntax { Identifier.ValueText: PicoAopNames.InterceptBy }
                }
            } => true,
            _ => false,
        };

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

    public static InterceptionInfo? ExtractInterceptionInfo(
        GeneratorSyntaxContext ctx,
        CancellationToken ct
    )
    {
        var invocation = (InvocationExpressionSyntax)ctx.Node;
        var semanticModel = ctx.SemanticModel;

        if (semanticModel.GetSymbolInfo(invocation, ct).Symbol is not IMethodSymbol methodSymbol)
            return null;
        if (methodSymbol.Name != PicoAopNames.InterceptBy)
            return null;
        if (methodSymbol.TypeArguments.Length != 1)
            return null;

        var interceptorType = methodSymbol.TypeArguments[0];

        // Walk up through InterceptBy chain: Register*().InterceptBy<A>().InterceptBy<B>()
        // For InterceptBy<B>, we need to skip past InterceptBy<A> to find Register*().
        var current = invocation;
        while (true)
        {
            if (current.Expression is not MemberAccessExpressionSyntax outerMember)
                return null;
            if (outerMember.Expression is not InvocationExpressionSyntax innerInvocation)
                return null;

            var innerSymbol =
                semanticModel.GetSymbolInfo(innerInvocation, ct).Symbol as IMethodSymbol;
            if (innerSymbol == null)
                return null;

            // If the inner call is another InterceptBy, walk past it
            if (innerSymbol.Name == PicoAopNames.InterceptBy)
            {
                current = innerInvocation;
                continue;
            }

            // Found Register*() call — extract types
            if (innerSymbol.TypeArguments.Length < 1)
                return null;

            var serviceType = innerSymbol.TypeArguments[0];
            var implType =
                innerSymbol.TypeArguments.Length > 1 ? innerSymbol.TypeArguments[1] : null;

            return new InterceptionInfo(
                serviceType,
                interceptorType,
                implType,
                HasMultipleRegisters: false
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

        return new GlobalInterceptorInfo(methodSymbol.TypeArguments[0], null);
    }
}
