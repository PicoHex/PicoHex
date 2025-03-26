namespace PicoHex.DI;

public sealed class SvcProvider(ISvcContainer container, ISvcScopeFactory scopeFactory)
    : ISvcProvider
{
    private readonly AsyncLocal<ResolutionContext?> _asyncContext = new();

    public object Resolve(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type serviceType
    )
    {
        var context = _asyncContext.Value ??= new ResolutionContext();

        if (!context.TryEnterResolution(serviceType, out var cyclePath))
            throw new InvalidOperationException($"Circular dependency detected: {cyclePath}");

        try
        {
            var svcDescriptor = container.GetDescriptor(serviceType);

            if (svcDescriptor is null)
                throw new InvalidOperationException($"Type {serviceType.Name} is not registered.");

            if (svcDescriptor.Factory is null && svcDescriptor.SingleInstance is null)
                svcDescriptor.Factory = CreateAotFactory(svcDescriptor.ImplementationType);

            return svcDescriptor.Lifetime switch
            {
                SvcLifetime.Transient => svcDescriptor.Factory!(this),
                SvcLifetime.Singleton => GetSingleton(svcDescriptor),
                SvcLifetime.Scoped => svcDescriptor.Factory!(this),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
        finally
        {
            context.ExitResolution();
            if (context.IsEmpty)
                _asyncContext.Value = null;
        }
    }

    private object GetSingleton(SvcDescriptor svcDescriptor)
    {
        if (svcDescriptor.SingleInstance is not null)
            return svcDescriptor.SingleInstance;
        lock (svcDescriptor)
            return svcDescriptor.SingleInstance ??= svcDescriptor.Factory!(this);
    }

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

    public ISvcScope CreateScope() => scopeFactory.CreateScope(this);
}
