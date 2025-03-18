namespace PicoHex.IoC;

public class SvcContainer : ISvcProvider
{
    private readonly IDictionary<Type, Func<ISvcProvider, object>> _factories =
        new Dictionary<Type, Func<ISvcProvider, object>>();
    private readonly Stack<Type> _resolving = new();

    public void Register<TImplementation>() => Register<TImplementation, TImplementation>();

    public void Register<TInterface, TImplementation>()
        where TImplementation : TInterface
    {
        RegisterImplementation<TImplementation>();
        RegisterMapping<TInterface, TImplementation>();
    }

    private void RegisterImplementation<T>() => RegisterMapping<T, T>();

    private void RegisterMapping<TInterface, TImplementation>()
    {
        var interfaceType = typeof(TInterface);
        var implementationType = typeof(TImplementation);
        RegisterMapping(interfaceType, implementationType);
    }

    private void RegisterMapping(Type interfaceType, Type implementationType)
    {
        if (_factories.ContainsKey(interfaceType))
            return;

        var factory = CreateFactory(implementationType);
        _factories[interfaceType] = factory;
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

    private static Func<ISvcProvider, object> CreateFactory(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type type
    )
    {
        var constructors = type.GetConstructors();
        var selectedConstructor = constructors
            .OrderByDescending(c => c.GetParameters().Length)
            .First();
        var parameters = selectedConstructor.GetParameters();

        var providerParam = Expression.Parameter(typeof(ISvcProvider), "sp");
        var args = parameters
            .Select(p =>
            {
                var getServiceCall = Expression.Call(
                    providerParam,
                    typeof(ISvcProvider).GetMethod(nameof(GetService), [typeof(Type)])!,
                    Expression.Constant(p.ParameterType)
                );
                return Expression.Convert(getServiceCall, p.ParameterType); // 关键转换
            })
            .ToArray<Expression>();

        var newExpr = Expression.New(selectedConstructor, args);
        var lambda = Expression.Lambda<Func<ISvcProvider, object>>(newExpr, providerParam);
        var factory = lambda.Compile();

        return factory;
    }
}
