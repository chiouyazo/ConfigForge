using ConfigForge.Core.Schema;

namespace ConfigForge.AspNet;

/// <summary>
/// Mutable, process-wide state for the hosted ConfigForge UI. Holds the resolved
/// options, the currently available schemas (keyed by schema id), and the degraded
/// flags applied when a plugin a schema depends on is removed. Registered as a
/// singleton so the directory watcher and the request pipeline share one view.
/// </summary>
public interface IConfigForgeHostState
{
    /// <summary>The resolved options the UI is hosted with.</summary>
    AspNetConfigForgeOptions Options { get; }

    /// <summary>The schemas currently available to the UI, keyed by schema id.</summary>
    IReadOnlyDictionary<string, ConfigSchema> Schemas { get; }

    /// <summary>
    /// The ids of schemas that are currently degraded because a plugin they depend
    /// on is no longer loaded. Degraded schemas remain editable but their
    /// actions and loaders are disabled.
    /// </summary>
    IReadOnlyCollection<string> DegradedSchemaIds { get; }

    /// <summary>Adds or replaces an available schema.</summary>
    /// <param name="schema">The schema to register.</param>
    void UpsertSchema(ConfigSchema schema);

    /// <summary>Removes an available schema by id, if present.</summary>
    /// <param name="schemaId">The id of the schema to remove.</param>
    void RemoveSchema(string schemaId);

    /// <summary>Marks a schema as degraded.</summary>
    /// <param name="schemaId">The id of the schema to degrade.</param>
    void MarkDegraded(string schemaId);

    /// <summary>Clears the degraded flag for a schema.</summary>
    /// <param name="schemaId">The id of the schema to restore.</param>
    void ClearDegraded(string schemaId);

    /// <summary>Returns whether a schema is currently degraded.</summary>
    /// <param name="schemaId">The id of the schema to query.</param>
    /// <returns><see langword="true"/> when the schema is degraded.</returns>
    bool IsDegraded(string schemaId);
}
