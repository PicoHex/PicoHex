namespace PicoDI.Abs;

/// <summary>
/// Exception thrown by PicoDI container operations.
/// </summary>
public class PicoDiException : Exception
{
    public PicoDiException() { }

    public PicoDiException(string message)
        : base(message) { }

    public PicoDiException(string message, Exception innerException)
        : base(message, innerException) { }
}
