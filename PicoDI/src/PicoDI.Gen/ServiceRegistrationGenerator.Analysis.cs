namespace PicoDI.Gen;

internal static class RegistrationSemanticPipeline
{
    public static RegistrationInvocationCandidate? GetInvocationInfo(GeneratorSyntaxContext context)
    {
        if (context.Node is not InvocationExpressionSyntax invocation)
            return null;

        var semanticModel = context.SemanticModel;
        var symbolInfo = semanticModel.GetSymbolInfo(invocation);

        if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
            return null;

        if (!PicoDiNames.IsPicoDiMethod(methodSymbol))
            return null;

        return !PicoDiNames.RegisterMethodNames.Contains(methodSymbol.Name)
            ? null
            : new RegistrationInvocationCandidate(invocation, semanticModel);
    }

    public static RegistrationSemanticBatch NormalizeRegistrations(
        ImmutableArray<RegistrationInvocationCandidate?> invocations
    )
    {
        var registrations = new List<ServiceRegistration>();
        var diagnostics = new List<Diagnostic>();
        var seenRegistrations = new HashSet<ServiceRegistration>();
        var seenDiagnostics = new HashSet<Diagnostic>(DiagnosticIdentityComparer.Instance);

        foreach (var invocation in invocations)
        {
            if (invocation is not { } candidate)
                continue;

            var outcome = AnalyzeRegistration(candidate.Invocation, candidate.SemanticModel);

            if (outcome.Registration is { } registration && seenRegistrations.Add(registration))
                registrations.Add(registration);

            if (outcome.Diagnostic is { } diagnostic && seenDiagnostics.Add(diagnostic))
                diagnostics.Add(diagnostic);
        }

        return new RegistrationSemanticBatch([.. registrations], [.. diagnostics]);
    }

    private static RegistrationSemanticOutcome AnalyzeRegistration(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel
    )
    {
        var methodName = GetMethodName(invocation.Expression);

        if (methodName is null || !PicoDiNames.RegisterMethodNames.Contains(methodName))
            return new RegistrationSemanticOutcome(null, null);

        var symbolInfo = semanticModel.GetSymbolInfo(invocation);
        if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
            return new RegistrationSemanticOutcome(null, null);

        if (!PicoDiNames.IsPicoDiMethod(methodSymbol))
            return new RegistrationSemanticOutcome(null, null);

        if (IsFactoryRegistration(invocation, methodSymbol, semanticModel))
            return new RegistrationSemanticOutcome(null, null);

        ITypeSymbol? serviceType = null;
        ITypeSymbol? implementationType = null;
        var lifetime = PicoDiNames.Singleton;
        var hasFactory = false;

        var genericNameSyntax = invocation.Expression switch
        {
            MemberAccessExpressionSyntax { Name: GenericNameSyntax gn } => gn,
            GenericNameSyntax gn => gn,
            _ => null
        };

        if (genericNameSyntax?.TypeArgumentList.Arguments.Count > 0)
        {
            var typeArg = genericNameSyntax.TypeArgumentList.Arguments[0];
            serviceType = semanticModel.GetTypeInfo(typeArg).Type;

            if (genericNameSyntax.TypeArgumentList.Arguments.Count > 1)
            {
                var implTypeArg = genericNameSyntax.TypeArgumentList.Arguments[1];
                implementationType = semanticModel.GetTypeInfo(implTypeArg).Type;
            }
            else
            {
                implementationType = serviceType;
            }
        }

        foreach (var arg in invocation.ArgumentList.Arguments)
        {
            var argType = semanticModel.GetTypeInfo(arg.Expression).Type;

            if (argType is INamedTypeSymbol { Name: PicoDiNames.Func })
            {
                hasFactory = true;
            }
            else
                switch (argType?.Name)
                {
                    case PicoDiNames.SvcLifetime:
                        lifetime = PicoDiNames.ParseLifetimeFromExpression(
                            arg.Expression.ToString()
                        );
                        break;
                    case PicoDiNames.Type when arg.Expression is TypeOfExpressionSyntax typeOfExpr:
                        {
                            var typeSymbol = semanticModel.GetTypeInfo(typeOfExpr.Type).Type;
                            if (serviceType is null)
                                serviceType = typeSymbol;
                            else
                                implementationType = typeSymbol;
                            break;
                        }
                }
        }

        lifetime = PicoDiNames.InferLifetimeFromMethodName(methodName, lifetime);

        if (serviceType is null || implementationType is null)
            return new RegistrationSemanticOutcome(null, null);

        if (hasFactory)
            return new RegistrationSemanticOutcome(null, null);

        if (implementationType is not INamedTypeSymbol namedImplementationType)
            return new RegistrationSemanticOutcome(null, null);

        if (
            methodName == PicoDiNames.Register
            && genericNameSyntax is null
            && serviceType is INamedTypeSymbol { IsUnboundGenericType: false }
            && namedImplementationType is { IsUnboundGenericType: false }
        )
        {
            return new RegistrationSemanticOutcome(null, null);
        }

        var activation = ImplementationActivationAnalyzer.Analyze(namedImplementationType);
        if (!activation.IsValid)
        {
            if (IsPureSelfRegistration(invocation))
                return new RegistrationSemanticOutcome(null, null);

            return new RegistrationSemanticOutcome(
                Registration: null,
                Diagnostic: activation.CreateDiagnostic(GetImplementationLocation(invocation))
            );
        }

        return new RegistrationSemanticOutcome(
            Registration: new ServiceRegistration(
                serviceType.Name,
                serviceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                implementationType.Name,
                implementationType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                lifetime,
                hasFactory,
                activation.ConstructorParameterTypes
            ),
            Diagnostic: null
        );
    }

    private static string? GetMethodName(ExpressionSyntax expression)
    {
        return expression switch
        {
            MemberAccessExpressionSyntax memberAccess
                => RegistrationSyntaxPipeline.GetMethodNameFromMemberAccess(memberAccess),
            GenericNameSyntax genericName => genericName.Identifier.Text,
            IdentifierNameSyntax identifierName => identifierName.Identifier.Text,
            _ => null
        };
    }

    private static bool IsFactoryRegistration(
        InvocationExpressionSyntax invocation,
        IMethodSymbol methodSymbol,
        SemanticModel semanticModel
    )
    {
        if (
            invocation
                .ArgumentList
                .Arguments
                .Any(
                    arg =>
                        arg.Expression
                            is LambdaExpressionSyntax
                                or AnonymousMethodExpressionSyntax
                                or AnonymousFunctionExpressionSyntax
                )
        )
        {
            return true;
        }

        if (
            methodSymbol.Parameters.Any(p => p.Type is INamedTypeSymbol { Name: PicoDiNames.Func })
            && invocation.ArgumentList.Arguments.Count > 0
        )
        {
            return true;
        }

        return invocation
            .ArgumentList
            .Arguments
            .Any(
                arg =>
                    semanticModel.GetTypeInfo(arg.Expression).ConvertedType
                        is INamedTypeSymbol { Name: PicoDiNames.Func }
            );
    }

    private static bool IsPureSelfRegistration(InvocationExpressionSyntax invocation)
    {
        var genericNameSyntax = invocation.Expression switch
        {
            MemberAccessExpressionSyntax { Name: GenericNameSyntax gn } => gn,
            GenericNameSyntax gn => gn,
            _ => null
        };

        return genericNameSyntax?.TypeArgumentList.Arguments.Count == 1
            && !invocation
                .ArgumentList
                .Arguments
                .Any(arg => arg.Expression is TypeOfExpressionSyntax);
    }

    private static Location? GetImplementationLocation(InvocationExpressionSyntax invocation)
    {
        var genericNameSyntax = invocation.Expression switch
        {
            MemberAccessExpressionSyntax { Name: GenericNameSyntax gn } => gn,
            GenericNameSyntax gn => gn,
            _ => null
        };

        if (genericNameSyntax?.TypeArgumentList.Arguments.Count > 1)
            return genericNameSyntax.TypeArgumentList.Arguments[1].GetLocation();

        var typeOfArguments = invocation
            .ArgumentList
            .Arguments
            .Select(a => a.Expression)
            .OfType<TypeOfExpressionSyntax>()
            .ToList();

        return typeOfArguments.Count > 1
            ? typeOfArguments[1].Type.GetLocation()
            : typeOfArguments.Count == 1
                ? typeOfArguments[0].Type.GetLocation()
                : genericNameSyntax?.TypeArgumentList.Arguments.FirstOrDefault()?.GetLocation();
    }

    private sealed class DiagnosticIdentityComparer : IEqualityComparer<Diagnostic>
    {
        public static readonly DiagnosticIdentityComparer Instance = new();

        public bool Equals(Diagnostic? x, Diagnostic? y)
        {
            if (ReferenceEquals(x, y))
                return true;

            if (x is null || y is null)
                return false;

            return x.Id == y.Id
                && x.Location.SourceSpan.Equals(y.Location.SourceSpan)
                && string.Equals(x.GetMessage(), y.GetMessage(), StringComparison.Ordinal);
        }

        public int GetHashCode(Diagnostic obj)
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 23) + obj.Id.GetHashCode();
                hash = (hash * 23) + obj.Location.SourceSpan.GetHashCode();
                hash = (hash * 23) + obj.GetMessage().GetHashCode();
                return hash;
            }
        }
    }
}
