namespace PicoAop.Gen.Emission;

internal static class ProxyEmitter
{
    public static string EmitInterceptedClass(
        string safeSvcName,
        INamedTypeSymbol serviceType,
        ITypeSymbol interceptorType)
    {
        var sb = new StringBuilder();
        var svcFullName = serviceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var intFullName = interceptorType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var intSuffix = InvocationEmitter.Sanitize(interceptorType.Name);
        var className = $"{PicoAopNames.InterceptedPrefix}{safeSvcName}_{intSuffix}";

        sb.AppendLine($"sealed class {className} : {svcFullName}");
        sb.AppendLine("{");
        sb.AppendLine($"    private readonly {svcFullName} _inner;");
        sb.AppendLine($"    private readonly {intFullName} _i0;");
        // Static delegate cache — one per method
        foreach (var method in serviceType.GetMembers().OfType<IMethodSymbol>()
            .Where(m => m.MethodKind == MethodKind.Ordinary
                && m.DeclaredAccessibility == Accessibility.Public
                && !m.IsStatic
                && m.Parameters.All(p => p.RefKind == RefKind.None)))
        {
            var delCache = EmitStaticDelegateCache(safeSvcName, method, interceptorType);
            foreach (var line in delCache.Split('\n'))
                sb.AppendLine(line);
        }
        sb.AppendLine();

        // Constructor
        sb.AppendLine($"    public {className}({svcFullName} inner, {intFullName} i0)");
        sb.AppendLine("    {");
        sb.AppendLine("        _inner = inner;");
        sb.AppendLine("        _i0 = i0;");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Methods
        var methods = serviceType.GetMembers().OfType<IMethodSymbol>()
            .Where(m => m.MethodKind == MethodKind.Ordinary
                && m.DeclaredAccessibility == Accessibility.Public
                && !m.IsStatic);

        foreach (var method in methods)
            EmitMethodOverride(sb, safeSvcName, method, svcFullName, intFullName, interceptorType);

        // Properties
        foreach (var prop in serviceType.GetMembers().OfType<IPropertySymbol>()
            .Where(p => !p.IsIndexer && !p.IsStatic && p.DeclaredAccessibility == Accessibility.Public))
            EmitProperty(sb, safeSvcName, prop, svcFullName, intFullName, interceptorType);

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static void EmitMethodOverride(
        StringBuilder sb, string safeSvcName,
        IMethodSymbol method, string svcFullName, string intFullName, ITypeSymbol? interceptorType)
    {
        var retType = method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var paramDecl = string.Join(", ", method.Parameters.Select(p =>
            $"{p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} {p.Name}"));
        var paramArgs = string.Join(", ", method.Parameters.Select(p => p.Name));
        var structName = InvocationEmitter.BuildStructName(safeSvcName, method, interceptorType);
        var hasRefOut = method.Parameters.Any(p => p.RefKind != RefKind.None);

        sb.AppendLine($"    public {retType} {method.Name}({paramDecl})");
        sb.AppendLine("    {");

        if (hasRefOut)
        {
            sb.AppendLine($"        return _inner.{method.Name}({paramArgs});");
        }
        else
        {
            var retNamed = method.ReturnType as INamedTypeSymbol;
            var metaName = retNamed?.MetadataName;
            var isTaskOf = metaName is "Task`1";
            var isValueTaskOf = metaName is "ValueTask`1";
            var isTask = metaName is "Task";
            var isValueTask = metaName is "ValueTask";
            var isAsync = isTaskOf || isValueTaskOf || isTask || isValueTask;

            if (isAsync)
            {
                sb.AppendLine($"        var inv = new {structName}(_inner, _i0{(method.Parameters.Length > 0 ? ", " + paramArgs : "")});");

                if (isTaskOf || isTask)
                {
                    // Task<T> or Task: convert ValueTask → Task via .AsTask()
                    var invokeMethod = isTaskOf ? "InvokeAsync" : "InvokeAsyncVoid";
                    sb.AppendLine($"        return _i0.{invokeMethod}(inv, static i => (({structName})i).InvokeTargetAsync()).AsTask();");
                }
                else
                {
                    // ValueTask<T> or ValueTask: return directly
                    var invokeMethod = isValueTaskOf ? "InvokeAsync" : "InvokeAsyncVoid";
                    sb.AppendLine($"        return _i0.{invokeMethod}(inv, static i => (({structName})i).InvokeTargetAsync());");
                }
            }
            else if (method.ReturnType.SpecialType == SpecialType.System_Void)
            {
                sb.AppendLine($"        var inv = new {structName}(_inner, _i0{(method.Parameters.Length > 0 ? ", " + paramArgs : "")});");
                sb.AppendLine($"        _i0.InvokeVoid(inv, s_{method.Name}Next);");
            }
            else
            {
                sb.AppendLine($"        var inv = new {structName}(_inner, _i0{(method.Parameters.Length > 0 ? ", " + paramArgs : "")});");
                sb.AppendLine($"        return _i0.Invoke(inv, s_{method.Name}Next);");
            }
        }

        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static void EmitProperty(
        StringBuilder sb, string safeSvcName,
        IPropertySymbol prop, string svcFullName, string intFullName,
        ITypeSymbol? interceptorType)
    {
        var propType = prop.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var intSuffix = interceptorType != null ? $"_{InvocationEmitter.Sanitize(interceptorType.Name)}" : "";
        sb.AppendLine($"    public {propType} {prop.Name}");
        sb.AppendLine("    {");

        if (prop.GetMethod?.DeclaredAccessibility == Accessibility.Public)
        {
            sb.AppendLine("        get");
            sb.AppendLine("        {");
            sb.AppendLine($"            var inv = new Invocation_{safeSvcName}_{prop.Name}_Getter{intSuffix}(_inner, _i0);");
            sb.AppendLine($"            return _i0.Invoke(inv, s_get_{prop.Name}Next);");
            sb.AppendLine("        }");
        }

        if (prop.SetMethod?.DeclaredAccessibility == Accessibility.Public && !prop.SetMethod.IsInitOnly)
        {
            sb.AppendLine("        set");
            sb.AppendLine("        {");
            sb.AppendLine($"            var inv = new Invocation_{safeSvcName}_{prop.Name}_Setter{intSuffix}(_inner, _i0, value);");
            sb.AppendLine($"            _i0.InvokeVoid(inv, s_set_{prop.Name}Next);");
            sb.AppendLine("        }");
        }

        sb.AppendLine("    }");
        sb.AppendLine();
    }

    public static string EmitStaticDelegateCache(string safeSvcName, IMethodSymbol method, ITypeSymbol? interceptorType = null)
    {
        var structName = InvocationEmitter.BuildStructName(safeSvcName, method, interceptorType);
        var isAsync = method.ReturnType is INamedTypeSymbol { MetadataName: "Task`1" or "ValueTask`1" or "Task" or "ValueTask" };

        if (isAsync)
        {
            var namedRet = method.ReturnType as INamedTypeSymbol;
            var isOrigValueTask = namedRet?.MetadataName is "ValueTask" or "ValueTask`1";
            if (isOrigValueTask)
            {
                // ValueTask<T>: InvokeTargetAsync() returns ValueTask<T> directly
                var retType = namedRet!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                return $"    private static readonly Func<{structName}, {retType}> s_{method.Name}Next = static inv => inv.InvokeTargetAsync();";
            }
            else
            {
                // Task<T>: InvokeTargetAsync() returns ValueTask<T> (wrapped)
                var unwrapped = namedRet?.TypeArguments.Length > 0
                    ? $"<{namedRet!.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}>"
                    : "";
                var retType = $"ValueTask{unwrapped}";
                return $"    private static readonly Func<{structName}, {retType}> s_{method.Name}Next = static inv => inv.InvokeTargetAsync();";
            }
        }

        if (method.ReturnType.SpecialType == SpecialType.System_Void)
            return $"    private static readonly Func<{structName}, object?> s_{method.Name}Next = static inv => {{ inv.InvokeTarget(); return null; }};";

        var syncRetType = method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return $"    private static readonly Func<{structName}, {syncRetType}> s_{method.Name}Next = static inv => inv.InvokeTarget();";
    }

    public static string EmitWrappersClass(
        string safeSvcName,
        INamedTypeSymbol serviceType,
        ITypeSymbol interceptorType)
    {
        var svcFullName = serviceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var intFullName = interceptorType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var intSuffix = InvocationEmitter.Sanitize(interceptorType.Name);
        var className = $"{PicoAopNames.InterceptedPrefix}{safeSvcName}_{intSuffix}";

        return
            $"public static partial class {PicoAopNames.WrappersClass}\n" +
            "{\n" +
            $"    public static {svcFullName} Wrap_{safeSvcName}_{intSuffix}({svcFullName} inner, {intFullName} i0) =>\n" +
            $"        new {className}(inner, i0);\n" +
            "}\n";
    }
}
