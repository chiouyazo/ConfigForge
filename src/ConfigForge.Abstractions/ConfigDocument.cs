namespace ConfigForge.Abstractions;

/// <summary>
/// A mutable set of configuration values keyed by JSON Schema property key.
/// Produced by parsing or generation and edited live by the UI. Values not
/// declared in the schema are preserved so they round-trip on save.
/// </summary>
/// <remarks>
/// Values keep their natural nested shape: nested objects are stored as
/// <see cref="IDictionary{TKey, TValue}"/> and arrays as <see cref="IList{T}"/>.
/// Accessors take a path with <c>/</c> separating segments
/// (<c>Parent/Child/Leaf</c>); a numeric segment indexes into an array. A plain
/// key with no separator addresses a top-level value, so existing callers behave
/// unchanged. Object property names and map keys therefore must not contain a
/// literal <c>/</c>.
/// </remarks>
public sealed class ConfigDocument
{
    private readonly Dictionary<string, object?> _values;

    /// <summary>Creates an empty document.</summary>
    public ConfigDocument()
        : this(new Dictionary<string, object?>(StringComparer.Ordinal)) { }

    /// <summary>Creates a document seeded with the supplied values.</summary>
    /// <param name="values">The initial field values, copied into the document.</param>
    public ConfigDocument(IDictionary<string, object?> values)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(values);
#else
        if (values is null)
        {
            throw new ArgumentNullException(nameof(values));
        }
#endif

        _values = new Dictionary<string, object?>(values, StringComparer.Ordinal);
    }

    /// <summary>The top-level field keys currently present in the document.</summary>
    public IReadOnlyCollection<string> Keys => _values.Keys;

    /// <summary>The number of top-level fields present in the document.</summary>
    public int Count => _values.Count;

    /// <summary>Gets or sets the raw value at a path.</summary>
    /// <param name="path">The field path, with <c>/</c> separating nested segments.</param>
    public object? this[string path]
    {
        get
        {
            EnsurePath(path);
            return Resolve(Segments(path), out object? value) ? value : null;
        }
        set
        {
            EnsurePath(path);
            Assign(Segments(path), value);
        }
    }

    /// <summary>Returns true when the path resolves to a present value, even if null.</summary>
    /// <param name="path">The field path.</param>
    /// <returns>True when the path exists.</returns>
    public bool ContainsKey(string path)
    {
        EnsurePath(path);
        return Resolve(Segments(path), out _);
    }

    /// <summary>Attempts to read the value at a path.</summary>
    /// <param name="path">The field path.</param>
    /// <param name="value">The value if present, otherwise null.</param>
    /// <returns>True when the path exists.</returns>
    public bool TryGetValue(string path, out object? value)
    {
        EnsurePath(path);
        return Resolve(Segments(path), out value);
    }

    /// <summary>Removes the value at a path.</summary>
    /// <param name="path">The field path.</param>
    /// <returns>True when a value was removed.</returns>
    public bool Remove(string path)
    {
        EnsurePath(path);

        string[] segments = Segments(path);
        if (segments.Length == 1)
        {
            return _values.Remove(segments[0]);
        }

        if (!Resolve(segments, segments.Length - 1, out object? parent))
        {
            return false;
        }

        string leaf = segments[^1];
        return parent switch
        {
            IDictionary<string, object?> dict => dict.Remove(leaf),
            IList<object?> list
                when int.TryParse(leaf, out int index) && index >= 0 && index < list.Count =>
                RemoveAt(list, index),
            _ => false,
        };
    }

    /// <summary>
    /// Returns the value at a path as its string representation, or an empty string
    /// when absent or null. Used by <see cref="IActionContext"/>.
    /// </summary>
    /// <param name="path">The field path.</param>
    /// <returns>The string representation of the value, or an empty string.</returns>
    public string GetString(string path)
    {
        EnsurePath(path);
        return Resolve(Segments(path), out object? value) && value is not null
            ? value.ToString() ?? string.Empty
            : string.Empty;
    }

    /// <summary>Creates an independent deep copy of this document.</summary>
    /// <returns>A new document with copies of the same field values.</returns>
    public ConfigDocument Clone()
    {
        Dictionary<string, object?> copy = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, object?> entry in _values)
        {
            copy[entry.Key] = DeepCopy(entry.Value);
        }

        return new ConfigDocument(copy);
    }

    private static void EnsurePath(string path)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(path);
#else
        if (path is null)
        {
            throw new ArgumentNullException(nameof(path));
        }
#endif
    }

    private static string[] Segments(string path) => path.Split('/');

    private bool Resolve(string[] segments, out object? value) =>
        Resolve(segments, segments.Length, out value);

    private bool Resolve(string[] segments, int count, out object? value)
    {
        if (count == 0 || !_values.TryGetValue(segments[0], out object? current))
        {
            value = null;
            return false;
        }

        for (int i = 1; i < count; i++)
        {
            if (!Step(current, segments[i], out current))
            {
                value = null;
                return false;
            }
        }

        value = current;
        return true;
    }

    private static bool Step(object? node, string segment, out object? next)
    {
        switch (node)
        {
            case IDictionary<string, object?> dict:
                return dict.TryGetValue(segment, out next);
            case IList<object?> list
                when int.TryParse(segment, out int index) && index >= 0 && index < list.Count:
                next = list[index];
                return true;
            default:
                next = null;
                return false;
        }
    }

    private void Assign(string[] segments, object? value)
    {
        if (segments.Length == 1)
        {
            _values[segments[0]] = value;
            return;
        }

        if (!_values.TryGetValue(segments[0], out object? node) || node is null)
        {
            Dictionary<string, object?> container = new(StringComparer.Ordinal);
            _values[segments[0]] = container;
            node = container;
        }

        for (int i = 1; i < segments.Length - 1; i++)
        {
            node = NextContainer(node, segments[i]);
        }

        SetLeaf(node, segments[^1], value);
    }

    private static object NextContainer(object? node, string segment)
    {
        if (Step(node, segment, out object? existing) && existing is not null)
        {
            return existing;
        }

        Dictionary<string, object?> container = new(StringComparer.Ordinal);
        SetLeaf(node, segment, container);
        return container;
    }

    private static void SetLeaf(object? node, string segment, object? value)
    {
        switch (node)
        {
            case IDictionary<string, object?> dict:
                dict[segment] = value;
                break;
            case IList<object?> list when int.TryParse(segment, out int index) && index >= 0:
                while (list.Count <= index)
                {
                    list.Add(null);
                }

                list[index] = value;
                break;
            default:
                break;
        }
    }

    private static bool RemoveAt(IList<object?> list, int index)
    {
        list.RemoveAt(index);
        return true;
    }

    private static object? DeepCopy(object? value)
    {
        switch (value)
        {
            case IDictionary<string, object?> dict:
                Dictionary<string, object?> mapCopy = new(StringComparer.Ordinal);
                foreach (KeyValuePair<string, object?> entry in dict)
                {
                    mapCopy[entry.Key] = DeepCopy(entry.Value);
                }

                return mapCopy;
            case IList<object?> list:
                List<object?> listCopy = new(list.Count);
                foreach (object? item in list)
                {
                    listCopy.Add(DeepCopy(item));
                }

                return listCopy;
            default:
                return value;
        }
    }
}
