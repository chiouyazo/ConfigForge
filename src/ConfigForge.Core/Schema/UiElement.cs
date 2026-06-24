namespace ConfigForge.Core.Schema;

/// <summary>
/// A parsed JsonForms UI-schema node (Control, layout, Group, Label, Category,
/// or Categorization).
/// </summary>
public sealed class UiElement
{
    /// <summary>
    /// The node type: Control, VerticalLayout, HorizontalLayout, Group, Label,
    /// Category, or Categorization.
    /// </summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>The JSON Pointer scope for a Control, e.g. <c>#/properties/key</c>.</summary>
    public string? Scope { get; init; }

    /// <summary>The display label, when present.</summary>
    public string? Label { get; init; }

    /// <summary>The child elements of a layout, group, or category.</summary>
    public IReadOnlyList<UiElement> Elements { get; init; } = [];

    /// <summary>The rule attached to this element, when present.</summary>
    public JsonFormsRule? Rule { get; init; }
}
