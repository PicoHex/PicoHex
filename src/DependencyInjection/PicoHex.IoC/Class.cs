using System.Reflection;

namespace PicoHex.IoC;

public interface IServiceProvider
{
    object GetService(Type serviceType);
}

public class SvcContainer : IServiceProvider
{
    private readonly IDictionary<
        Type,
        (Func<IServiceProvider, object> factory, ConstructorInfo constructor)
    > _factories = new Dictionary<Type, (Func<IServiceProvider, object>, ConstructorInfo)>();
    private readonly Stack<Type> _resolving = new();

    public void Register<TInterface, TImplementation>()
        where TImplementation : TInterface
    {
        RegisterImplementation<TImplementation>();
        RegisterMapping<TInterface, TImplementation>();
    }

    public void Register<T>()
        where T : class
    {
        Register<T, T>();
    }

    private void RegisterImplementation<T>()
    {
        var type = typeof(T);
        var constructors = type.GetConstructors();
        var selectedConstructor = SelectConstructor(constructors);
        var parameters = selectedConstructor.GetParameters();

        // 创建工厂函数
        Func<IServiceProvider, object> factory = sp =>
        {
            var args = parameters.Select(p => sp.GetService(p.ParameterType)).ToArray();
            return selectedConstructor.Invoke(args);
        };

        _factories[type] = (factory, selectedConstructor);
    }

    private void RegisterMapping<TInterface, TImplementation>()
    {
        var interfaceType = typeof(TInterface);
        var implementationType = typeof(TImplementation);

        if (!_factories.ContainsKey(interfaceType))
        {
            _factories[interfaceType] = (sp => sp.GetService(implementationType), null);
        }
    }

    public object GetService(Type serviceType)
    {
        if (!_factories.TryGetValue(serviceType, out var entry))
        {
            throw new InvalidOperationException($"Type {serviceType.Name} is not registered.");
        }

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
            return entry.factory(this); // 使用当前容器作为服务提供者
        }
        finally
        {
            _resolving.Pop();
        }
    }

    private static ConstructorInfo SelectConstructor(ConstructorInfo[] constructors)
    {
        // 默认选择参数最多的构造函数
        // 如果需要支持自定义属性，可以添加特性检测逻辑
        return constructors.OrderByDescending(c => c.GetParameters().Length).First();
    }

    object IServiceProvider.GetService(Type serviceType)
    {
        return GetService(serviceType);
    }
}
