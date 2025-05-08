namespace Pico.DI.Abs;

public interface ISvcProvider : ISvcResolver, IDisposable, IAsyncDisposable
{
    ISvcScope CreateScope();
}
