namespace ConfigForge.Core.Schema;

/// <summary>
/// A fully resolved field: the merge of a JSON Schema property with its
/// <c>x-cf.controls</c> entry and any attached UI rules. No raw JSON leaks here.
/// </summary>
public sealed class FieldDefinition
{
    /// <summary>The JSON Schema property key.</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>The resolved control type (e.g. <c>text</c>, <c>select</c>, <c>checkbox</c>).</summary>
    public string ControlType { get; init; } = string.Empty;

    /// <summary>The field title shown as its label.</summary>
    public string? Title { get; init; }

    /// <summary>The field description.</summary>
    public string? Description { get; init; }

    /// <summary>An optional tooltip sourced from <c>x-cf.controls</c>.</summary>
    public string? Tooltip { get; init; }

    /// <summary>An optional placeholder sourced from <c>x-cf.controls</c>.</summary>
    public string? Placeholder { get; init; }

    /// <summary>An optional unit suffix sourced from <c>x-cf.controls</c>.</summary>
    public string? Unit { get; init; }

    /// <summary>True when the field is listed in the schema's <c>required</c> array.</summary>
    public bool Required { get; init; }

    /// <summary>True when the schema marks the property <c>readOnly</c>.</summary>
    public bool ReadOnly { get; init; }

    /// <summary>The declared schema <c>default</c>, if any, as a CLR value.</summary>
    public object? DefaultValue { get; init; }

    /// <summary>The <c>x-cf</c> loader identifier that populates this field's options.</summary>
    public string? LoaderId { get; init; }

    /// <summary>The <c>x-cf</c> validator identifier applied to this field.</summary>
    public string? ValidatorId { get; init; }

    /// <summary>The UI rules attached to this field's control.</summary>
    public IReadOnlyList<JsonFormsRule> Rules { get; init; } = [];

    /// <summary>
    /// The schema constraint keywords (minimum, maximum, pattern, enum, type, …)
    /// copied as CLR values for validation and example generation.
    /// </summary>
    public IReadOnlyDictionary<string, object> SchemaConstraints { get; init; } =
        new Dictionary<string, object>(StringComparer.Ordinal);
}
