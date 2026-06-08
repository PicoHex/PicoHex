namespace PicoAot.Gen.Emission;

internal static class InvocationEmitter
{
    public static string EmitInvocationStruct(
        string safeSvcName, string methodName,
        IMethodSymbol method, ITypeSymbol interceptorType)
    {
        var sb = new StringBuilder();
        var svcFullName = method.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var retType = method.ReturnType;
        var hasReturn = retType.SpecialType != SpecialType.System_Void;
        var resultTypeName = hasReturn
            ? retType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            : "object";

        var structName = BuildStructName(safeSvcName, method);
        var interfaceName = hasReturn ? $"IInvocation<{resultTypeName}>" : "IInvocation";

        sb.AppendLine($"struct {structName} : {interfaceName}");
        sb.AppendLine("{");
        sb.AppendLine($"    internal readonly {svcFullName} _target;");
        sb.AppendLine($"    internal readonly {interceptorType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} _i0;");
        foreach (var p in method.Parameters)
            sb.AppendLine($"    internal readonly {p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} _{p.Name};");

        sb.AppendLine();
        sb.AppendLine($"    public string MethodName => \"{method.Name}\";");
        sb.AppendLine($"    public Type ServiceType => typeof({svcFullName});");
        if (hasReturn)
            sb.AppendLine($"    public {resultTypeName} Result {{ get; set; }}");

        sb.AppendLine();
        var paramArgs = string.Join(", ", method.Parameters.Select(p => $"_{p.Name}"));
        if (hasReturn)
            sb.AppendLine($"    internal {resultTypeName} InvokeTarget() => _target.{method.Name}({paramArgs});");
        else
            sb.AppendLine($"    internal void InvokeTarget() => _target.{method.Name}({paramArgs});");

        sb.AppendLine("}");
        return sb.ToString();
    }

    public static string BuildStructName(string safeSvcName, IMethodSymbol method)
    {
        var name = $"Invocation_{safeSvcName}_{method.Name}";
        foreach (var p in method.Parameters)
            name += $"_{Sanitize(p.Type.Name)}";
        return name;
    }

    public static string Sanitize(string name) =>
        name.Replace('.', '_').Replace('<', '_').Replace('>', '_')
            .Replace(',', '_').Replace(' ', '_').Replace('+', '_');

    public static string BuildSafeServiceName(ITypeSymbol serviceType) =>
        Sanitize(serviceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
}
