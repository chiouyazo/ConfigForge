using ConfigForge.Abstractions;
using ConfigForge.Core.Internal;

namespace ConfigForge.Core.Documents;

/// <summary>
/// Tracks unsaved edits by comparing a live document against a snapshot baseline
/// of canonical per-key JSON values.
/// </summary>
public sealed class DirtyStateTracker : IDirtyStateTracker
{
    private Dictionary<string, string> _baseline = new(StringComparer.Ordinal);
    private HashSet<string> _dirtyKeys = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public event EventHandler? DirtyStateChanged;

    /// <inheritdoc />
    public bool IsDirty => _dirtyKeys.Count > 0;

    /// <inheritdoc />
    public IReadOnlySet<string> DirtyKeys => _dirtyKeys;

    /// <inheritdoc />
    public void Snapshot(ConfigDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        _baseline = Capture(document);
        SetDirtyKeys([]);
    }

    /// <inheritdoc />
    public void Update(ConfigDocument current)
    {
        ArgumentNullException.ThrowIfNull(current);

        Dictionary<string, string> snapshot = Capture(current);
        HashSet<string> dirty = new(StringComparer.Ordinal);

        foreach (KeyValuePair<string, string> entry in snapshot)
        {
            if (
                !_baseline.TryGetValue(entry.Key, out string? baselineValue)
                || !string.Equals(baselineValue, entry.Value, StringComparison.Ordinal)
            )
            {
                dirty.Add(entry.Key);
            }
        }

        foreach (string baselineKey in _baseline.Keys)
        {
            if (!snapshot.ContainsKey(baselineKey))
            {
                dirty.Add(baselineKey);
            }
        }

        SetDirtyKeys(dirty);
    }

    /// <inheritdoc />
    public void Reset()
    {
        _baseline = new Dictionary<string, string>(StringComparer.Ordinal);
        SetDirtyKeys([]);
    }

    private static Dictionary<string, string> Capture(ConfigDocument document)
    {
        Dictionary<string, string> map = new(StringComparer.Ordinal);
        foreach (string key in document.Keys)
        {
            map[key] = JsonValueHelper.Serialize(document[key]);
        }

        return map;
    }

    private void SetDirtyKeys(HashSet<string> dirty)
    {
        if (_dirtyKeys.SetEquals(dirty))
        {
            return;
        }

        _dirtyKeys = dirty;
        DirtyStateChanged?.Invoke(this, EventArgs.Empty);
    }
}
