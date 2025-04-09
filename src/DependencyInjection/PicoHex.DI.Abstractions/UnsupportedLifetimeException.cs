namespace PicoHex.DI.Abstractions;

public class UnsupportedLifetimeException(SvcLifetime lifetime)
    : ArgumentOutOfRangeException($"Unsupported service lifetime: {lifetime}");
