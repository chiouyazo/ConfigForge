namespace ConfigForge.Abstractions;

/// <summary>
/// A mutable set of configuration values keyed by JSON Schema property key.
/// Produced by parsing or generation and edited live by the UI. Values not
/// declared in the schema are preserved so they round-trip on save.
/// </summary>
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

    /// <summary>All field keys currently present in the document.</summary>
    public IReadOnlyCollection<string> Keys => _values.Keys;

    /// <summary>The number of fields present in the document.</summary>
    public int Count => _values.Count;

    /// <summary>Gets or sets the raw value of a field.</summary>
    /// <param name="key">The JSON Schema property key.</param>
    public object? this[string key]
    {
        get => _values.TryGetValue(key, out object? value) ? value : null;
        set => _values[key] = value;
    }

    /// <summary>Returns true when the field is present, even if its value is null.</summary>
    /// <param name="key">The JSON Schema property key.</param>
    /// <returns>True when the key exists.</returns>
    public bool ContainsKey(string key) => _values.ContainsKey(key);

    /// <summary>Attempts to read a field value.</summary>
    /// <param name="key">The JSON Schema property key.</param>
    /// <param name="value">The value if present, otherwise null.</param>
    /// <returns>True when the key exists.</returns>
    public bool TryGetValue(string key, out object? value) => _values.TryGetValue(key, out value);

    /// <summary>Removes a field from the document.</summary>
    /// <param name="key">The JSON Schema property key.</param>
    /// <returns>True when a field was removed.</returns>
    public bool Remove(string key) => _values.Remove(key);

    /// <summary>
    /// Returns the field value as its string representation, or an empty string
    /// when the field is absent or null. Used by <see cref="IActionContext"/>.
    /// </summary>
    /// <param name="key">The JSON Schema property key.</param>
    /// <returns>The string representation of the value, or an empty string.</returns>
    public string GetString(string key) =>
        _values.TryGetValue(key, out object? value) && value is not null
            ? value.ToString() ?? string.Empty
            : string.Empty;

    /// <summary>Creates an independent copy of this document.</summary>
    /// <returns>A new document with the same field values.</returns>
    public ConfigDocument Clone() => new(_values);
}
