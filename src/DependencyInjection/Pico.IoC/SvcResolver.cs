using Pico.IoC.Internal;

namespace Pico.IoC;

public class SvcResolver(ISvcContainer container, ISvcProvider provider) : ISvcResolver
{
    public object Resolve(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type serviceType
    ) =>
        IsEnumerableRequest(serviceType, out var elementType)
            ? ResolveAllServices(elementType)
            : ResolveInstance(container.GetDescriptor(serviceType));

    #region private

    private static bool IsEnumerableRequest(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            out Type elementType
    )
    {
        elementType = null!;
        if (
            !serviceType.IsGenericType
            || serviceType.GetGenericTypeDefinition() != typeof(IEnumerable<>)
        )
            return false;

        elementType = serviceType.GetGenericArguments()[0];
        return true;
    }

    private object ResolveAllServices(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type elementType
    )
    {
        var descriptors =
            container.GetDescriptors(elementType)
            ?? throw new ServiceNotRegisteredException(elementType);

        return descriptors.Count is 0
            ? Array.CreateInstance(elementType, 0)
            : CreateServiceArray(elementType, descriptors);
    }

    private Array CreateServiceArray(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type elementType,
        IList<SvcDescriptor> descriptors
    )
    {
        var instances = descriptors.Select(ResolveInstance).ToArray();
        var array = Array.CreateInstance(elementType, instances.Length);
        Array.Copy(instances, array, instances.Length);
        return array;
    }

    private object ResolveInstance(SvcDescriptor descriptor)
    {
        if (descriptor.Lifetime is SvcLifetime.Singleton && descriptor.SingleInstance is not null)
            return descriptor.SingleInstance;
        if (descriptor.Factory is not null)
            return descriptor.SingleInstance = descriptor.Factory!(provider);
        lock (descriptor)
            descriptor.Factory ??= SvcFactory.CreateAotFactory(descriptor);
        return descriptor.SingleInstance = descriptor.Factory(provider);
    }

    #endregion
}
