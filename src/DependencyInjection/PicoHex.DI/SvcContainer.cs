namespace PicoHex.DI;

public sealed class SvcContainer(ISvcProviderFactory providerFactory) : ISvcContainer
{
    private readonly ConcurrentDictionary<Type, SvcDescriptor> _descriptors = new();

    public ISvcContainer Register(SvcDescriptor descriptor)
    {
        if (descriptor.Factory is null && descriptor.SingleInstance is null)
            descriptor.Factory = CreateAotFactory(descriptor.ImplementationType);

        _descriptors.TryAdd(descriptor.ServiceType, descriptor);
        _descriptors.TryAdd(descriptor.ImplementationType, descriptor);
        return this;
    }

    public ISvcProvider CreateProvider() => providerFactory.CreateProvider(this);

    public SvcDescriptor? GetDescriptor(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type type
    ) => _descriptors.GetValueOrDefault(type);

    private static Func<ISvcProvider, object> CreateAotFactory(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type type
    )
    {
        var constructor = type.GetConstructors()
            .OrderByDescending(c => c.GetParameters().Length)
            .First();

        var parameters = constructor.GetParameters();
        var providerParam = Expression.Parameter(typeof(ISvcProvider), "sp");

        var args = parameters
            .Select(p =>
            {
                var getServiceCall = Expression.Call(
                    providerParam,
                    typeof(ISvcResolver).GetMethod(nameof(ISvcResolver.Resolve))!,
                    Expression.Constant(p.ParameterType)
                );
                return Expression.Convert(getServiceCall, p.ParameterType);
            })
            .ToArray<Expression>();

        var newExpr = Expression.New(constructor, args);
        var lambda = Expression.Lambda<Func<ISvcProvider, object>>(newExpr, providerParam);
        return lambda.Compile();
    }
}
