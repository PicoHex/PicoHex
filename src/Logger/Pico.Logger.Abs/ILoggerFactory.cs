namespace Pico.Log.Abs;

public interface ILoggerFactory
{
    ILogger CreateLogger(string categoryName);
}
