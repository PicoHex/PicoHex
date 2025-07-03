namespace Pico.IoC.Abs;

public class ServiceNotRegisteredException(
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type type
) : InvalidOperationException($"Service of type {type.Name} not registered.");
