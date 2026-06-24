namespace ConfigForge.Core.Schema;

/// <summary>
/// Thrown when a combined ConfigForge schema document cannot be parsed: malformed
/// JSON, a missing <c>schema</c> section, or an invalid Draft 7 schema.
/// </summary>
public sealed class SchemaParseException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="SchemaParseException"/> class.</summary>
    public SchemaParseException() { }

    /// <summary>Initializes a new instance with the supplied message.</summary>
    /// <param name="message">The error message.</param>
    public SchemaParseException(string message)
        : base(message) { }

    /// <summary>Initializes a new instance with the supplied message and inner exception.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The underlying cause.</param>
    public SchemaParseException(string message, Exception innerException)
        : base(message, innerException) { }
}
