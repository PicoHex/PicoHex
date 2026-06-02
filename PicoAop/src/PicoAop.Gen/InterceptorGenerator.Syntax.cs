namespace PicoAop.Gen;

public sealed partial class InterceptorGenerator
{
    private sealed record InterceptionInfo(
        ITypeSymbol ServiceType,
        ITypeSymbol? ImplType,
        IReadOnlyList<ITypeSymbol> InterceptorTypes,
        IReadOnlyList<ITypeSymbol> WithoutInterceptorTypes,
        bool WithoutInterceptors,
        bool HasMultipleRegisters = false,
        Location? Location = null
    );

    private sealed record GlobalInterceptorInfo(
        ITypeSymbol InterceptorType,
        string? NamespaceFilter = null,
        ITypeSymbol? InterfaceFilter = null,
        List<ITypeSymbol>? ExcludedTypes = null,
        Location? Location = null
    );

    private static InterceptionInfo? ExtractInterceptionInfo(
        GeneratorSyntaxContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.Node is not InvocationExpressionSyntax invocation)
            return null;

        var semanticModel = ctx.SemanticModel;

        var current = invocation;
        InvocationExpressionSyntax? registerCall = null;
        var interceptorArgTypes = new List<ITypeSymbol>();
        var hasWithoutInterceptors = false;
        var withoutInterceptorTypes = new List<ITypeSymbol>();
        var registerCount = 0;

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
                            interceptorArgTypes.Insert(0, typeArg);
                        else
                            withoutInterceptorTypes.Add(typeArg);
                    }
                }
            }
            else if (methodName == "WithoutInterceptors")
            {
                hasWithoutInterceptors = true;
            }
            else if (methodName == "Register")
            {
                registerCount++;
            }

            current = nextInvocation;
        }

        if (
            current.Expression is MemberAccessExpressionSyntax memAccess
            && memAccess.Name.Identifier.ValueText == "Register"
        )
        {
            registerCall = current;
        }

        if (registerCall is null || interceptorArgTypes.Count == 0)
            return null;

        // The final Register call (that stopped the walk loop) was not counted
        registerCount++;

        var registerSymbol = semanticModel.GetSymbolInfo(registerCall).Symbol as IMethodSymbol;
        ITypeSymbol? serviceType = null;
        ITypeSymbol? implType = null;

        if (registerSymbol is { TypeArguments.Length: >= 1 })
        {
            serviceType = registerSymbol.TypeArguments[0];
            implType =
                registerSymbol.TypeArguments.Length >= 2 ? registerSymbol.TypeArguments[1] : null;
        }
        else if (registerCall.ArgumentList.Arguments.Count >= 2)
        {
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
            hasWithoutInterceptors,
            HasMultipleRegisters: registerCount > 1,
            Location: invocation.GetLocation()
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
            excludedTypes.Count > 0 ? excludedTypes : null,
            Location: invocation.GetLocation()
        );
    }

    private static bool MatchesGlobalFilter(
        INamedTypeSymbol serviceType,
        GlobalInterceptorInfo filter
    )
    {
        if (
            filter.ExcludedTypes?.Any(e => SymbolEqualityComparer.Default.Equals(e, serviceType))
            == true
        )
            return false;

        if (filter.NamespaceFilter is not null)
        {
            var ns = serviceType.ContainingNamespace?.ToDisplayString() ?? "";
            if (
                !ns.StartsWith(filter.NamespaceFilter, StringComparison.Ordinal)
                && ns != filter.NamespaceFilter
            )
                return false;
        }

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
}
