namespace ConfigForge.Core.Schema;

/// <summary>
/// A top-level category (tab/page) of the resolved schema, with its decorated
/// metadata and the UI elements it contains.
/// </summary>
public sealed class CategoryElement
{
    /// <summary>The category label shown in the tab strip.</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>An optional icon identifier sourced from <c>x-cf.categories</c>.</summary>
    public string? Icon { get; init; }

    /// <summary>An optional description sourced from <c>x-cf.categories</c>.</summary>
    public string? Description { get; init; }

    /// <summary>The UI elements contained in this category.</summary>
    public IReadOnlyList<UiElement> Elements { get; init; } = [];
}
