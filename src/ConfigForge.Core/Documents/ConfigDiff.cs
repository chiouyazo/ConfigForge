namespace ConfigForge.Core.Documents;

/// <summary>The set of changes between two configuration documents.</summary>
public sealed class ConfigDiff
{
    /// <summary>The individual key-level changes.</summary>
    public IReadOnlyList<ConfigChange> Changes { get; init; } = [];

    /// <summary>True when at least one change is present.</summary>
    public bool HasChanges => Changes.Count > 0;
}
