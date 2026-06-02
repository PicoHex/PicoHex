namespace PicoAop.Gen;

public sealed partial class InterceptorGenerator
{
    /// <summary>
    /// Produces a legal C# identifier from a type name by replacing special characters
    /// with underscores.
    /// </summary>
    private static string Sanitize(string name)
    {
        return name.Replace("<", "_")
            .Replace(">", "_")
            .Replace(",", "_")
            .Replace(".", "_")
            .Replace(" ", "");
    }

    private static bool ImplementsIInterceptor(INamedTypeSymbol type)
    {
        foreach (var iface in type.AllInterfaces)
        {
            if (iface.ToDisplayString() == PicoAopNames.IInterceptorFull)
                return true;
        }

        for (var baseType = type.BaseType; baseType is not null; baseType = baseType.BaseType)
        {
            if (baseType.ToDisplayString() == PicoAopNames.InterceptorBaseFull)
                return true;
        }

        return type.ToDisplayString() == PicoAopNames.IInterceptorFull;
    }

    private static bool HasRefLikeParameters(IMethodSymbol method) =>
        method.Parameters.Any(p => p.RefKind != RefKind.None);

    private static string GetRefKindPrefix(RefKind refKind) =>
        refKind switch
        {
            RefKind.Ref => "ref ",
            RefKind.Out => "out ",
            RefKind.In => "in ",
            _ => ""
        };

    /// <summary>
    /// Builds C# where clauses for generic method type parameters.
    /// Example: "where T : class, IComparable<T>, new()"
    /// </summary>
    private static string BuildGenericConstraints(
        System.Collections.Immutable.ImmutableArray<ITypeParameterSymbol> typeParams
    )
    {
        var sb = new StringBuilder();
        foreach (var tp in typeParams)
        {
            var constraints = new List<string>();

            if (tp.HasReferenceTypeConstraint)
                constraints.Add("class");
            else if (tp.HasValueTypeConstraint)
                constraints.Add("struct");

            if (tp.HasNotNullConstraint)
                constraints.Add("notnull");

            if (tp.HasUnmanagedTypeConstraint)
                constraints.Add("unmanaged");

            foreach (var ct in tp.ConstraintTypes)
                constraints.Add(ct.ToDisplayString());

            if (tp.HasConstructorConstraint)
                constraints.Add("new()");

            if (constraints.Count > 0)
            {
                sb.Append(" where ");
                sb.Append(tp.Name);
                sb.Append(" : ");
                sb.Append(string.Join(", ", constraints));
            }
        }
        return sb.ToString();
    }
}
