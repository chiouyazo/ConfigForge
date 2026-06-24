namespace ConfigForge.Core.Documents;

/// <summary>The kind of change applied to a single key between two documents.</summary>
public enum ConfigChangeKind
{
    /// <summary>The key exists in the modified document but not the original.</summary>
    Added,

    /// <summary>The key exists in the original document but not the modified.</summary>
    Removed,

    /// <summary>The key exists in both documents with a different value.</summary>
    Modified,
}
