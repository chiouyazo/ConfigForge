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

    /// <summary>
    /// When set, this category is a <em>collection master/detail</em>: the sidebar lists the
    /// entries of the map field named here as selectable sub-items (with add/remove) and the
    /// canvas shows only the selected entry's form instead of the whole map. Sourced from
    /// <c>x-cf.categories[label].collection</c>. Null for an ordinary category.
    /// </summary>
    public string? CollectionKey { get; init; }

    /// <summary>
    /// For a collection category, the relative key within one entry whose value labels that
    /// entry in the sidebar (e.g. <c>name</c>). Falls back to the entry key when unset or empty.
    /// Sourced from <c>x-cf.categories[label].collectionLabel</c>.
    /// </summary>
    public string? CollectionEntryLabelKey { get; init; }

    /// <summary>
    /// For a collection category, the label of the "add" affordance in the sidebar
    /// (e.g. <c>Add shop</c>). Sourced from <c>x-cf.categories[label].collectionAddLabel</c>.
    /// </summary>
    public string? CollectionAddLabel { get; init; }
}
