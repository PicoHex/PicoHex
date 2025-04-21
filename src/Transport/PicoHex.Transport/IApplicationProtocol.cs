namespace PicoHex.Transport;

public interface IApplicationProtocol
{
    ProtocolVersion Version { get; }
    Task ActivateAsync();
    Task DeactivateAsync();
}

public record struct ProtocolVersion(int Major, int Minor);
