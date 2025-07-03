namespace Pico.Log.Abs;

public interface ILogFormatter
{
    string Format(LogEntry entry);
}
