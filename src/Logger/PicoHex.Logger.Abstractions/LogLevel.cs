namespace PicoHex.Logger.Abstractions;

public enum LogLevel : byte
{
    Trace,
    Debug,
    Information,
    Notice,
    Warning,
    Error,
    Critical,
    Alert,
    Emergency,
    None = byte.MaxValue
}
