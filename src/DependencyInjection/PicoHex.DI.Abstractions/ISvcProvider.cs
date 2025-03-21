namespace PicoHex.DI.Abstractions;

public interface ISvcProvider : ISvcResolver
{
    ISvcScope CreateScope();
}
