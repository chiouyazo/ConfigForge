namespace ConfigForge.Plugin.Template;

/// <summary>
/// Thrown by <see cref="ExampleServiceClient"/> when a connection attempt
/// fails. Stub support type for the reference plugin.
/// </summary>
public sealed class ServiceConnectionException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="ServiceConnectionException"/> class.</summary>
    public ServiceConnectionException() { }

    /// <summary>Initializes a new instance with a message.</summary>
    /// <param name="message">The error message.</param>
    public ServiceConnectionException(string message)
        : base(message) { }

    /// <summary>Initializes a new instance with a message and inner exception.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The underlying cause.</param>
    public ServiceConnectionException(string message, Exception innerException)
        : base(message, innerException) { }
}
