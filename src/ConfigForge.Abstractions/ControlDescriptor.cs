namespace ConfigForge.Abstractions;

/// <summary>
/// The resolved description of a single control, handed to a custom
/// <c>IConfigControl</c> so it can render itself without touching raw schema JSON.
/// </summary>
public sealed class ControlDescriptor
{
    /// <summary>The JSON Schema property key this control edits.</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>The resolved control type identifier (built-in or custom).</summary>
    public string ControlType { get; init; } = string.Empty;

    /// <summary>Display title for the field.</summary>
    public string? Title { get; init; }

    /// <summary>Long-form description shown beneath or beside the field.</summary>
    public string? Description { get; init; }

    /// <summary>Hover help text.</summary>
    public string? Tooltip { get; init; }

    /// <summary>Placeholder text for text-like controls.</summary>
    public string? Placeholder { get; init; }

    /// <summary>Unit label rendered alongside number and slider controls.</summary>
    public string? Unit { get; init; }

    /// <summary>True when a value is required.</summary>
    public bool Required { get; init; }

    /// <summary>True when the control is read-only.</summary>
    public bool ReadOnly { get; init; }

    /// <summary>
    /// Additional x-cf control properties not covered by the typed members above,
    /// keyed by property name. Lets custom controls read bespoke options.
    /// </summary>
    public IReadOnlyDictionary<string, object> Options { get; init; } =
        new Dictionary<string, object>(StringComparer.Ordinal);
}
