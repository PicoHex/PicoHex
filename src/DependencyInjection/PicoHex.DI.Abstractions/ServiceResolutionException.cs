namespace PicoHex.DI.Abstractions;

public class ServiceResolutionException(string? message) : InvalidOperationException(message);
