namespace ConfigForge.AspNet.Endpoints;

/// <summary>One category of the manifest.</summary>
/// <param name="Label">The category label.</param>
/// <param name="Description">The category description, if any.</param>
/// <param name="CollectionKey">
/// The map field key backing a collection category, or null for an ordinary one.
/// </param>
/// <param name="CollectionEntryLabelKey">
/// The relative key labelling one entry, for a collection category.
/// </param>
/// <param name="Elements">
/// For a collection category, the entry's fields as a flat list of <c>Control</c> elements (an
/// entry has no further layout to mirror). For an ordinary category, the real layout tree the
/// interactive UI renders from: groups, tab groups, and layouts nesting fields and further
/// groups exactly as <see cref="ConfigForge.Core.Schema.CategoryElement.Elements"/> does.
/// </param>
/// <param name="CollectionIsLoaderBacked">
/// For a collection category, whether it is backed by a registered collection loader (live,
/// read-only rows computed per request) rather than a saved document map. Null for an ordinary
/// category. A remote consumer uses this to decide whether a mirrored collection can support
/// real add/edit/remove (document-backed) or must stay read-only-with-refresh (loader-backed).
/// </param>
public sealed record ManifestCategory(
    string Label,
    string? Description,
    string? CollectionKey,
    string? CollectionEntryLabelKey,
    IReadOnlyList<ManifestElement> Elements,
    bool? CollectionIsLoaderBacked = null
);
