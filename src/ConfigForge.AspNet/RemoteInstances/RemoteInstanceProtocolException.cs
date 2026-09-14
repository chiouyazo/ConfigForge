namespace ConfigForge.AspNet.RemoteInstances;

/// <summary>A remote instance responded unexpectedly to a poll or action relay call.</summary>
public sealed class RemoteInstanceProtocolException : Exception
{
    /// <summary>Creates the exception with no known instance or message.</summary>
    public RemoteInstanceProtocolException()
        : this("unknown", string.Empty) { }

    /// <summary>Creates the exception with the supplied message and no known instance.</summary>
    /// <param name="message">The exception message.</param>
    public RemoteInstanceProtocolException(string message)
        : this("unknown", message) { }

    /// <summary>Creates the exception with the supplied message, inner exception, and no known instance.</summary>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public RemoteInstanceProtocolException(string message, Exception innerException)
        : this("unknown", message, innerException) { }

    /// <summary>Creates the exception for a known instance.</summary>
    /// <param name="instanceName">The instance that misbehaved.</param>
    /// <param name="message">The exception message.</param>
    public RemoteInstanceProtocolException(string instanceName, string message)
        : base(message)
    {
        InstanceName = instanceName;
    }

    /// <summary>Creates the exception for a known instance with an inner exception.</summary>
    /// <param name="instanceName">The instance that misbehaved.</param>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public RemoteInstanceProtocolException(
        string instanceName,
        string message,
        Exception innerException
    )
        : base(message, innerException)
    {
        InstanceName = instanceName;
    }

    /// <summary>The instance that misbehaved, or <c>unknown</c>.</summary>
    public string InstanceName { get; }

    /// <summary>The HTTP status code the instance responded with, when known.</summary>
    public int? HttpStatusCode { get; init; }
}
