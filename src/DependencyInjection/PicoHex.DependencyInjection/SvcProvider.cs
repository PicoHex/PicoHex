namespace PicoHex.DependencyInjection;

public class SvcProvider(ISvcRegistry registry, ISvcScopeFactory scopeFactory) : ISvcProvider
{
    public object? Resolve(Type serviceType)
    {
        var factory = registry.GetOrAddInstanceFactory(
            serviceType,
            svcProvider =>
            {
                var resolutionStack = new Stack<Type>();
                return Resolve(serviceType, svcProvider, resolutionStack);
            }
        );

        return factory(this);
    }

    public ISvcScope CreateScope() => scopeFactory.CreateScope(this);

    private object? Resolve(Type serviceType, ISvcProvider svcProvider, Stack<Type> resolutionStack)
    {
        var descriptor = registry.GetServiceDescriptor(serviceType);

        // 处理闭合泛型类型：检查是否存在对应的开放泛型注册
        if (!IsClosedGenericType(serviceType))
            return descriptor.Lifetime switch
            {
                SvcLifetime.Singleton
                    => registry.GetSingletonInstance(
                        serviceType,
                        () => CreateInstance(descriptor, svcProvider, resolutionStack)
                    ),
                SvcLifetime.PerThread
                    => registry.GetPerThreadInstance(
                        serviceType,
                        () => CreateInstance(descriptor, svcProvider, resolutionStack)
                    ),
                _ => CreateInstance(descriptor, svcProvider, resolutionStack)
            };
        // 动态构造闭合实现类型
        if (descriptor.ImplementationType == null)
            return descriptor.Lifetime switch
            {
                SvcLifetime.Singleton
                    => registry.GetSingletonInstance(
                        serviceType,
                        () => CreateInstance(descriptor, svcProvider, resolutionStack)
                    ),
                SvcLifetime.PerThread
                    => registry.GetPerThreadInstance(
                        serviceType,
                        () => CreateInstance(descriptor, svcProvider, resolutionStack)
                    ),
                _ => CreateInstance(descriptor, svcProvider, resolutionStack)
            };
        var closedImplType = descriptor
            .ImplementationType
            .MakeGenericType(serviceType.GetGenericArguments());

        // 创建临时描述符并解析
        var tempDescriptor = new SvcDescriptor(serviceType, closedImplType, descriptor.Lifetime);
        return CreateInstance(tempDescriptor, svcProvider, new Stack<Type>());
    }

    private static bool IsClosedGenericType(Type type) =>
        type is { IsGenericType: true, IsGenericTypeDefinition: false };

    private object? CreateInstance(
        SvcDescriptor descriptor,
        ISvcProvider svcProvider,
        Stack<Type> resolutionStack
    )
    {
        if (descriptor.Factory is not null)
            return descriptor.Factory(svcProvider);

        if (descriptor.ImplementationType is null)
            throw new InvalidOperationException("No factory or implementation type found.");

        var implementationType = descriptor.ImplementationType;

        // Check for circular dependencies
        if (resolutionStack.Contains(implementationType))
            throw new InvalidOperationException(
                $"Circular dependency detected for type {implementationType.Name}."
            );

        var constructor =
            descriptor.Constructor
            ?? throw new InvalidOperationException(
                $"No public constructor found for type {implementationType.Name}"
            );

        // 处理开放泛型实现类型（需动态闭合）
        if (implementationType.IsGenericTypeDefinition)
        {
            var serviceGenericArgs = descriptor.ServiceType.GetGenericArguments();
            implementationType = implementationType.MakeGenericType(serviceGenericArgs);
        }
        resolutionStack.Push(implementationType);
        try
        {
            var parameters = constructor
                .Parameters
                .Select(param => Resolve(param.ParameterType, svcProvider, resolutionStack))
                .ToArray();
            return constructor.ConstructorInfo.Invoke(parameters);
        }
        finally
        {
            resolutionStack.Pop();
        }
    }
}
