namespace ConfigForge.AspNet.RemoteInstances;

/// <summary>A remote instance rejected the credentials it was polled or relayed to with.</summary>
public sealed class RemoteInstanceUnauthorizedException : Exception
{
    /// <summary>Creates the exception with a default message.</summary>
    public RemoteInstanceUnauthorizedException()
        : base("The instance rejected the configured credentials.") { }

    /// <summary>Creates the exception with the supplied message.</summary>
    /// <param name="message">The exception message.</param>
    public RemoteInstanceUnauthorizedException(string message)
        : base(message) { }

    /// <summary>Creates the exception with the supplied message and inner exception.</summary>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public RemoteInstanceUnauthorizedException(string message, Exception innerException)
        : base(message, innerException) { }
}
