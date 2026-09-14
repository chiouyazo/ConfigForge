namespace ConfigForge.AspNet.Endpoints;

/// <summary>One action of the manifest. Registration status is deliberately not reported here;
/// see <see cref="ActionResponse"/>.</summary>
/// <param name="ActionId">The action identifier.</param>
/// <param name="Label">The button label.</param>
/// <param name="Category">The category label the action is scoped to, or null for every category.</param>
/// <param name="Section">The section (sub-tab) the action is scoped to, or null.</param>
/// <param name="RequiresEntry">Whether the action needs a selected collection entry.</param>
/// <param name="Variant">The visual variant.</param>
/// <param name="Position">Top or bottom placement.</param>
public sealed record ManifestAction(
    string ActionId,
    string Label,
    string? Category,
    string? Section,
    bool RequiresEntry,
    string Variant,
    string Position
);
