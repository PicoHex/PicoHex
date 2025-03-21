namespace PicoHex.Log;

public enum LogLevel : byte
{
    Trace,
    Debug,
    Information,
    Warning,
    Error,
    Critical,
    None = byte.MaxValue
}
