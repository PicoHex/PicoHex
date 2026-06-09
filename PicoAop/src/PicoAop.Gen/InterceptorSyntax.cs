namespace PicoAop.Gen;

internal static class InterceptorSyntax
{
    public static bool IsInterceptByInvocation(SyntaxNode node) => node switch
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

    public static bool IsAddInterceptorInvocation(SyntaxNode node) => node switch
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

    public static InterceptionInfo? ExtractInterceptionInfo(GeneratorSyntaxContext ctx, CancellationToken ct)
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

        // Walk up: Register*().InterceptBy<T>() 
        // The InterceptBy's receiver is the Register*() call
        if (invocation.Expression is not MemberAccessExpressionSyntax outerMember)
            return null;
        if (outerMember.Expression is not InvocationExpressionSyntax registerInvocation)
            return null;

        var registerSymbol = semanticModel.GetSymbolInfo(registerInvocation, ct).Symbol as IMethodSymbol;
        if (registerSymbol?.TypeArguments.Length < 1)
            return null;

        var serviceType = registerSymbol.TypeArguments[0];
        var implType = registerSymbol.TypeArguments.Length > 1 ? registerSymbol.TypeArguments[1] : null;

        return new InterceptionInfo(serviceType, interceptorType, implType, HasMultipleRegisters: false);
    }

    public static GlobalInterceptorInfo? ExtractGlobalInterceptorInfo(GeneratorSyntaxContext ctx, CancellationToken ct)
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
