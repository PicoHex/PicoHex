namespace Pico.Log.Abstractions;

public interface ILogFormatter
{
    string Format(LogEntry entry);
}
