using ConfigForge.Abstractions;

namespace ConfigForge.Core.Documents;

/// <summary>
/// Tracks unsaved edits to a document relative to a snapshot baseline.
/// </summary>
public interface IDirtyStateTracker
{
    /// <summary>Captures the supplied document as the clean baseline.</summary>
    /// <param name="document">The document to snapshot.</param>
    void Snapshot(ConfigDocument document);

    /// <summary>
    /// Documented addition: pushes the live document so the tracker can recompute
    /// dirty state against the baseline.
    /// </summary>
    /// <param name="current">The current (possibly edited) document.</param>
    void Update(ConfigDocument current);

    /// <summary>True when the current document differs from the baseline.</summary>
    bool IsDirty { get; }

    /// <summary>The keys whose values differ from the baseline.</summary>
    IReadOnlySet<string> DirtyKeys { get; }

    /// <summary>Clears the baseline and dirty state.</summary>
    void Reset();

    /// <summary>Raised whenever the set of dirty keys changes.</summary>
    event EventHandler? DirtyStateChanged;
}
