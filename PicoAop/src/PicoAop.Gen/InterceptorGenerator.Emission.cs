namespace PicoAop.Gen;

public sealed partial class InterceptorGenerator
{
    private static void EmitInvocationStruct(
        StringBuilder sb,
        INamedTypeSymbol serviceType,
        INamedTypeSymbol interceptorType
    )
    {
        var safeSvc = Sanitize(serviceType.ToDisplayString());
        var safeInt = Sanitize(interceptorType.Name);

        foreach (
            var method in GetAllMethods(serviceType)
                .Where(m => m.MethodKind == MethodKind.Ordinary && !HasRefLikeParameters(m))
        )
        {
            var retType = method.ReturnType;
            var resultName = retType
                is INamedTypeSymbol { MetadataName: "ValueTask`1" or "Task`1" } t
                ? t.TypeArguments[0].ToDisplayString()
                : retType.SpecialType == SpecialType.System_Void
                || retType is INamedTypeSymbol { MetadataName: "ValueTask" or "Task" }
                    ? PicoAopNames.VoidResultFull
                    : retType.ToDisplayString();

            var svcName = serviceType.ToDisplayString();
            var paramList = method.Parameters.ToList();
            var typeParams = method.TypeParameters;
            var isGeneric = typeParams.Length > 0;

            var structName = $"{safeSvc}_{safeInt}_{method.Name}";
            if (paramList.Count > 0)
            {
                foreach (var p in paramList)
                    structName += $"_{Sanitize(p.Type.Name)}";
            }
            structName += "_Invocation";

            var typeParamDecl = "";
            var typeParamConstraints = "";
            if (isGeneric)
            {
                typeParamDecl = "<" + string.Join(", ", typeParams.Select(tp => tp.Name)) + ">";
                typeParamConstraints = BuildGenericConstraints(typeParams);
            }

            sb.AppendLine(
                $"internal struct {structName}{typeParamDecl}{typeParamConstraints} : IInvocation<{resultName}>"
            );
            sb.AppendLine("{");
            sb.AppendLine($"    internal readonly {svcName} _target;");
            foreach (var p in paramList)
                sb.AppendLine($"    internal readonly {p.Type.ToDisplayString()} _{p.Name};");

            sb.AppendLine($"    public string MethodName => \"{method.Name}\";");
            sb.AppendLine($"    public System.Type ServiceType => typeof({svcName});");
            sb.AppendLine($"    public {resultName} Result {{ get; set; }}");
            sb.AppendLine();

            var paramDecl =
                paramList.Count > 0
                    ? ", "
                        + string.Join(
                            ", ",
                            paramList.Select(p => $"{p.Type.ToDisplayString()} {p.Name}")
                        )
                    : "";
            sb.AppendLine($"    public {structName}{typeParamDecl}({svcName} target{paramDecl})");
            sb.AppendLine("    {");
            sb.AppendLine("        _target = target;");
            foreach (var p in paramList)
                sb.AppendLine($"        _{p.Name} = {p.Name};");
            sb.AppendLine("        Result = default!;");
            sb.AppendLine("    }");
            sb.AppendLine();

            var paramArgs = string.Join(", ", paramList.Select(p => $"_{p.Name}"));
            var isAsyncReturn =
                retType
                    is INamedTypeSymbol
                    {
                        MetadataName: "ValueTask`1" or "Task`1" or "ValueTask" or "Task"
                    };
            var isVoidAsync = retType is INamedTypeSymbol { MetadataName: "ValueTask" or "Task" };
            var isSystemTask = retType is INamedTypeSymbol { MetadataName: "Task" or "Task`1" };
            var invokeTargetReturnType = isAsyncReturn ? retType.ToDisplayString() : resultName;
            var isVoidSync = retType.SpecialType == SpecialType.System_Void;

            var methodTypeArgs = isGeneric
                ? "<" + string.Join(", ", typeParams.Select(tp => tp.Name)) + ">"
                : "";

            if (isVoidSync)
            {
                sb.AppendLine(
                    $"    public {invokeTargetReturnType} InvokeTarget() {{ _target.{method.Name}{methodTypeArgs}({paramArgs}); return default; }}"
                );
            }
            else
            {
                sb.AppendLine(
                    $"    public {invokeTargetReturnType} InvokeTarget() => _target.{method.Name}{methodTypeArgs}({paramArgs});"
                );
            }

            if (isAsyncReturn)
            {
                var asyncReturn = isVoidAsync
                    ? "global::System.Threading.Tasks.ValueTask"
                    : $"global::System.Threading.Tasks.ValueTask<{resultName}>";
                if (isSystemTask)
                    sb.AppendLine(
                        $"    public async {asyncReturn} InvokeTargetAsync() => await _target.{method.Name}{methodTypeArgs}({paramArgs});"
                    );
                else
                    sb.AppendLine(
                        $"    public {asyncReturn} InvokeTargetAsync() => _target.{method.Name}{methodTypeArgs}({paramArgs});"
                    );
            }

            sb.AppendLine("}");
            sb.AppendLine();
        }
    }

    private static void EmitDecoratorClass(
        StringBuilder sb,
        INamedTypeSymbol serviceType,
        ITypeSymbol? implType,
        INamedTypeSymbol interceptorType,
        IReadOnlyList<ITypeSymbol> allInterceptors,
        int index,
        bool isLast
    )
    {
        var safeSvc = Sanitize(serviceType.ToDisplayString());
        var safeInt = Sanitize(interceptorType.Name);
        var className = $"{safeSvc}_{safeInt}Decorator";
        var svcName = serviceType.ToDisplayString();
        var intName = interceptorType.ToDisplayString();
        var innerTypeName = isLast
            ? (implType?.ToDisplayString() ?? svcName)
            : Sanitize(serviceType.ToDisplayString())
                + "_"
                + Sanitize(((INamedTypeSymbol)allInterceptors[index + 1]).Name)
                + "Decorator";

        sb.AppendLine($"sealed class {className} : {svcName}");
        sb.AppendLine("{");
        sb.AppendLine($"    private readonly {svcName} _inner;");
        sb.AppendLine($"    private readonly {intName} _i0;");
        sb.AppendLine();
        sb.AppendLine($"    public {className}({svcName} inner, {intName} i0)");
        sb.AppendLine("    {");
        sb.AppendLine("        _inner = inner;");
        sb.AppendLine("        _i0 = i0;");
        sb.AppendLine("    }");
        sb.AppendLine();

        foreach (
            var method in GetAllMethods(serviceType)
                .Where(m => m.MethodKind == MethodKind.Ordinary && !HasRefLikeParameters(m))
        )
        {
            var retType = method.ReturnType.ToDisplayString();
            var isVoidTask =
                method.ReturnType is INamedTypeSymbol { MetadataName: "ValueTask" or "Task" };
            var isTaskOf =
                method.ReturnType is INamedTypeSymbol { MetadataName: "ValueTask`1" or "Task`1" };
            var isSystemTask =
                method.ReturnType is INamedTypeSymbol { MetadataName: "Task" or "Task`1" };
            var isVoid = isVoidTask || method.ReturnType.SpecialType == SpecialType.System_Void;

            var paramDecl = string.Join(
                ", ",
                method.Parameters.Select(p => $"{p.Type.ToDisplayString()} {p.Name}")
            );
            var paramArgs = method.Parameters.Any()
                ? ", " + string.Join(", ", method.Parameters.Select(p => p.Name))
                : "";

            // Generic method support — emit <T1, T2> and any where constraints
            var typeParams = method.TypeParameters;
            var isGeneric = typeParams.Length > 0;
            var methodTypeParams = isGeneric
                ? "<" + string.Join(", ", typeParams.Select(tp => tp.Name)) + ">"
                : "";
            var methodConstraints = isGeneric ? BuildGenericConstraints(typeParams) : "";

            var paramTypeSuffix = "";
            if (method.Parameters.Length > 0)
            {
                foreach (var p in method.Parameters)
                    paramTypeSuffix += $"_{Sanitize(p.Type.Name)}";
            }
            var structRef = $"{safeSvc}_{safeInt}_{method.Name}{paramTypeSuffix}_Invocation";

            sb.AppendLine(
                $"    public {retType} {method.Name}{methodTypeParams}({paramDecl}){methodConstraints}"
            );
            sb.AppendLine("    {");
            sb.AppendLine(
                $"        var inv = new {structRef}{methodTypeParams}(_inner{paramArgs});"
            );
            if (isVoidTask)
            {
                if (isSystemTask)
                    sb.AppendLine(
                        $"        return _i0.InvokeAsyncVoid(inv, static async i => {{ await (({structRef}{methodTypeParams})i).InvokeTargetAsync(); }}).AsTask();"
                    );
                else
                    sb.AppendLine(
                        $"        return _i0.InvokeAsyncVoid(inv, static async i => {{ await (({structRef}{methodTypeParams})i).InvokeTargetAsync(); }});"
                    );
            }
            else if (isTaskOf)
            {
                if (isSystemTask)
                    sb.AppendLine(
                        $"        return _i0.InvokeAsync(inv, static async i => await (({structRef}{methodTypeParams})i).InvokeTargetAsync()).AsTask();"
                    );
                else
                    sb.AppendLine(
                        $"        return _i0.InvokeAsync(inv, static async i => await (({structRef}{methodTypeParams})i).InvokeTargetAsync());"
                    );
            }
            else if (isVoid)
                sb.AppendLine(
                    $"        _i0.InvokeVoid(inv, static i => (({structRef}{methodTypeParams})i).InvokeTarget());"
                );
            else
                sb.AppendLine(
                    $"        return _i0.Invoke(inv, static i => (({structRef}{methodTypeParams})i).InvokeTarget());"
                );
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        // Property delegation (not intercepted)
        foreach (var prop in GetAllProperties(serviceType).Where(p => !p.IsIndexer && !p.IsStatic))
        {
            var propType = prop.Type.ToDisplayString();
            var hasGetter = prop.GetMethod is { DeclaredAccessibility: Accessibility.Public };
            var hasSetter =
                prop.SetMethod is { DeclaredAccessibility: Accessibility.Public }
                && !prop.SetMethod.IsInitOnly;
            var isInitOnly =
                prop.SetMethod is { DeclaredAccessibility: Accessibility.Public }
                && prop.SetMethod.IsInitOnly;

            sb.AppendLine($"    public {propType} {prop.Name}");
            sb.AppendLine("    {");
            if (hasGetter)
                sb.AppendLine($"        get => _inner.{prop.Name};");
            if (hasSetter)
                sb.AppendLine($"        set => _inner.{prop.Name} = value;");
            if (isInitOnly)
                sb.AppendLine(
                    $"        init {{ /* init-only: silently no-op; _inner already constructed */ }}"
                );
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        // ref/out/in methods — delegated directly
        foreach (
            var method in GetAllMethods(serviceType)
                .Where(m => m.MethodKind == MethodKind.Ordinary && HasRefLikeParameters(m))
        )
        {
            var retType = method.ReturnType.ToDisplayString();
            var paramDecl = string.Join(
                ", ",
                method
                    .Parameters
                    .Select(
                        p => $"{GetRefKindPrefix(p.RefKind)}{p.Type.ToDisplayString()} {p.Name}"
                    )
            );
            var paramArgs = string.Join(
                ", ",
                method.Parameters.Select(p => $"{GetRefKindPrefix(p.RefKind)}{p.Name}")
            );

            sb.AppendLine($"    public {retType} {method.Name}({paramDecl})");
            sb.AppendLine($"        => _inner.{method.Name}({paramArgs});");
            sb.AppendLine();
        }

        sb.AppendLine("}");
        sb.AppendLine();
    }

    private static void EmitRegistration(
        StringBuilder sb,
        INamedTypeSymbol serviceType,
        ITypeSymbol? implType,
        INamedTypeSymbol interceptorType,
        bool isLast
    )
    {
        var effectiveImplType = implType ?? serviceType;
        var safeSvc = Sanitize(serviceType.ToDisplayString());
        var safeInt = Sanitize(interceptorType.Name);
        var decoratorClass = $"{safeSvc}_{safeInt}Decorator";
        var svcName = serviceType.ToDisplayString();
        var intName = interceptorType.ToDisplayString();
        var implName = effectiveImplType.ToDisplayString();
        var resolveType = isLast ? implName : svcName;

        sb.AppendLine($"        container.Register<{svcName}>(scope =>");
        sb.AppendLine("        {");
        sb.AppendLine($"            var inner = scope.GetService<{resolveType}>();");
        sb.AppendLine($"            var i0 = scope.GetService<{intName}>();");
        sb.AppendLine($"            return new {decoratorClass}(inner, i0);");
        sb.AppendLine("        }, SvcLifetime.Scoped);");
        sb.AppendLine();
    }
}
