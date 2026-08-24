namespace ConfigForge.Abstractions.Annotations;

/// <summary>
/// Declares a map property as a <b>sidebar collection</b> (master/detail): its entries are listed
/// as selectable sub-items under the category, with add/remove, and the canvas edits the selected
/// entry instead of the whole map. Put it on the <c>IDictionary</c> property alongside its
/// <c>[CfGroup]</c>/<c>[CfCategory]</c> (which names the sidebar node). This replaces hand-writing
/// the <c>x-cf.categories[…]</c> collection metadata in an overlay.
/// </summary>
/// <example>
/// <code>
/// [CfGroup("Connectors"), CfCollection(Label = "name", AddLabel = "Add connector", Status = "active")]
/// public IDictionary&lt;Guid, ConnectorConfig&gt; Connectors { get; init; } = new Dictionary&lt;Guid, ConnectorConfig&gt;();
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class CfCollectionAttribute : Attribute
{
    /// <summary>
    /// Relative key of the entry field whose value labels the entry in the sidebar (e.g. <c>name</c>).
    /// Falls back to the entry key when unset.
    /// </summary>
    public string? Label { get; init; }

    /// <summary>Text of the sidebar "add" affordance (e.g. <c>Add connector</c>).</summary>
    public string? AddLabel { get; init; }

    /// <summary>
    /// Relative key of a boolean entry field indicating whether the entry is active. When set, the
    /// sidebar shows a status dot and dims inactive entries, so on/off state is visible without
    /// opening each entry.
    /// </summary>
    public string? Status { get; init; }
}
