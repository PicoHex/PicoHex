namespace PicoDI.Gen;

internal static class InterceptionHelper
{
    public const string InterceptBy = "InterceptBy";

    public static bool IsInterceptByInvocation(SyntaxNode node) => node switch
    {
        InvocationExpressionSyntax
        {
            Expression: MemberAccessExpressionSyntax
            {
                Name: GenericNameSyntax { Identifier.ValueText: InterceptBy }
            }
        } => true,
        _ => false,
    };

    public static InterceptionInfo? ExtractInterceptionInfo(GeneratorSyntaxContext ctx, CancellationToken ct)
    {
        var invocation = (InvocationExpressionSyntax)ctx.Node;
        var semanticModel = ctx.SemanticModel;

        if (semanticModel.GetSymbolInfo(invocation, ct).Symbol is not IMethodSymbol methodSymbol)
            return null;
        if (methodSymbol.Name != InterceptBy)
            return null;
        if (methodSymbol.TypeArguments.Length != 1)
            return null;

        var interceptorType = methodSymbol.TypeArguments[0];

        // Walk up through InterceptBy chain: Register*().InterceptBy<A>().InterceptBy<B>()
        // For InterceptBy<B>, skip past InterceptBy<A> to find Register*().
        var current = invocation;
        while (true)
        {
            if (current.Expression is not MemberAccessExpressionSyntax outerMember)
                return null;
            if (outerMember.Expression is not InvocationExpressionSyntax innerInvocation)
                return null;

            var innerSymbol = semanticModel.GetSymbolInfo(innerInvocation, ct).Symbol as IMethodSymbol;
            if (innerSymbol == null)
                return null;

            // Skip past intermediate InterceptBy calls
            if (innerSymbol.Name == InterceptBy)
            {
                current = innerInvocation;
                continue;
            }

            // Found Register*() call
            if (innerSymbol.TypeArguments.Length < 1)
                return null;

            var serviceType = innerSymbol.TypeArguments[0];
            var implType = innerSymbol.TypeArguments.Length > 1 ? innerSymbol.TypeArguments[1] : serviceType;

            return new InterceptionInfo(serviceType, implType, interceptorType);
        }
    }
}

internal record InterceptionInfo(
    ITypeSymbol ServiceType,
    ITypeSymbol ImplType,
    ITypeSymbol InterceptorType);
