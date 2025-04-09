namespace PicoHex.DI.Abstractions;

public class ServiceNotRegisteredException(Type type)
    : InvalidOperationException($"Service of type {type.Name} not registered.");
