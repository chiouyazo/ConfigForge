namespace ConfigForge.Core.Documents;

/// <summary>A single field-level validation failure.</summary>
public sealed class ValidationError
{
    /// <summary>The key of the field that failed validation.</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>A human-readable description of the failure.</summary>
    public string Message { get; init; } = string.Empty;
}
