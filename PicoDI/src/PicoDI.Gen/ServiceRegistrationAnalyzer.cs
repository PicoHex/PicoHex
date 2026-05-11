namespace PicoDI.Gen;

/// <summary>
/// Diagnostic analyzer that detects potential issues with service registrations at compile time.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ServiceRegistrationAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>

        [
            DiagnosticDescriptors.CircularDependency,
            DiagnosticDescriptors.AbstractTypeRegistration,
            DiagnosticDescriptors.MissingPublicConstructor,
            DiagnosticDescriptors.MultipleMarkedConstructors,
            DiagnosticDescriptors.GenericRegistrationOverload
        ];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Get method name
        var methodName = invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
            _ => null
        };

        if (methodName is null || !IsRegisterMethod(methodName))
            return;

        // Get semantic info
        var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
        if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
            return;

        if (!PicoDiNames.IsPicoDiMethod(methodSymbol))
            return;

        // Check if it's a factory-based registration - multiple detection methods
        // 1. Check if any argument is a lambda expression or anonymous method
        var hasLambda = invocation
            .ArgumentList
            .Arguments
            .Any(
                arg =>
                    arg.Expression is LambdaExpressionSyntax
                    || arg.Expression is AnonymousMethodExpressionSyntax
                    || arg.Expression is AnonymousFunctionExpressionSyntax
            );

        if (hasLambda)
            return;

        // 2. Check if the method has a Func parameter (covers delegate and method group cases)
        var hasFactoryParameter = methodSymbol
            .Parameters
            .Any(p => p.Type is INamedTypeSymbol { Name: PicoDiNames.Func });

        if (hasFactoryParameter && invocation.ArgumentList.Arguments.Count > 0)
            return;

        // 3. Check if any argument's converted type is Func (covers method groups and delegate variables)
        if (
            invocation
                .ArgumentList
                .Arguments
                .Any(
                    arg =>
                        context.SemanticModel.GetTypeInfo(arg.Expression).ConvertedType
                            is INamedTypeSymbol { Name: PicoDiNames.Func }
                )
        )
        {
            return;
        }

        // Extract type arguments
        var genericNameSyntax = invocation.Expression switch
        {
            MemberAccessExpressionSyntax { Name: GenericNameSyntax gn } => gn,
            GenericNameSyntax gn => gn,
            _ => null
        };

        var typeOfExpressions = invocation
            .ArgumentList
            .Arguments
            .Select(arg => arg.Expression)
            .OfType<TypeOfExpressionSyntax>()
            .ToList();
        var explicitImplementationType = typeOfExpressions
            .Select(
                typeOfExpression => context.SemanticModel.GetTypeInfo(typeOfExpression.Type).Type
            )
            .OfType<INamedTypeSymbol>()
            .LastOrDefault();

        // 4. Detect Register(Type, Type, SvcLifetime) used with non-open-generic types.
        // The guarded Register(Type, Type, SvcLifetime) overload is only for open generics.
        if (
            methodName == "Register"
            && genericNameSyntax is null
            && typeOfExpressions.Count > 0
            && explicitImplementationType is { IsUnboundGenericType: false }
        )
        {
            var loc = typeOfExpressions.LastOrDefault()?.GetLocation();
            context.ReportDiagnostic(
                Diagnostic.Create(DiagnosticDescriptors.GenericRegistrationOverload, loc)
            );
            return;
        }

        if (genericNameSyntax?.TypeArgumentList.Arguments.Count is null or 0)
        {
            if (explicitImplementationType is { IsUnboundGenericType: true })
            {
                ReportActivationDiagnosticIfNeeded(
                    context,
                    explicitImplementationType,
                    typeOfExpressions.Last().Type.GetLocation()
                );
            }

            return;
        }
        var typeArgs = genericNameSyntax.TypeArgumentList.Arguments;

        // Skip placeholder methods (type-based registration scanned by Source Generator)
        // These are methods with only type arguments and possibly Type/SvcLifetime arguments
        // but no factory. They return container immediately and real registration is generated.
        // We only want to analyze Register<TService, TImplementation>() where both types are concrete.
        if (typeArgs.Count == 1)
        {
            if (explicitImplementationType is not null)
            {
                ReportActivationDiagnosticIfNeeded(
                    context,
                    explicitImplementationType,
                    typeOfExpressions.Last().Type.GetLocation()
                );
            }

            return;
        }

        // For Register<TService, TImplementation>() - check the implementation type (last type arg)
        var implementationTypeArg = typeArgs.Last();
        var implementationType = context.SemanticModel.GetTypeInfo(implementationTypeArg).Type;

        if (implementationType is not INamedTypeSymbol namedImplementationType)
            return;

        ReportActivationDiagnosticIfNeeded(
            context,
            namedImplementationType,
            implementationTypeArg.GetLocation()
        );
    }

    private static void ReportActivationDiagnosticIfNeeded(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol implementationType,
        Location location
    )
    {
        var activationType = implementationType.IsUnboundGenericType
            ? implementationType.OriginalDefinition
            : implementationType;
        var activation = ImplementationActivationAnalyzer.Analyze(activationType);
        var diagnostic = activation.CreateDiagnostic(location);
        if (diagnostic is null)
        {
            return;
        }

        context.ReportDiagnostic(diagnostic);
    }

    private static bool IsRegisterMethod(string methodName)
    {
        return methodName
            is PicoDiNames.Register
                or PicoDiNames.RegisterTransient
                or PicoDiNames.RegisterScoped
                or PicoDiNames.RegisterSingleton;
    }
}
