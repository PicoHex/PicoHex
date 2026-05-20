namespace PicoDI.Gen;

internal enum ImplementationActivationStatus
{
    Valid,
    AbstractOrInterface,
    MissingPublicConstructor,
    MultipleMarkedConstructors
}

internal readonly record struct ImplementationActivationAnalysis(
    INamedTypeSymbol ImplementationType,
    ImplementationActivationStatus Status,
    IMethodSymbol? SelectedConstructor,
    ImmutableArray<string> ConstructorParameterTypes,
    ImmutableArray<ITypeSymbol> ConstructorParameterTypeSymbols
)
{
    public bool IsValid => Status == ImplementationActivationStatus.Valid;

    public Diagnostic? CreateDiagnostic(Location? location = null)
    {
        var descriptor = Status switch
        {
            ImplementationActivationStatus.AbstractOrInterface
                => DiagnosticDescriptors.AbstractTypeRegistration,
            ImplementationActivationStatus.MissingPublicConstructor
                => DiagnosticDescriptors.MissingPublicConstructor,
            ImplementationActivationStatus.MultipleMarkedConstructors
                => DiagnosticDescriptors.MultipleMarkedConstructors,
            _ => null
        };

        return descriptor is null
            ? null
            : Diagnostic.Create(descriptor, location ?? Location.None, ImplementationType.Name);
    }
}

internal static class ImplementationActivationAnalyzer
{
    public static ImplementationActivationAnalysis Analyze(INamedTypeSymbol implementationType)
    {
        if (implementationType.TypeKind == TypeKind.Interface || implementationType.IsAbstract)
        {
            return CreateInvalid(
                implementationType,
                ImplementationActivationStatus.AbstractOrInterface
            );
        }

        var publicConstructors = implementationType
            .Constructors
            .Where(c => !c.IsStatic && c.DeclaredAccessibility == Accessibility.Public)
            .ToImmutableArray();

        if (publicConstructors.IsEmpty)
        {
            return implementationType.IsValueType
                ? CreateValid(implementationType, selectedConstructor: null)
                : CreateInvalid(
                    implementationType,
                    ImplementationActivationStatus.MissingPublicConstructor
                );
        }

        var markedConstructors = publicConstructors
            .Where(HasSvcConstructorAttribute)
            .ToImmutableArray();

        if (markedConstructors.Length > 1)
        {
            return CreateInvalid(
                implementationType,
                ImplementationActivationStatus.MultipleMarkedConstructors
            );
        }

        var selectedConstructor =
            markedConstructors.Length == 1
                ? markedConstructors[0]
                : publicConstructors.OrderByDescending(c => c.Parameters.Length).First();

        return CreateValid(implementationType, selectedConstructor);
    }

    private static ImplementationActivationAnalysis CreateValid(
        INamedTypeSymbol implementationType,
        IMethodSymbol? selectedConstructor
    )
    {
        return new ImplementationActivationAnalysis(
            implementationType,
            ImplementationActivationStatus.Valid,
            selectedConstructor,
            GetConstructorParameterTypes(selectedConstructor),
            GetConstructorParameterTypeSymbols(selectedConstructor)
        );
    }

    private static ImplementationActivationAnalysis CreateInvalid(
        INamedTypeSymbol implementationType,
        ImplementationActivationStatus status
    )
    {
        return new ImplementationActivationAnalysis(
            implementationType,
            status,
            SelectedConstructor: null,
            ConstructorParameterTypes: ImmutableArray<string>.Empty,
            ConstructorParameterTypeSymbols: ImmutableArray<ITypeSymbol>.Empty
        );
    }

    private static ImmutableArray<string> GetConstructorParameterTypes(IMethodSymbol? constructor)
    {
        return constructor is null
            ? ImmutableArray<string>.Empty
            :
            [
                .. constructor.Parameters.Select(p =>
                    p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                )
            ];
    }

    private static ImmutableArray<ITypeSymbol> GetConstructorParameterTypeSymbols(
        IMethodSymbol? constructor
    )
    {
        return constructor is null
            ? ImmutableArray<ITypeSymbol>.Empty
            : [.. constructor.Parameters.Select(p => p.Type)];
    }

    private static bool HasSvcConstructorAttribute(IMethodSymbol constructor)
    {
        return constructor
            .GetAttributes()
            .Any(
                attr =>
                    attr.AttributeClass?.ToDisplayString()
                    == PicoDiNames.SvcConstructorAttributeFullName
            );
    }
}
