namespace ConfigForge.Core.Documents;

/// <summary>A single key-level difference between two configuration documents.</summary>
public sealed class ConfigChange
{
    /// <summary>The key that changed.</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>The kind of change.</summary>
    public ConfigChangeKind Kind { get; init; }

    /// <summary>The value in the original document, if any.</summary>
    public object? OldValue { get; init; }

    /// <summary>The value in the modified document, if any.</summary>
    public object? NewValue { get; init; }
}
