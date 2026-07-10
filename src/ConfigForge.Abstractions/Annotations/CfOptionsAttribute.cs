namespace ConfigForge.Abstractions.Annotations;

/// <summary>
/// Sets several ConfigForge hints for a property in one place, instead of stacking the
/// individual <c>[Cf*]</c> attributes. Every setting maps to the same result as its
/// dedicated attribute (e.g. <see cref="Group"/> == <c>[CfGroup]</c>).
/// <para>
/// The individual attributes still work and take precedence: a dedicated <c>[CfLabel]</c>
/// overrides <see cref="Label"/> here, so you can set the common hints in bulk and override
/// a single facet with a specific attribute.
/// </para>
/// </summary>
/// <example>
/// <code>
/// [CfOptions(Group = "Settings", Category = "General", Order = 0, Label = "Instance Name")]
/// public string? InstanceName { get; init; }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class CfOptionsAttribute : Attribute
{
    /// <summary>Sidebar group (see <c>[CfGroup]</c>). Null leaves it ungrouped.</summary>
    public string? Group { get; init; }

    /// <summary>Tab within the group (see <c>[CfCategory]</c>). Null leaves it uncategorised.</summary>
    public string? Category { get; init; }

    /// <summary>Titled section within the tab (see <c>[CfSection]</c>). Null renders it bare.</summary>
    public string? Section { get; init; }

    /// <summary>
    /// Sort order within its container (see <c>[CfOrder]</c>). Lower sorts first; the default
    /// <see cref="int.MaxValue"/> means "unset" (sorts after ordered fields, then by declaration).
    /// </summary>
    public int Order { get; init; } = int.MaxValue;

    /// <summary>Label / schema <c>title</c> (see <c>[CfLabel]</c>).</summary>
    public string? Label { get; init; }

    /// <summary>Schema <c>description</c> (see <c>[CfDescription]</c>).</summary>
    public string? Description { get; init; }

    /// <summary>Tooltip (see <c>[CfTooltip]</c>).</summary>
    public string? Tooltip { get; init; }

    /// <summary>Placeholder text (see <c>[CfPlaceholder]</c>).</summary>
    public string? Placeholder { get; init; }

    /// <summary>Unit suffix (see <c>[CfUnit]</c>).</summary>
    public string? Unit { get; init; }

    /// <summary>Explicit control type (see <c>[CfControl]</c>). Null infers from the CLR type.</summary>
    public string? Control { get; init; }

    /// <summary>Loader id that fills the control's options (see <c>[CfLoader]</c>).</summary>
    public string? Loader { get; init; }

    /// <summary>Renders as a write-only secret control (see <c>[CfSecret]</c>).</summary>
    public bool Secret { get; init; }

    /// <summary>
    /// Whether the field participates in persistence/dirty-tracking. False mirrors
    /// <c>[CfUntracked]</c> (editable and readable, but never saved). Defaults to true.
    /// </summary>
    public bool Tracked { get; init; } = true;

    /// <summary>Renders the field read-only (schema <c>readOnly</c>): shown but not editable.</summary>
    public bool ReadOnly { get; init; }

    /// <summary>Excludes the property from the schema entirely (see <c>[CfIgnore]</c>).</summary>
    public bool Ignore { get; init; }
}
