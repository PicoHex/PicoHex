namespace PicoHex.Logger.NG.Abstractions;

public interface ILogFormatter
{
    string Format(LogEntry entry);
}
