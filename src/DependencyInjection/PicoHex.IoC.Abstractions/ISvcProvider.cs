namespace PicoHex.IoC.Abstractions;

public interface ISvcProvider : ISvcResolver
{
    ISvcScope CreateScope();
}
