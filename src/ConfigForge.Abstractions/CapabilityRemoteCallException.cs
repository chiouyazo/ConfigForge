namespace ConfigForge.Abstractions;

/// <summary>
/// Thrown by a capability proxy when the remote call could not be completed: a transport
/// failure, a non-success response, or the remote side reporting the action as failed.
/// </summary>
public sealed class CapabilityRemoteCallException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="CapabilityRemoteCallException"/> class.</summary>
    public CapabilityRemoteCallException() { }

    /// <summary>Initializes a new instance with the supplied message.</summary>
    /// <param name="message">The error message.</param>
    public CapabilityRemoteCallException(string message)
        : base(message) { }

    /// <summary>Initializes a new instance with the supplied message and inner exception.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The underlying cause.</param>
    public CapabilityRemoteCallException(string message, Exception innerException)
        : base(message, innerException) { }
}
