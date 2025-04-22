namespace PicoHex.DI.Abstractions;

public class ServiceNotRegisteredException(
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.None)] Type type
) : InvalidOperationException($"Service of type {type.Name} not registered.");
