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

        // Walk up: Register*().InterceptBy<T>()
        if (invocation.Expression is not MemberAccessExpressionSyntax outerMember)
            return null;
        if (outerMember.Expression is not InvocationExpressionSyntax registerInvocation)
            return null;

        var registerSymbol = semanticModel.GetSymbolInfo(registerInvocation, ct).Symbol as IMethodSymbol;
        if (registerSymbol?.TypeArguments.Length < 1)
            return null;

        var serviceType = registerSymbol.TypeArguments[0];
        var implType = registerSymbol.TypeArguments.Length > 1 ? registerSymbol.TypeArguments[1] : serviceType;

        return new InterceptionInfo(serviceType, implType, interceptorType);
    }
}

internal record InterceptionInfo(
    ITypeSymbol ServiceType,
    ITypeSymbol ImplType,
    ITypeSymbol InterceptorType);
