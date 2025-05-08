namespace Pico.DI.Abstractions;

public class ServiceNotRegisteredException(
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type type
) : InvalidOperationException($"Service of type {type.Name} not registered.");
