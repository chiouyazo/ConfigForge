namespace ConfigForge.Abstractions.Annotations;

/// <summary>
/// Declares an icon and/or description for a category (sidebar group or tab), keyed by its
/// label. Placed on the config <b>type</b> (not a property, since a category is shared by
/// many properties) and repeatable — one per category you want to decorate.
/// </summary>
/// <example>
/// <code>
/// [CfCategoryMeta("Database", Icon = "database", Description = "Connection settings.")]
/// [CfCategoryMeta("Alerts", Icon = "bell")]
/// public sealed record AppConfig { … }
/// </code>
/// </example>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct,
    AllowMultiple = true,
    Inherited = false
)]
public sealed class CfCategoryMetaAttribute : Attribute
{
    /// <summary>Creates category metadata for the category with the given label.</summary>
    /// <param name="category">The category label (matches a <c>[CfGroup]</c>/<c>[CfCategory]</c>).</param>
    public CfCategoryMetaAttribute(string category) => Category = category;

    /// <summary>The category label this metadata applies to.</summary>
    public string Category { get; }

    /// <summary>Optional icon identifier shown next to the category.</summary>
    public string? Icon { get; init; }

    /// <summary>Optional description shown at the top of the category.</summary>
    public string? Description { get; init; }
}
