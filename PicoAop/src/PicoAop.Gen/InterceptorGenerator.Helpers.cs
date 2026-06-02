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

    /// <summary>
    /// Returns all methods from <paramref name="type"/> including those inherited
    /// from base interfaces. Standard <c>GetMembers()</c> does not return members
    /// declared on parent interfaces.
    /// </summary>
    private static IEnumerable<IMethodSymbol> GetAllMethods(INamedTypeSymbol type)
    {
        var seen = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
        foreach (var m in type.GetMembers().OfType<IMethodSymbol>())
        {
            if (seen.Add(m))
                yield return m;
        }
        foreach (var iface in type.AllInterfaces)
        {
            foreach (var m in iface.GetMembers().OfType<IMethodSymbol>())
            {
                if (seen.Add(m))
                    yield return m;
            }
        }
    }

    /// <summary>
    /// Returns all properties from <paramref name="type"/> including those inherited
    /// from base interfaces.
    /// </summary>
    private static IEnumerable<IPropertySymbol> GetAllProperties(INamedTypeSymbol type)
    {
        var seen = new HashSet<IPropertySymbol>(SymbolEqualityComparer.Default);
        foreach (var p in type.GetMembers().OfType<IPropertySymbol>())
        {
            if (seen.Add(p))
                yield return p;
        }
        foreach (var iface in type.AllInterfaces)
        {
            foreach (var p in iface.GetMembers().OfType<IPropertySymbol>())
            {
                if (seen.Add(p))
                    yield return p;
            }
        }
    }

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
