namespace ConfigForge.Abstractions;

/// <summary>Raised by a control when the user changes its value.</summary>
public sealed class FieldChangedArgs
{
    /// <summary>The JSON Schema property key of the changed field.</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>The new value of the field.</summary>
    public object? Value { get; init; }
}
