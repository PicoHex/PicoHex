namespace PicoHex.Transport;

// 自定义传输层异常基类
public class TransportException : Exception
{
    public TransportException(string message)
        : base(message) { }

    public TransportException(string message, Exception innerException)
        : base(message, innerException) { }
}
