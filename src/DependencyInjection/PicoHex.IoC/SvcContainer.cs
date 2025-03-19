namespace PicoHex.IoC;

public class SvcContainer : ISvcProvider
{
    private readonly Dictionary<Type, Func<ISvcProvider, object>> _factories = new();
    private readonly Stack<Type> _resolving = new();

    public void Register<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            TImplementation
    >() => Register<TImplementation, TImplementation>();

    public void Register<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TInterface,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            TImplementation
    >()
        where TImplementation : TInterface
    {
        RegisterImplementation<TImplementation>();
        RegisterMapping<TInterface, TImplementation>();
    }

    private void RegisterImplementation<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T
    >() => RegisterMapping<T, T>();

    private void RegisterMapping<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TInterface,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            TImplementation
    >()
    {
        RegisterMapping(typeof(TInterface), typeof(TImplementation));
    }

    private void RegisterMapping(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type interfaceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type implementationType
    )
    {
        if (_factories.ContainsKey(interfaceType))
            return;

        _factories[interfaceType] = CreateAotFactory(implementationType);
    }

    public object GetService(Type serviceType)
    {
        if (!_factories.TryGetValue(serviceType, out var entry))
            throw new InvalidOperationException($"Type {serviceType.Name} is not registered.");

        // 循环依赖检测
        if (_resolving.Contains(serviceType))
        {
            var cycle = string.Join(" → ", _resolving.Reverse().Select(t => t.Name));
            throw new InvalidOperationException(
                $"Circular dependency detected: {cycle} → {serviceType.Name}"
            );
        }

        _resolving.Push(serviceType);
        try
        {
            return entry(this); // 使用当前容器作为服务提供者
        }
        finally
        {
            _resolving.Pop();
        }
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
                    typeof(ISvcProvider).GetMethod(nameof(ISvcProvider.GetService))!,
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
