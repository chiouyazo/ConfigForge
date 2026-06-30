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
    public IReadOnlySet<string> IgnoredKeys { get; set; } =
        new HashSet<string>(StringComparer.Ordinal);

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

    private Dictionary<string, string> Capture(ConfigDocument document)
    {
        ConfigDocument effective = document;
        if (IgnoredKeys.Count > 0)
        {
            effective = document.Clone();
            foreach (string ignored in IgnoredKeys)
            {
                effective.Remove(ignored);
                PruneEmptyAncestors(effective, ignored);
            }
        }

        Dictionary<string, string> map = new(StringComparer.Ordinal);
        foreach (string key in effective.Keys)
        {
            map[key] = JsonValueHelper.Serialize(effective[key]);
        }

        return map;
    }

    private static void PruneEmptyAncestors(ConfigDocument document, string path)
    {
        string[] segments = path.Split('/');
        for (int i = segments.Length - 1; i >= 1; i--)
        {
            string ancestor = string.Join('/', segments, 0, i);
            if (document.TryGetValue(ancestor, out object? value) && IsEmptyContainer(value))
            {
                document.Remove(ancestor);
            }
            else
            {
                break;
            }
        }
    }

    private static bool IsEmptyContainer(object? value) =>
        value switch
        {
            IDictionary<string, object?> dict => dict.Count == 0,
            IList<object?> list => list.Count == 0,
            _ => false,
        };

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
