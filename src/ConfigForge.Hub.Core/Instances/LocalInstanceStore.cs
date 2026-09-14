using System.Text.Json;

namespace ConfigForge.Hub.Core.Instances;

/// <summary>
/// Persists the Hub's own "Manage Instances" configuration (the connected instance list) to a
/// local JSON file, keyed by a stable id so map entries keep their identity across load/save
/// round-trips. This is the Hub's own configuration, so unlike an instance-owned category it is
/// saved locally rather than relayed anywhere.
/// </summary>
internal sealed class LocalInstanceStore(string filePath)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(
        JsonSerializerDefaults.Web
    )
    {
        WriteIndented = true,
    };

    public async Task<Dictionary<string, StoredInstance>> LoadAsync(
        CancellationToken cancellationToken = default
    )
    {
        if (!File.Exists(filePath))
        {
            return new Dictionary<string, StoredInstance>(StringComparer.Ordinal);
        }

        await using FileStream stream = File.OpenRead(filePath);
        Dictionary<string, StoredInstance>? data = await JsonSerializer.DeserializeAsync<
            Dictionary<string, StoredInstance>
        >(stream, SerializerOptions, cancellationToken);
        return data ?? new Dictionary<string, StoredInstance>(StringComparer.Ordinal);
    }

    public async Task SaveAsync(
        Dictionary<string, StoredInstance> instances,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(instances);

        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using FileStream stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(
            stream,
            instances,
            SerializerOptions,
            cancellationToken
        );
    }
}
