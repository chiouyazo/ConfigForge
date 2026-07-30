namespace ConfigForge.Abstractions.Annotations;

/// <summary>
/// Applies ConfigForge hints to a property of a type you do not own and therefore cannot
/// decorate inline (a type from a referenced library). Declared at assembly level and
/// matched against a property by its declaring/reflected type and name during schema
/// generation, it carries the same settings as <see cref="CfOptionsAttribute"/>.
/// <para>
/// Precedence is <c>inline attribute</c> ?? <c>inline [CfOptions]</c> ?? <c>[CfMember]</c> ??
/// framework default: an inline attribute on a property you do own always wins, so a
/// <c>[CfMember]</c> only fills in metadata that could not be declared inline.
/// </para>
/// <para>
/// Schema generation scans the assembly of the root type passed to the generator plus any
/// extra assemblies listed in <c>SchemaGenerationOptions.MetadataAssemblies</c>.
/// </para>
/// </summary>
/// <example>
/// <code>
/// [assembly: CfMember(typeof(LogConfiguration), nameof(LogConfiguration.Level),
///     Label = "Log Level", Group = "Logging", Order = 1)]
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class CfMemberAttribute : Attribute
{
    /// <summary>Creates the attribute targeting a property of an external type.</summary>
    /// <param name="targetType">The type that declares (or inherits) the property.</param>
    /// <param name="propertyName">The name of the property the hints apply to.</param>
    public CfMemberAttribute(Type targetType, string propertyName)
    {
        TargetType = targetType;
        PropertyName = propertyName;
    }

    /// <summary>The type whose property is being annotated.</summary>
    public Type TargetType { get; }

    /// <summary>The name of the annotated property on <see cref="TargetType"/>.</summary>
    public string PropertyName { get; }

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
