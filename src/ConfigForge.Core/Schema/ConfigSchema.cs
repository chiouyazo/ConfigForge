namespace ConfigForge.Core.Schema;

/// <summary>
/// A fully resolved configuration schema: identity metadata, the category tree
/// for layout, and the resolved field definitions keyed by property key.
/// </summary>
public sealed class ConfigSchema
{
    /// <summary>The schema identifier from <c>x-cf.id</c>.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>The display name from <c>x-cf.name</c>.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The schema version from <c>x-cf.version</c>.</summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>The owning plugin identifier from <c>x-cf.pluginId</c>, if any.</summary>
    public string? PluginId { get; init; }

    /// <summary>The top-level categories describing the form layout.</summary>
    public IReadOnlyList<CategoryElement> Categories { get; init; } = [];

    /// <summary>The resolved field definitions keyed by property key.</summary>
    public IReadOnlyDictionary<string, FieldDefinition> Fields { get; init; } =
        new Dictionary<string, FieldDefinition>(StringComparer.Ordinal);

    /// <summary>The action buttons declared in <c>x-cf.actions</c>.</summary>
    public IReadOnlyList<ActionDefinition> Actions { get; init; } = [];

    /// <summary>
    /// The path keys of fields marked <c>tracked: false</c>: editable and readable
    /// by actions, but excluded from dirty tracking and from the saved output.
    /// </summary>
    public IReadOnlySet<string> UntrackedKeys { get; init; } =
        new HashSet<string>(StringComparer.Ordinal);
}
