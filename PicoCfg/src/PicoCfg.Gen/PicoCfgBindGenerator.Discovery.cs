namespace PicoCfg.Gen;

// Discovers supported entry points and maps invocations to bind operations.
public sealed partial class PicoCfgBindGenerator
{
    private static bool IsCandidateInvocation(SyntaxNode node)
    {
        if (node is not InvocationExpressionSyntax invocation)
            return false;

        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax { Name: { } simpleName }
                => IsTargetMethodName(simpleName.Identifier.ValueText),
            SimpleNameSyntax simpleName => IsTargetMethodName(simpleName.Identifier.ValueText),
            _ => false,
        };
    }

    private static BindCall? TransformInvocation(GeneratorSyntaxContext context)
    {
        if (context.Node is not InvocationExpressionSyntax invocation)
            return null;

        if (context.SemanticModel.Compilation.AssemblyName == "PicoCfg.Gen")
            return null;

        if (context.SemanticModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol method
            && TryGetOperation(method, out var operation)
            && TryGetTargetType(context.SemanticModel, invocation, method, out var targetType))
        {
            return new BindCall(targetType, operation, invocation.GetLocation());
        }

        if (TryGetDiOperationFromSyntax(context.SemanticModel, invocation, out var syntaxOperation, out var syntaxTargetType))
            return new BindCall(syntaxTargetType, syntaxOperation, invocation.GetLocation());

        return null;
    }

    private static bool TryGetDiOperationFromSyntax(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        out BindOperation operation,
        out ITypeSymbol targetType
    )
    {
        operation = default;
        targetType = null!;

        if (invocation.Expression is not MemberAccessExpressionSyntax { Expression: var receiver, Name: GenericNameSyntax genericName })
            return false;

        if (!IsDiRegistrationMethodName(genericName.Identifier.ValueText))
            return false;

        var receiverType = semanticModel.GetTypeInfo(receiver).Type ?? semanticModel.GetTypeInfo(receiver).ConvertedType;
        if (!IsSupportedDiReceiverType(receiverType))
            return false;

        if (!TryGetTypeSymbol(semanticModel, genericName.TypeArgumentList.Arguments[0], out targetType))
            return false;

        operation = BindOperation.Bind;
        return true;
    }

    private static bool TryGetTargetType(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        out ITypeSymbol targetType
    )
    {
        targetType = null!;

        if (method.TypeArguments.Length == 1)
        {
            targetType = method.TypeArguments[0];
            return true;
        }

        if (invocation.Expression is MemberAccessExpressionSyntax { Name: GenericNameSyntax genericName })
            return TryGetTypeSymbol(semanticModel, genericName.TypeArgumentList.Arguments[0], out targetType);

        if (invocation.Expression is GenericNameSyntax directGenericName)
            return TryGetTypeSymbol(semanticModel, directGenericName.TypeArgumentList.Arguments[0], out targetType);

        return false;
    }

    private static bool TryGetTypeSymbol(SemanticModel semanticModel, TypeSyntax syntax, out ITypeSymbol typeSymbol)
    {
        typeSymbol = semanticModel.GetTypeInfo(syntax).Type ?? semanticModel.GetTypeInfo(syntax).ConvertedType!;
        return typeSymbol is not null;
    }

    private static bool IsSupportedDiReceiverType(ITypeSymbol? receiverType)
    {
        if (receiverType is null)
            return false;

        var receiverNamespace = receiverType.ContainingNamespace.ToDisplayString();
        return receiverType.Name is "ISvcContainer" or "SvcContainer"
            && receiverNamespace is "PicoDI.Abs" or "PicoDI";
    }

    private static bool TryGetOperation(IMethodSymbol method, out BindOperation operation)
    {
        operation = default;
        if (!method.IsGenericMethod || method.TypeArguments.Length != 1)
            return false;

        return TryGetOperationCore(method, out operation)
            || (method.ReducedFrom is { } reducedMethod
                && TryGetOperationCore(reducedMethod, out operation));
    }

    private static bool TryGetOperationCore(IMethodSymbol operationMethod, out BindOperation operation)
    {
        operation = default;

        if (
            operationMethod.ContainingType.Name == "CfgBind"
            && operationMethod.ContainingType.ContainingNamespace.ToDisplayString() == "PicoCfg"
        )
        {
            switch (operationMethod.Name)
            {
                case "Bind":
                    operation = BindOperation.Bind;
                    return true;
                case "TryBind":
                    operation = BindOperation.TryBind;
                    return true;
                case "BindInto":
                    operation = BindOperation.BindInto;
                    return true;
                default:
                    return false;
            }
        }

        var isDiRegistrationName = operationMethod.Name
            is "RegisterCfgTransient"
                or "RegisterCfgScoped"
                or "RegisterCfgSingleton";
        if (!isDiRegistrationName)
            return false;

        var containingNamespace = operationMethod.ContainingType.ContainingNamespace.ToDisplayString();
        var isSupportedDiContainerSurface =
            (operationMethod.ContainingType.Name == "SvcContainerExtensions"
                && containingNamespace == "PicoCfg.DI")
            || (operationMethod.ContainingType.Name is "ISvcContainer" or "SvcContainer"
                && containingNamespace is "PicoDI.Abs" or "PicoDI");
        if (!isSupportedDiContainerSurface)
            return false;

        switch (operationMethod.Name)
        {
            case "RegisterCfgTransient":
            case "RegisterCfgScoped":
            case "RegisterCfgSingleton":
                operation = BindOperation.Bind;
                return true;
        }

        return false;
    }

    private static bool IsTargetMethodName(string methodName) =>
        methodName
            is "Bind"
                or "TryBind"
                or "BindInto"
                or "RegisterCfgTransient"
                or "RegisterCfgScoped"
                or "RegisterCfgSingleton";

    private static bool IsDiRegistrationMethodName(string methodName) =>
        methodName
            is "RegisterCfgTransient"
                or "RegisterCfgScoped"
                or "RegisterCfgSingleton";
}
