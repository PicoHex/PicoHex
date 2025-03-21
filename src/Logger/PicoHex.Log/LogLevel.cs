namespace PicoHex.Log;

public enum LogLevel : byte
{
    Trace,
    Debug,
    Info,
    Notice,
    Warning,
    Error,
    Critical,
    Alert,
    Emergency,
    None = byte.MaxValue
}
