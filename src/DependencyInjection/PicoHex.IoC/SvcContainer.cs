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

        _factories[interfaceType] = CreateFactory(implementationType);
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

        return AotFactoryGenerator.CreateFactory(selectedConstructor); // 关键修改点
    }
}
