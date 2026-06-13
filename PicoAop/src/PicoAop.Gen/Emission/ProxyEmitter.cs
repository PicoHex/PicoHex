namespace PicoAop.Gen.Emission;

internal static class ProxyEmitter
{
    public static string EmitInterceptedClassMulti(
        string safeSvcName,
        INamedTypeSymbol serviceType,
        List<ITypeSymbol> interceptorTypes
    )
    {
        if (interceptorTypes.Count == 0)
            return string.Empty;
        if (interceptorTypes.Count == 1)
            return EmitInterceptedClass(safeSvcName, serviceType, interceptorTypes[0]);

        var sb = new StringBuilder();
        var svcFullName = serviceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var intSuffix = string.Join(
            "_",
            interceptorTypes.Select(t =>
                InvocationEmitter.Sanitize(
                    t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                )
            )
        );
        var className = $"{PicoAopNames.InterceptedPrefix}{safeSvcName}_{intSuffix}";

        sb.AppendLine($"sealed class {className} : {svcFullName}");
        sb.AppendLine("{");
        sb.AppendLine($"    private readonly {svcFullName} _inner;");
        for (int i = 0; i < interceptorTypes.Count; i++)
            sb.AppendLine(
                $"    private readonly {interceptorTypes[i].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} _i{i};"
            );
        sb.AppendLine();

        // Constructor
        var ctorParams = $"({svcFullName} inner";
        for (int i = 0; i < interceptorTypes.Count; i++)
            ctorParams +=
                $", {interceptorTypes[i].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} i{i}";
        ctorParams += ")";
        sb.AppendLine($"    public {className}{ctorParams}");
        sb.AppendLine("    {");
        sb.AppendLine("        _inner = inner;");
        for (int i = 0; i < interceptorTypes.Count; i++)
            sb.AppendLine($"        _i{i} = i{i};");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Static delegate chain: from outermost to innermost
        var methods = serviceType
            .GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m =>
                m.MethodKind == MethodKind.Ordinary
                && m.DeclaredAccessibility == Accessibility.Public
                && !m.IsStatic
            );

        foreach (var method in methods.Where(m => m.Parameters.All(p => p.RefKind == RefKind.None)))
        {
            var structName = InvocationEmitter.BuildStructName(safeSvcName, method);
            var multiStructName = $"{structName}_{intSuffix}";
            var hasReturn = method.ReturnType.SpecialType != SpecialType.System_Void;
            var isAsync =
                method.ReturnType is INamedTypeSymbol
                {
                    MetadataName: "Task`1" or "ValueTask`1" or "Task" or "ValueTask"
                };

            // Step0: calls InvokeTarget (the actual target method)
            if (isAsync)
            {
                var namedRet = method.ReturnType as INamedTypeSymbol;
                var isOrigValueTask = namedRet?.MetadataName is "ValueTask" or "ValueTask`1";
                var retType = isOrigValueTask
                    ? namedRet!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                    : (
                        namedRet?.TypeArguments.Length > 0
                            ? $"ValueTask<{namedRet.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}>"
                            : "ValueTask"
                    );
                sb.AppendLine(
                    $"    private static readonly Func<{multiStructName}, {retType}> s_{method.Name}_Step0 = static inv => inv.InvokeTargetAsync();"
                );
                for (int step = 1; step < interceptorTypes.Count; step++)
                {
                    int interceptorIdx = interceptorTypes.Count - step;
                    var invokeMethod =
                        (isOrigValueTask || retType == "ValueTask")
                            ? "InvokeAsyncVoid"
                            : "InvokeAsync";
                    sb.AppendLine(
                        $"    private static readonly Func<{multiStructName}, {retType}> s_{method.Name}_Step{step} = static inv => inv._i{interceptorIdx}.{invokeMethod}(inv, s_{method.Name}_Step{step - 1});"
                    );
                }
            }
            else if (hasReturn)
            {
                var retType = method.ReturnType.ToDisplayString(
                    SymbolDisplayFormat.FullyQualifiedFormat
                );
                sb.AppendLine(
                    $"    private static readonly Func<{multiStructName}, {retType}> s_{method.Name}_Step0 = static inv => inv.InvokeTarget();"
                );
                for (int step = 1; step < interceptorTypes.Count; step++)
                {
                    int interceptorIdx = interceptorTypes.Count - step;
                    sb.AppendLine(
                        $"    private static readonly Func<{multiStructName}, {retType}> s_{method.Name}_Step{step} = static inv => inv._i{interceptorIdx}.Invoke(inv, s_{method.Name}_Step{step - 1});"
                    );
                }
            }
            else
            {
                sb.AppendLine(
                    $"    private static readonly Func<{multiStructName}, object?> s_{method.Name}_Step0 = static inv => {{ inv.InvokeTarget(); return null; }};"
                );
                for (int step = 1; step < interceptorTypes.Count; step++)
                {
                    int interceptorIdx = interceptorTypes.Count - step;
                    sb.AppendLine(
                        $"    private static readonly Func<{multiStructName}, object?> s_{method.Name}_Step{step} = static inv => {{ inv._i{interceptorIdx}.InvokeVoid(inv, s_{method.Name}_Step{step - 1}); return null; }};"
                    );
                }
            }
        }
        sb.AppendLine();

        // Property delegate caches
        var svcFullName2 = serviceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        foreach (
            var prop in serviceType
                .GetMembers()
                .OfType<IPropertySymbol>()
                .Where(p =>
                    !p.IsIndexer && !p.IsStatic && p.DeclaredAccessibility == Accessibility.Public
                )
        )
        {
            var propType = prop.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var getterStructName = $"Invocation_{safeSvcName}_{prop.Name}_Getter_{intSuffix}";
            var setterStructName = $"Invocation_{safeSvcName}_{prop.Name}_Setter_{intSuffix}";

            if (prop.GetMethod?.DeclaredAccessibility == Accessibility.Public)
            {
                // Getter Step0
                sb.AppendLine(
                    $"    private static readonly Func<{getterStructName}, {propType}> s_get_{prop.Name}_Step0 = static inv => inv.InvokeTarget();"
                );
                // Getter Step1..StepN-1
                for (int step = 1; step < interceptorTypes.Count; step++)
                {
                    int interceptorIdx = interceptorTypes.Count - step;
                    sb.AppendLine(
                        $"    private static readonly Func<{getterStructName}, {propType}> s_get_{prop.Name}_Step{step} = static inv => inv._i{interceptorIdx}.Invoke(inv, s_get_{prop.Name}_Step{step - 1});"
                    );
                }
            }

            if (
                prop.SetMethod?.DeclaredAccessibility == Accessibility.Public
                && !prop.SetMethod.IsInitOnly
            )
            {
                // Setter Step0
                sb.AppendLine(
                    $"    private static readonly Func<{setterStructName}, object?> s_set_{prop.Name}_Step0 = static inv => {{ inv.InvokeTarget(); return null; }};"
                );
                // Setter Step1..StepN-1
                for (int step = 1; step < interceptorTypes.Count; step++)
                {
                    int interceptorIdx = interceptorTypes.Count - step;
                    sb.AppendLine(
                        $"    private static readonly Func<{setterStructName}, object?> s_set_{prop.Name}_Step{step} = static inv => {{ inv._i{interceptorIdx}.InvokeVoid(inv, s_set_{prop.Name}_Step{step - 1}); return null; }};"
                    );
                }
            }
        }
        sb.AppendLine();

        var isClassType = serviceType.TypeKind == TypeKind.Class;
        // Method overrides
        foreach (var method in methods)
            EmitMultiMethodOverride(
                sb,
                safeSvcName,
                method,
                svcFullName,
                interceptorTypes,
                intSuffix,
                isClassType
            );

        // Properties
        foreach (
            var prop in serviceType
                .GetMembers()
                .OfType<IPropertySymbol>()
                .Where(p =>
                    !p.IsIndexer && !p.IsStatic && p.DeclaredAccessibility == Accessibility.Public
                )
        )
            EmitMultiProperty(sb, safeSvcName, prop, svcFullName, interceptorTypes, intSuffix);

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static void EmitMultiMethodOverride(
        StringBuilder sb,
        string safeSvcName,
        IMethodSymbol method,
        string svcFullName,
        List<ITypeSymbol> interceptorTypes,
        string intSuffix,
        bool isClassType
    )
    {
        var retType = method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var paramDecl = string.Join(
            ", ",
            method.Parameters.Select(p =>
                $"{p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} {p.Name}"
            )
        );
        var paramArgs = string.Join(", ", method.Parameters.Select(p => p.Name));
        var structName = InvocationEmitter.BuildStructName(safeSvcName, method);
        var multiStructName = $"{structName}_{intSuffix}";
        var hasRefOut = method.Parameters.Any(p => p.RefKind != RefKind.None);

        // For class types with virtual/override/abstract methods, use 'public override'
        // so virtual dispatch routes through the proxy. Interface types don't need 'override'.
        var modifier =
            (isClassType && (method.IsVirtual || method.IsOverride || method.IsAbstract))
                ? "public override"
                : "public";
        sb.AppendLine($"    {modifier} {retType} {method.Name}({paramDecl})");
        sb.AppendLine("    {");

        if (hasRefOut)
        {
            sb.AppendLine($"        return _inner.{method.Name}({paramArgs});");
        }
        else
        {
            var ctorArgs = $"_inner{string.Concat(interceptorTypes.Select((_, i) => $", _i{i}"))}";
            if (method.Parameters.Length > 0)
                ctorArgs += ", " + paramArgs;

            var retNamed = method.ReturnType as INamedTypeSymbol;
            var metaName = retNamed?.MetadataName;
            var isTaskOf = metaName is "Task`1";
            var isValueTaskOf = metaName is "ValueTask`1";
            var isTask = metaName is "Task";
            var isValueTask = metaName is "ValueTask";

            sb.AppendLine($"        var inv = new {multiStructName}({ctorArgs});");

            var structType = multiStructName;
            var lastStep = interceptorTypes.Count - 1;
            var nextDelegate = $"s_{method.Name}_Step{lastStep}";

            if (isTaskOf || isValueTaskOf)
            {
                var unwrappedType = method.ReturnType
                    is INamedTypeSymbol { TypeArguments.Length: > 0 } namedRet
                    ? namedRet
                        .TypeArguments[0]
                        .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                    : "object";
                sb.AppendLine(
                    $"        return _i0.InvokeAsync<{structType}, {unwrappedType}>(inv, {nextDelegate}){(isTaskOf ? ".AsTask()" : "")};"
                );
            }
            else if (isTask)
            {
                sb.AppendLine(
                    $"        return _i0.InvokeAsyncVoid<{structType}>(inv, {nextDelegate}).AsTask();"
                );
            }
            else if (isValueTask)
            {
                sb.AppendLine(
                    $"        return _i0.InvokeAsyncVoid<{structType}>(inv, {nextDelegate});"
                );
            }
            else if (method.ReturnType.SpecialType == SpecialType.System_Void)
                sb.AppendLine($"        _i0.InvokeVoid(inv, {nextDelegate});");
            else
            {
                var retTypeName = method.ReturnType.ToDisplayString(
                    SymbolDisplayFormat.FullyQualifiedFormat
                );
                sb.AppendLine(
                    $"        return _i0.Invoke<{structType}, {retTypeName}>(inv, {nextDelegate});"
                );
            }
        }

        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static void EmitMultiProperty(
        StringBuilder sb,
        string safeSvcName,
        IPropertySymbol prop,
        string svcFullName,
        List<ITypeSymbol> interceptorTypes,
        string intSuffix
    )
    {
        var propType = prop.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        sb.AppendLine($"    public {propType} {prop.Name}");
        sb.AppendLine("    {");

        if (prop.GetMethod?.DeclaredAccessibility == Accessibility.Public)
        {
            sb.AppendLine("        get");
            sb.AppendLine("        {");
            sb.AppendLine(
                $"            var inv = new Invocation_{safeSvcName}_{prop.Name}_Getter_{intSuffix}(_inner, {string.Join(", ", interceptorTypes.Select((_, i) => $"_i{i}"))});"
            );
            var getterLastStep = interceptorTypes.Count - 1;
            sb.AppendLine(
                $"            return _i0.Invoke(inv, s_get_{prop.Name}_Step{getterLastStep});"
            );
            sb.AppendLine("        }");
        }

        if (
            prop.SetMethod?.DeclaredAccessibility == Accessibility.Public
            && !prop.SetMethod.IsInitOnly
        )
        {
            sb.AppendLine("        set");
            sb.AppendLine("        {");
            sb.AppendLine(
                $"            var inv = new Invocation_{safeSvcName}_{prop.Name}_Setter_{intSuffix}(_inner, {string.Join(", ", interceptorTypes.Select((_, i) => $"_i{i}"))}, value);"
            );
            var setterLastStep = interceptorTypes.Count - 1;
            sb.AppendLine(
                $"            _i0.InvokeVoid(inv, s_set_{prop.Name}_Step{setterLastStep});"
            );
            sb.AppendLine("        }");
        }

        sb.AppendLine("    }");
        sb.AppendLine();
    }

    public static string EmitInterceptedClass(
        string safeSvcName,
        INamedTypeSymbol serviceType,
        ITypeSymbol interceptorType
    )
    {
        var sb = new StringBuilder();
        var svcFullName = serviceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var intFullName = interceptorType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var intSuffix = InvocationEmitter.Sanitize(
            interceptorType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
        );
        var className = $"{PicoAopNames.InterceptedPrefix}{safeSvcName}_{intSuffix}";

        sb.AppendLine($"sealed class {className} : {svcFullName}");
        sb.AppendLine("{");
        sb.AppendLine($"    private readonly {svcFullName} _inner;");
        sb.AppendLine($"    private readonly {intFullName} _i0;");
        // Static delegate cache — one per method
        foreach (
            var method in serviceType
                .GetMembers()
                .OfType<IMethodSymbol>()
                .Where(m =>
                    m.MethodKind == MethodKind.Ordinary
                    && m.DeclaredAccessibility == Accessibility.Public
                    && !m.IsStatic
                    && m.Parameters.All(p => p.RefKind == RefKind.None)
                )
        )
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
        var isClassType = serviceType.TypeKind == TypeKind.Class;
        var methods = serviceType
            .GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m =>
                m.MethodKind == MethodKind.Ordinary
                && m.DeclaredAccessibility == Accessibility.Public
                && !m.IsStatic
            );

        foreach (var method in methods)
            EmitMethodOverride(
                sb,
                safeSvcName,
                method,
                svcFullName,
                intFullName,
                interceptorType,
                isClassType
            );

        // Properties
        foreach (
            var prop in serviceType
                .GetMembers()
                .OfType<IPropertySymbol>()
                .Where(p =>
                    !p.IsIndexer && !p.IsStatic && p.DeclaredAccessibility == Accessibility.Public
                )
        )
            EmitProperty(sb, safeSvcName, prop, svcFullName, intFullName, interceptorType);

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static void EmitMethodOverride(
        StringBuilder sb,
        string safeSvcName,
        IMethodSymbol method,
        string svcFullName,
        string intFullName,
        ITypeSymbol? interceptorType,
        bool isClassType
    )
    {
        var retType = method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var paramDecl = string.Join(
            ", ",
            method.Parameters.Select(p =>
                $"{p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} {p.Name}"
            )
        );
        var paramArgs = string.Join(", ", method.Parameters.Select(p => p.Name));
        var structName = InvocationEmitter.BuildStructName(safeSvcName, method, interceptorType);
        var hasRefOut = method.Parameters.Any(p => p.RefKind != RefKind.None);

        // For class types with virtual/override/abstract methods, use 'public override'
        var modifier =
            (isClassType && (method.IsVirtual || method.IsOverride || method.IsAbstract))
                ? "public override"
                : "public";
        sb.AppendLine($"    {modifier} {retType} {method.Name}({paramDecl})");
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
                sb.AppendLine(
                    $"        var inv = new {structName}(_inner, _i0{(method.Parameters.Length > 0 ? ", " + paramArgs : "")});"
                );

                if (isTaskOf || isTask)
                {
                    // Task<T> or Task: convert ValueTask → Task via .AsTask()
                    var invokeMethod = isTaskOf ? "InvokeAsync" : "InvokeAsyncVoid";
                    sb.AppendLine(
                        $"        return _i0.{invokeMethod}(inv, static i => (({structName})i).InvokeTargetAsync()).AsTask();"
                    );
                }
                else
                {
                    // ValueTask<T> or ValueTask: return directly
                    var invokeMethod = isValueTaskOf ? "InvokeAsync" : "InvokeAsyncVoid";
                    sb.AppendLine(
                        $"        return _i0.{invokeMethod}(inv, static i => (({structName})i).InvokeTargetAsync());"
                    );
                }
            }
            else if (method.ReturnType.SpecialType == SpecialType.System_Void)
            {
                sb.AppendLine(
                    $"        var inv = new {structName}(_inner, _i0{(method.Parameters.Length > 0 ? ", " + paramArgs : "")});"
                );
                sb.AppendLine($"        _i0.InvokeVoid(inv, s_{method.Name}Next);");
            }
            else
            {
                sb.AppendLine(
                    $"        var inv = new {structName}(_inner, _i0{(method.Parameters.Length > 0 ? ", " + paramArgs : "")});"
                );
                sb.AppendLine($"        return _i0.Invoke(inv, s_{method.Name}Next);");
            }
        }

        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static void EmitProperty(
        StringBuilder sb,
        string safeSvcName,
        IPropertySymbol prop,
        string svcFullName,
        string intFullName,
        ITypeSymbol? interceptorType
    )
    {
        var propType = prop.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var intSuffix =
            interceptorType != null
                ? $"_{InvocationEmitter.Sanitize(
                interceptorType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))}"
                : "";
        sb.AppendLine($"    public {propType} {prop.Name}");
        sb.AppendLine("    {");

        if (prop.GetMethod?.DeclaredAccessibility == Accessibility.Public)
        {
            sb.AppendLine("        get");
            sb.AppendLine("        {");
            sb.AppendLine(
                $"            var inv = new Invocation_{safeSvcName}_{prop.Name}_Getter{intSuffix}(_inner, _i0);"
            );
            sb.AppendLine($"            return _i0.Invoke(inv, s_get_{prop.Name}Next);");
            sb.AppendLine("        }");
        }

        if (
            prop.SetMethod?.DeclaredAccessibility == Accessibility.Public
            && !prop.SetMethod.IsInitOnly
        )
        {
            sb.AppendLine("        set");
            sb.AppendLine("        {");
            sb.AppendLine(
                $"            var inv = new Invocation_{safeSvcName}_{prop.Name}_Setter{intSuffix}(_inner, _i0, value);"
            );
            sb.AppendLine($"            _i0.InvokeVoid(inv, s_set_{prop.Name}Next);");
            sb.AppendLine("        }");
        }

        sb.AppendLine("    }");
        sb.AppendLine();
    }

    public static string EmitStaticDelegateCache(
        string safeSvcName,
        IMethodSymbol method,
        ITypeSymbol? interceptorType = null
    )
    {
        var structName = InvocationEmitter.BuildStructName(safeSvcName, method, interceptorType);
        var isAsync =
            method.ReturnType is INamedTypeSymbol
            {
                MetadataName: "Task`1" or "ValueTask`1" or "Task" or "ValueTask"
            };

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
                var unwrapped =
                    namedRet?.TypeArguments.Length > 0
                        ? $"<{namedRet!.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}>"
                        : "";
                var retType = $"ValueTask{unwrapped}";
                return $"    private static readonly Func<{structName}, {retType}> s_{method.Name}Next = static inv => inv.InvokeTargetAsync();";
            }
        }

        if (method.ReturnType.SpecialType == SpecialType.System_Void)
            return $"    private static readonly Func<{structName}, object?> s_{method.Name}Next = static inv => {{ inv.InvokeTarget(); return null; }};";

        var syncRetType = method.ReturnType.ToDisplayString(
            SymbolDisplayFormat.FullyQualifiedFormat
        );
        return $"    private static readonly Func<{structName}, {syncRetType}> s_{method.Name}Next = static inv => inv.InvokeTarget();";
    }

    public static string EmitWrappersClass(
        string safeSvcName,
        INamedTypeSymbol serviceType,
        ITypeSymbol interceptorType
    )
    {
        var svcFullName = serviceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var intFullName = interceptorType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var intSuffix = InvocationEmitter.Sanitize(
            interceptorType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
        );
        var className = $"{PicoAopNames.InterceptedPrefix}{safeSvcName}_{intSuffix}";

        return $"public static partial class {PicoAopNames.WrappersClass}\n"
            + "{\n"
            + $"    public static {svcFullName} Wrap_{safeSvcName}_{intSuffix}({svcFullName} inner, {intFullName} i0) =>\n"
            + $"        new {className}(inner, i0);\n"
            + "}\n";
    }
}
