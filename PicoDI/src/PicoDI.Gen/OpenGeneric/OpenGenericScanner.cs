namespace PicoDI.Gen.OpenGeneric;

/// <summary>
/// Scans for open generic registration invocations in the source code.
/// </summary>
internal sealed class OpenGenericScanner
{
    /// <summary>
    /// Scans an invocation expression to determine if it's an open generic registration.
    /// </summary>
    /// <param name="context">The generator syntax context containing the node to scan.</param>
    /// <returns>An <see cref="OpenGenericInvocationCandidate"/> if the invocation is an open generic registration, otherwise null.</returns>
    public OpenGenericInvocationCandidate? ScanOpenGenericInvocation(GeneratorSyntaxContext context)
    {
        if (context.Node is not InvocationExpressionSyntax invocation)
            return null;

        var semanticModel = context.SemanticModel;
        var symbolInfo = semanticModel.GetSymbolInfo(invocation);

        if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
            return null;

        if (!PicoDiNames.IsPicoDiMethod(methodSymbol))
            return null;

        var methodName = methodSymbol.Name;
        if (!PicoDiNames.RegisterMethodNames.Contains(methodName))
            return null;

        foreach (var arg in invocation.ArgumentList.Arguments)
        {
            if (arg.Expression is not TypeOfExpressionSyntax typeOfExpr)
                continue;

            var typeSymbol = semanticModel.GetTypeInfo(typeOfExpr.Type).Type;
            if (typeSymbol is INamedTypeSymbol { IsUnboundGenericType: true })
                return new OpenGenericInvocationCandidate(invocation, semanticModel);
        }

        return null;
    }

    /// <summary>
    /// Analyzes an open generic invocation to extract registration information.
    /// </summary>
    /// <param name="invocation">The invocation expression syntax.</param>
    /// <param name="semanticModel">The semantic model for the syntax tree.</param>
    /// <returns>An <see cref="OpenGenericSemanticOutcome"/> containing registration and diagnostic information.</returns>
    public OpenGenericSemanticOutcome AnalyzeOpenGenericInvocation(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel
    )
    {
        var methodName = invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
            _ => null
        };

        if (methodName is null || !PicoDiNames.RegisterMethodNames.Contains(methodName))
            return new OpenGenericSemanticOutcome(null, null);

        var args = invocation.ArgumentList.Arguments;
        if (args.Count < 1)
            return new OpenGenericSemanticOutcome(null, null);

        ITypeSymbol? serviceType = null;
        ITypeSymbol? implementationType = null;
        Location? implementationLocation = null;
        var lifetime = PicoDiNames.Singleton;

        foreach (var arg in args)
        {
            if (arg.Expression is TypeOfExpressionSyntax typeOfExpr)
            {
                var typeSymbol = semanticModel.GetTypeInfo(typeOfExpr.Type).Type;
                if (typeSymbol is not INamedTypeSymbol { IsUnboundGenericType: true } unboundType)
                    continue;

                if (serviceType is null)
                    serviceType = unboundType;
                else
                {
                    implementationType = unboundType;
                    implementationLocation = typeOfExpr.Type.GetLocation();
                }
            }
            else
            {
                var argType = semanticModel.GetTypeInfo(arg.Expression).Type;
                if (argType?.Name == PicoDiNames.SvcLifetime)
                    lifetime = PicoDiNames.ParseLifetimeFromExpression(arg.Expression.ToString());
            }
        }

        lifetime = PicoDiNames.InferLifetimeFromMethodName(methodName, lifetime);

        if (
            serviceType is not INamedTypeSymbol namedServiceType
            || implementationType is not INamedTypeSymbol namedImplementationType
        )
            return new OpenGenericSemanticOutcome(null, null);

        var openImplementationType = namedImplementationType.OriginalDefinition;
        var activation = ImplementationActivationAnalyzer.Analyze(openImplementationType);

        if (!activation.IsValid)
        {
            return new OpenGenericSemanticOutcome(
                Registration: null,
                Diagnostic: activation.CreateDiagnostic(implementationLocation)
            );
        }

        return new OpenGenericSemanticOutcome(
            Registration: new OpenGenericRegistration(
                namedServiceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                namedImplementationType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                lifetime,
                namedServiceType.TypeParameters.Length,
                openImplementationType.TypeParameters.Select(tp => tp.Name).ToImmutableArray(),
                activation.ConstructorParameterTypes
            ),
            Diagnostic: null
        );
    }
}
