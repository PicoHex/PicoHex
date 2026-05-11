namespace PicoDI.Gen;

internal static class RegistrationSyntaxPipeline
{
    /// <summary>
    /// Check if this is a closed generic type used in a declaration (variable, field, property, parameter).
    /// This helps detect entity-associated generics like IRepository&lt;User&gt;.
    /// Also detects closed generic types in constructor parameters (both regular and primary constructors).
    /// </summary>
    public static bool IsClosedGenericTypeDeclaration(SyntaxNode node)
    {
        if (node is not GenericNameSyntax genericName)
            return false;

        if (genericName.TypeArgumentList.Arguments.Count is 0)
            return false;

        var parent = genericName.Parent;
        while (parent is QualifiedNameSyntax or AliasQualifiedNameSyntax or NullableTypeSyntax)
            parent = parent.Parent;

        if (
            parent
            is VariableDeclarationSyntax
                or PropertyDeclarationSyntax
                or FieldDeclarationSyntax
                or ParameterSyntax
                or TypeArgumentListSyntax
                or BaseTypeSyntax
                or ObjectCreationExpressionSyntax
        )
            return true;

        if (parent is ParameterSyntax paramSyntax)
        {
            var paramListParent = paramSyntax.Parent?.Parent;
            return paramListParent
                is ConstructorDeclarationSyntax
                    or ClassDeclarationSyntax
                    or RecordDeclarationSyntax
                    or StructDeclarationSyntax;
        }

        return false;
    }

    /// <summary>
    /// Check if this is a constructor declaration (including primary constructors).
    /// Used to detect closed generic types in constructor parameters.
    /// </summary>
    public static bool IsConstructorWithGenericParameter(SyntaxNode node)
    {
        return node switch
        {
            ConstructorDeclarationSyntax ctorDecl
                => ctorDecl
                    .ParameterList
                    .Parameters
                    .Any(
                        p =>
                            p.Type
                                is GenericNameSyntax
                                    or QualifiedNameSyntax { Right: GenericNameSyntax }
                                    or NullableTypeSyntax { ElementType: GenericNameSyntax }
                    ),
            TypeDeclarationSyntax { ParameterList: not null } typeDecl
                => typeDecl
                    .ParameterList
                    .Parameters
                    .Any(
                        p =>
                            p.Type
                                is GenericNameSyntax
                                    or QualifiedNameSyntax { Right: GenericNameSyntax }
                                    or NullableTypeSyntax { ElementType: GenericNameSyntax }
                    ),
            _ => false
        };
    }

    /// <summary>
    /// Check if this is a GetService&lt;T&gt; invocation.
    /// </summary>
    public static bool IsGetServiceInvocation(SyntaxNode node)
    {
        if (node is not InvocationExpressionSyntax invocation)
            return false;

        var methodName = invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess
                => GetMethodNameFromMemberAccess(memberAccess),
            _ => null
        };

        return methodName is PicoDiNames.GetService or PicoDiNames.GetServices;
    }

    /// <summary>
    /// Check if this is an open generic registration invocation.
    /// Detects Register* methods with typeof(T&lt;&gt;) arguments.
    /// </summary>
    public static bool IsOpenGenericRegisterInvocation(SyntaxNode node)
    {
        if (node is not InvocationExpressionSyntax invocation)
            return false;

        var methodName = invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess
                => GetMethodNameFromMemberAccess(memberAccess),
            _ => null
        };

        if (methodName is null || !PicoDiNames.RegisterMethodNames.Any(methodName.StartsWith))
            return false;

        foreach (var arg in invocation.ArgumentList.Arguments)
        {
            if (arg.Expression is not TypeOfExpressionSyntax typeOfExpr)
                continue;

            var typeText = typeOfExpr.Type.ToString();
            if (typeText.Contains("<>") || typeText.Contains("<,"))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Fast syntax-only check to filter potential Register* method calls.
    /// Excludes open generic registrations which are handled separately.
    /// </summary>
    public static bool IsRegisterMethodInvocation(SyntaxNode node)
    {
        if (node is not InvocationExpressionSyntax invocation)
            return false;

        var methodName = invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess
                => GetMethodNameFromMemberAccess(memberAccess),
            _ => null
        };

        if (methodName is null || !PicoDiNames.RegisterMethodNames.Any(methodName.StartsWith))
            return false;

        // Fast syntax filter: only match instance method calls (e.g., "container.Register*()").
        // Static calls like "SvcContainerLifetimeExtensions.RegisterTransient(container, ...)"
        // are excluded early, reducing semantic analysis workload.
        if (
            invocation.Expression is MemberAccessExpressionSyntax
            {
                Expression: not (IdentifierNameSyntax or ThisExpressionSyntax)
            }
        )
            return false;

        foreach (var arg in invocation.ArgumentList.Arguments)
        {
            if (arg.Expression is not TypeOfExpressionSyntax typeOfExpr)
                continue;

            var typeText = typeOfExpr.Type.ToString();
            if (typeText.Contains("<>") || typeText.Contains("<,"))
                return false;
        }

        return true;
    }

    internal static string? GetMethodNameFromMemberAccess(MemberAccessExpressionSyntax memberAccess)
    {
        return memberAccess.Name switch
        {
            GenericNameSyntax genericName => genericName.Identifier.Text,
            IdentifierNameSyntax identifierName => identifierName.Identifier.Text,
            _ => null
        };
    }
}
