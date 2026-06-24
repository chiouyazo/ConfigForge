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
    private readonly ConcurrentDictionary<string, ConfigSchema> _schemas = new(
        StringComparer.Ordinal
    );

    private readonly ConcurrentDictionary<string, byte> _degraded = new(StringComparer.Ordinal);

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
        _schemas[schema.Id] = schema;
    }

    /// <inheritdoc />
    public void RemoveSchema(string schemaId)
    {
        ArgumentException.ThrowIfNullOrEmpty(schemaId);
        _schemas.TryRemove(schemaId, out _);
        _degraded.TryRemove(schemaId, out _);
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
