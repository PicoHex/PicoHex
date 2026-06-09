namespace PicoAop.Gen.Emission;

internal static class InvocationEmitter
{
    // Metadata names for async types
    private const string TaskOf = "Task`1";
    private const string ValueTaskOf = "ValueTask`1";
    private const string PlainTask = "Task";
    private const string PlainValueTask = "ValueTask";

    public static string EmitInvocationStruct(
        string safeSvcName,
        string methodName,
        IMethodSymbol method,
        ITypeSymbol interceptorType
    )
    {
        var sb = new StringBuilder();
        var svcFullName = method.ContainingType.ToDisplayString(
            SymbolDisplayFormat.FullyQualifiedFormat
        );
        var retType = method.ReturnType;
        var retNamed = retType as INamedTypeSymbol;
        var metaName = retNamed?.MetadataName;

        var isAsync = metaName is TaskOf or ValueTaskOf or PlainTask or PlainValueTask;
        var hasReturn = !isAsync
            ? retType.SpecialType != SpecialType.System_Void
            : metaName is TaskOf or ValueTaskOf;

        var resultTypeName = hasReturn
            ? (
                retNamed?.TypeArguments.Length > 0
                    ? retNamed
                        .TypeArguments[0]
                        .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                    : retType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            )
            : "object";

        var structName = BuildStructName(safeSvcName, method, interceptorType);
        var interfaceName = hasReturn ? $"IInvocation<{resultTypeName}>" : "IInvocation";
        var intFullName = interceptorType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        sb.AppendLine($"struct {structName} : {interfaceName}");
        sb.AppendLine("{");
        sb.AppendLine($"    internal readonly {svcFullName} _target;");
        sb.AppendLine($"    internal readonly {intFullName} _i0;");
        foreach (var p in method.Parameters)
            sb.AppendLine(
                $"    internal readonly {p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} _{p.Name};"
            );
        sb.AppendLine();

        // Constructor
        var ctorParams = $"({svcFullName} target, {intFullName} i0";
        if (method.Parameters.Length > 0)
            ctorParams +=
                ", "
                + string.Join(
                    ", ",
                    method.Parameters.Select(p =>
                        $"{p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} {p.Name}"
                    )
                );
        ctorParams += ")";

        sb.AppendLine($"    internal {structName}{ctorParams}");
        sb.AppendLine("    {");
        sb.AppendLine("        _target = target;");
        sb.AppendLine("        _i0 = i0;");
        foreach (var p in method.Parameters)
            sb.AppendLine($"        _{p.Name} = {p.Name};");
        if (hasReturn)
            sb.AppendLine("        Result = default!;");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Metadata properties
        sb.AppendLine($"    public string MethodName => \"{method.Name}\";");
        sb.AppendLine($"    public Type ServiceType => typeof({svcFullName});");
        if (hasReturn)
            sb.AppendLine($"    public {resultTypeName} Result {{ get; set; }}");
        sb.AppendLine();

        // InvokeTarget / InvokeTargetAsync
        var paramArgs = string.Join(", ", method.Parameters.Select(p => $"_{p.Name}"));
        if (isAsync)
        {
            var asyncRet = hasReturn ? $"ValueTask<{resultTypeName}>" : "ValueTask";
            var isOrigValueTask = metaName is PlainValueTask or ValueTaskOf;

            if (hasReturn && !isOrigValueTask)
                sb.AppendLine(
                    $"    internal {asyncRet} InvokeTargetAsync() => new(_target.{method.Name}({paramArgs}));"
                );
            else if (hasReturn)
                sb.AppendLine(
                    $"    internal {asyncRet} InvokeTargetAsync() => _target.{method.Name}({paramArgs});"
                );
            else if (isOrigValueTask)
                sb.AppendLine(
                    $"    internal {asyncRet} InvokeTargetAsync() => _target.{method.Name}({paramArgs});"
                );
            else
                sb.AppendLine(
                    $"    internal async {asyncRet} InvokeTargetAsync() => await _target.{method.Name}({paramArgs});"
                );
        }
        else
        {
            if (hasReturn)
                sb.AppendLine(
                    $"    internal {resultTypeName} InvokeTarget() => _target.{method.Name}({paramArgs});"
                );
            else
                sb.AppendLine(
                    $"    internal void InvokeTarget() => _target.{method.Name}({paramArgs});"
                );
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    public static string BuildStructName(
        string safeSvcName,
        IMethodSymbol method,
        ITypeSymbol? interceptorType = null
    )
    {
        var name = $"Invocation_{safeSvcName}_{method.Name}";
        foreach (var p in method.Parameters)
            name += $"_{Sanitize(p.Type.Name)}";
        if (interceptorType != null)
            name += $"_{Sanitize(interceptorType.Name)}";
        return name;
    }

    public static string Sanitize(string name) =>
        name.Replace("global::", "")
            .Replace("::", "_")
            .Replace('.', '_')
            .Replace('<', '_')
            .Replace('>', '_')
            .Replace(',', '_')
            .Replace(' ', '_')
            .Replace('+', '_');

    public static string BuildSafeServiceName(ITypeSymbol serviceType) =>
        Sanitize(serviceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
}
