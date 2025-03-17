namespace PicoHex.Core.Logging;

public interface ILogFormatter
{
    string Format(LogEntry entry);
}
