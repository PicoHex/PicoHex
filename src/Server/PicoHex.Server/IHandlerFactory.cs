namespace PicoHex.Server;

public interface IHandlerFactory
{
    IStreamHandler GetHandler(string path);
}
