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

        if (
            serviceType.IsGenericType
            && serviceType.GetGenericTypeDefinition() == typeof(IEnumerable<>)
        )
        {
            var elementType = serviceType.GetGenericArguments()[0];
            return ResolveAll(elementType, context);
        }

        return ResolveLast(serviceType, context);
    }

    private object ResolveLast(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type serviceType,
        ResolutionContext context
    )
    {
        var svcDescriptor = container.GetDescriptor(serviceType);

        if (svcDescriptor is null)
            throw new InvalidOperationException($"Type {serviceType.Name} is not registered.");

        return GetOrCreateInstance(svcDescriptor, context);
    }

    private object ResolveAll(Type elementType, ResolutionContext context)
    {
        var descriptors = container.GetDescriptors(elementType);

        if (descriptors is null || descriptors.Count is 0)
        {
            var emptyArray = Array.CreateInstance(elementType, 0);
            return emptyArray;
        }

        var instances = descriptors.Select(d => GetOrCreateInstance(d, context)).ToArray();

        var array = Array.CreateInstance(elementType, instances.Length);
        Array.Copy(instances, array, instances.Length);
        return array;
    }

    private object GetOrCreateInstance(SvcDescriptor svcDescriptor, ResolutionContext context)
    {
        try
        {
            if (!context.TryEnterResolution(svcDescriptor.ServiceType, out var cyclePath))
                throw new InvalidOperationException($"Circular dependency detected: {cyclePath}");
            return svcDescriptor.Lifetime switch
            {
                SvcLifetime.Transient => GetTransientInstance(svcDescriptor),
                SvcLifetime.Singleton => GetSingletonInstance(svcDescriptor),
                SvcLifetime.Scoped => GetScopedInstance(svcDescriptor),
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

    private object GetTransientInstance(SvcDescriptor svcDescriptor)
    {
        if (svcDescriptor.Factory is not null)
            return svcDescriptor.Factory(this);
        lock (svcDescriptor)
            svcDescriptor.Factory ??= CreateAotFactory(svcDescriptor.ImplementationType);
        return svcDescriptor.Factory(this);
    }

    private object GetSingletonInstance(SvcDescriptor svcDescriptor)
    {
        if (svcDescriptor.SingleInstance is not null)
            return svcDescriptor.SingleInstance;
        lock (svcDescriptor)
        {
            if (svcDescriptor.SingleInstance is not null)
                return svcDescriptor.SingleInstance;
            svcDescriptor.SingleInstance ??= svcDescriptor.Factory is null
                ? CreateAotFactory(svcDescriptor.ImplementationType)(this)
                : svcDescriptor.Factory(this);
        }

        return svcDescriptor.SingleInstance;
    }

    private object GetScopedInstance(SvcDescriptor svcDescriptor) =>
        scopeFactory.CreateScope(this).Resolve(svcDescriptor.ServiceType);

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
