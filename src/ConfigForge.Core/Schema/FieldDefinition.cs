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
    /// Whether this field participates in persistence. When false (set via
    /// <c>x-cf.controls[key].tracked: false</c>), the field is still editable and
    /// readable by actions, but its value never marks the document dirty and is
    /// stripped from the saved output, e.g. a transient "test recipient" field.
    /// Defaults to true.
    /// </summary>
    public bool Tracked { get; init; } = true;

    /// <summary>
    /// For an <c>arrayobject</c> control, the field templates describing one item's
    /// properties. Their <see cref="Key"/> is relative to a single array element;
    /// the array control rebases them onto <c>parentKey/index/childKey</c> per row.
    /// Empty for every other control type.
    /// </summary>
    public IReadOnlyList<FieldDefinition> Children { get; init; } = [];

    /// <summary>
    /// For a <c>map</c> control, the template describing each value (from the
    /// schema's <c>additionalProperties</c>). Its <see cref="Key"/> is empty and is
    /// rebased onto <c>parentKey/entryKey</c> per entry. Null for every other control
    /// type.
    /// </summary>
    public FieldDefinition? ValueField { get; init; }

    /// <summary>
    /// The schema constraint keywords (minimum, maximum, pattern, enum, type, …)
    /// copied as CLR values for validation and example generation.
    /// </summary>
    public IReadOnlyDictionary<string, object> SchemaConstraints { get; init; } =
        new Dictionary<string, object>(StringComparer.Ordinal);

    /// <summary>
    /// Returns a copy of this definition with a different <see cref="Key"/>. Used by
    /// the array and map controls to rebase a child or value template onto the
    /// concrete document path of one row or entry.
    /// </summary>
    /// <param name="key">The path key for the copy.</param>
    /// <returns>A copy carrying the new key.</returns>
    public FieldDefinition WithKey(string key) =>
        new()
        {
            Key = key,
            ControlType = ControlType,
            Title = Title,
            Description = Description,
            Tooltip = Tooltip,
            Placeholder = Placeholder,
            Unit = Unit,
            Required = Required,
            ReadOnly = ReadOnly,
            DefaultValue = DefaultValue,
            LoaderId = LoaderId,
            ValidatorId = ValidatorId,
            Rules = Rules,
            Tracked = Tracked,
            Children = Children,
            ValueField = ValueField,
            SchemaConstraints = SchemaConstraints,
        };
}
