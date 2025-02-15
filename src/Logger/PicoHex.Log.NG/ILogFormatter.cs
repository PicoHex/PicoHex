namespace PicoHex.Log.NG;

public interface ILogFormatter
{
    string Format(LogEntry entry);
}
