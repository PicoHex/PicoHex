namespace PicoDI.Abs;

/// <summary>
/// Thrown when a type registered as a hosted service does not implement <see cref="IHostedSvc"/>.
/// </summary>
public sealed class HostedSvcRegistrationException : PicoDiException
{
    public HostedSvcRegistrationException() { }

    public HostedSvcRegistrationException(string message)
        : base(message) { }

    public HostedSvcRegistrationException(string message, Exception innerException)
        : base(message, innerException) { }
}
