using System.Collections.Concurrent;
using ConfigForge.Core.Schema;

namespace ConfigForge.AspNet;

/// <summary>
/// Thread-safe default implementation of <see cref="IConfigForgeHostState"/>. The
/// directory watcher mutates it from a background thread while the request pipeline
/// reads it, so the backing collections are concurrent.
/// </summary>
public sealed class ConfigForgeHostState : IConfigForgeHostState
{
    private readonly ConcurrentDictionary<string, ConfigSchema> _localSchemas = new(
        StringComparer.Ordinal
    );

    private readonly ConcurrentDictionary<string, ConfigSchema> _schemas = new(
        StringComparer.Ordinal
    );

    private readonly ConcurrentDictionary<string, byte> _degraded = new(StringComparer.Ordinal);

    private readonly object _remoteContentGate = new();
    private IReadOnlyList<CategoryElement> _remoteCategories = [];
    private IReadOnlyDictionary<string, FieldDefinition> _remoteFields = new Dictionary<
        string,
        FieldDefinition
    >(StringComparer.Ordinal);
    private IReadOnlyList<ActionDefinition> _remoteActions = [];

    /// <summary>Creates the host state for the supplied resolved options.</summary>
    /// <param name="options">The resolved options the UI is hosted with.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public ConfigForgeHostState(AspNetConfigForgeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Options = options;
    }

    /// <inheritdoc />
    public AspNetConfigForgeOptions Options { get; }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, ConfigSchema> Schemas => _schemas;

    /// <inheritdoc />
    public IReadOnlyCollection<string> DegradedSchemaIds =>
        (IReadOnlyCollection<string>)_degraded.Keys;

    /// <inheritdoc />
    public void UpsertSchema(ConfigSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        _localSchemas[schema.Id] = schema;
        _schemas[schema.Id] = WithRemoteContent(schema);
    }

    /// <inheritdoc />
    public void RemoveSchema(string schemaId)
    {
        ArgumentException.ThrowIfNullOrEmpty(schemaId);
        _localSchemas.TryRemove(schemaId, out _);
        _schemas.TryRemove(schemaId, out _);
        _degraded.TryRemove(schemaId, out _);
    }

    /// <inheritdoc />
    public void SetRemoteContent(
        IReadOnlyList<CategoryElement> categories,
        IReadOnlyDictionary<string, FieldDefinition> fields,
        IReadOnlyList<ActionDefinition> actions
    )
    {
        ArgumentNullException.ThrowIfNull(categories);
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(actions);

        lock (_remoteContentGate)
        {
            _remoteCategories = categories;
            _remoteFields = fields;
            _remoteActions = actions;
        }

        foreach (KeyValuePair<string, ConfigSchema> entry in _localSchemas)
        {
            _schemas[entry.Key] = WithRemoteContent(entry.Value);
        }
    }

    private ConfigSchema WithRemoteContent(ConfigSchema local)
    {
        IReadOnlyList<CategoryElement> remoteCategories;
        IReadOnlyDictionary<string, FieldDefinition> remoteFields;
        IReadOnlyList<ActionDefinition> remoteActions;
        lock (_remoteContentGate)
        {
            remoteCategories = _remoteCategories;
            remoteFields = _remoteFields;
            remoteActions = _remoteActions;
        }

        if (remoteCategories.Count == 0 && remoteActions.Count == 0 && remoteFields.Count == 0)
        {
            return local;
        }

        Dictionary<string, FieldDefinition> fields = new(local.Fields, StringComparer.Ordinal);
        foreach (KeyValuePair<string, FieldDefinition> field in remoteFields)
        {
            fields[field.Key] = field.Value;
        }

        return new ConfigSchema
        {
            Id = local.Id,
            Name = local.Name,
            Version = local.Version,
            PluginId = local.PluginId,
            // Remote categories come first, the host's own local categories last: a host that only
            // has one local category to manage local state (e.g. an instance list) alongside a
            // RemoteInstances-driven dashboard wants that category to stay last regardless of how
            // many remote instances come and go, not buried before them.
            Categories = [.. remoteCategories, .. local.Categories],
            Fields = fields,
            Actions = [.. remoteActions, .. local.Actions],
            UntrackedKeys = local.UntrackedKeys,
        };
    }

    /// <inheritdoc />
    public void MarkDegraded(string schemaId)
    {
        ArgumentException.ThrowIfNullOrEmpty(schemaId);
        _degraded[schemaId] = 0;
    }

    /// <inheritdoc />
    public void ClearDegraded(string schemaId)
    {
        ArgumentException.ThrowIfNullOrEmpty(schemaId);
        _degraded.TryRemove(schemaId, out _);
    }

    /// <inheritdoc />
    public bool IsDegraded(string schemaId)
    {
        ArgumentException.ThrowIfNullOrEmpty(schemaId);
        return _degraded.ContainsKey(schemaId);
    }
}
