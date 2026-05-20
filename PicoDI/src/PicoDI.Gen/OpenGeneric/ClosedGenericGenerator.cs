namespace PicoDI.Gen.OpenGeneric;

/// <summary>
/// Generates closed generic service registrations from open generic registrations and closed generic usages.
/// </summary>
internal sealed class ClosedGenericGenerator
{
    private readonly TypeParameterSubstitutor _substitutor;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClosedGenericGenerator"/> class.
    /// </summary>
    /// <param name="substitutor">The type parameter substitutor to use.</param>
    public ClosedGenericGenerator(TypeParameterSubstitutor substitutor)
    {
        _substitutor = substitutor;
    }

    /// <summary>
    /// Generates closed generic service registrations by combining open generic registrations with closed generic usages.
    /// </summary>
    /// <param name="openGenerics">Open generic registrations discovered in the codebase.</param>
    /// <param name="closedUsages">Closed generic type usages found in the codebase.</param>
    /// <returns>A list of service registrations for closed generic types.</returns>
    public List<ServiceRegistration> GenerateClosedGenericRegistrations(
        List<OpenGenericRegistration> openGenerics,
        List<ClosedGenericUsage> closedUsages
    )
    {
        var result = new List<ServiceRegistration>();

        foreach (var usage in closedUsages)
        {
            var openGeneric = openGenerics.FirstOrDefault(
                og => og.OpenServiceTypeFullName == usage.OpenServiceTypeFullName
            );
            if (openGeneric is null)
                continue;

            var closedImplementationTypeFullName = _substitutor.BuildClosedGenericTypeName(
                openGeneric.OpenImplementationTypeFullName,
                usage.TypeArgumentsFullNames
            );

            // Prefer Roslyn-based symbol substitution for accurate handling of nested generics
            var constructorParameters = useSymbolSubstitution(openGeneric, usage);

            result.Add(
                new ServiceRegistration(
                    TypeNameDisplay.GetSimpleName(usage.ClosedServiceTypeFullName),
                    usage.ClosedServiceTypeFullName,
                    TypeNameDisplay.GetSimpleName(closedImplementationTypeFullName),
                    closedImplementationTypeFullName,
                    openGeneric.Lifetime,
                    false,
                    constructorParameters
                )
            );
        }

        return result;
    }

    private ImmutableArray<string> useSymbolSubstitution(
        OpenGenericRegistration openGeneric,
        ClosedGenericUsage usage
    )
    {
        if (
            !openGeneric.ConstructorParameterTypeSymbols.IsDefaultOrEmpty
            && !usage.TypeArgumentSymbols.IsDefaultOrEmpty
        )
        {
            return _substitutor.SubstituteTypeParametersWithSymbols(
                openGeneric.ConstructorParameterTypeSymbols,
                usage.TypeArgumentSymbols,
                openGeneric.TypeParameterNames
            );
        }

        // Fallback to string-based substitution when symbols are unavailable
        // (e.g., when ClosedGenericUsage was parsed from a string representation)
        var typeParameterMap = new Dictionary<string, string>();
        for (
            var index = 0;
            index < openGeneric.TypeParameterNames.Length
                && index < usage.TypeArgumentsFullNames.Length;
            index++
        )
        {
            typeParameterMap[openGeneric.TypeParameterNames[index]] = usage.TypeArgumentsFullNames[
                index
            ];
        }

        return openGeneric
            .ConstructorParameters
            .Select(
                typeFullName =>
                    _substitutor.SubstituteTypeParameters(typeFullName, typeParameterMap)
            )
            .ToImmutableArray();
    }
}
