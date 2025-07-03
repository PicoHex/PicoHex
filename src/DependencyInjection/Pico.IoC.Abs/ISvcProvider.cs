namespace Pico.IoC.Abs;

public interface ISvcProvider : ISvcResolver, IDisposable, IAsyncDisposable
{
    ISvcScope CreateScope();
}
