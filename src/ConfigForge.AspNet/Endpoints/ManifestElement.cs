namespace ConfigForge.AspNet.Endpoints;

/// <summary>
/// One node of a manifest category's layout tree, mirroring one
/// <see cref="ConfigForge.Core.Schema.UiElement"/>: either a field (<c>Control</c>, <see cref="Field"/>
/// set, no children) or a layout node (<c>Group</c>, <c>Categorization</c>, <c>Category</c>,
/// <c>VerticalLayout</c>, <c>HorizontalLayout</c>, or <c>Label</c>) wrapping further elements.
/// </summary>
/// <param name="Type">The node type, matching <see cref="ConfigForge.Core.Schema.UiElement.Type"/>.</param>
/// <param name="Label">The node's display label, when present (a group/tab title, or label text).</param>
/// <param name="Field">The field this node addresses, set only for a <c>Control</c> node.</param>
/// <param name="Elements">The node's child elements, empty for a <c>Control</c> node.</param>
public sealed record ManifestElement(
    string Type,
    string? Label,
    ManifestField? Field,
    IReadOnlyList<ManifestElement> Elements
);
