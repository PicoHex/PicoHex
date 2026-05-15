namespace PicoDI.Abs;

/// <summary>
/// Base exception for all PicoDI errors. Not sealed by design —
/// serves as the base class for specialized exception types such as
/// <see cref="HostedSvcRegistrationException"/>.
/// </summary>
public class PicoDiException : Exception
{
    public PicoDiException() { }

    public PicoDiException(string message)
        : base(message) { }

    public PicoDiException(string message, Exception innerException)
        : base(message, innerException) { }
}
