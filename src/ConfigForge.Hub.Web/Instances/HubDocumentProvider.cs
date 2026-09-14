using System.Text.Json;
using System.Text.Json.Nodes;
using ConfigForge.AspNet;
using ConfigForge.Hub.Core.Instances;

namespace ConfigForge.Hub.Web.Instances;

/// <summary>
/// Backs the "hub" schema's <c>OnLoad</c>/<c>OnSave</c>: the only category the Hub itself owns
/// is "Manage Instances" (<see cref="HubLocalSchema"/>), so this is the whole document. Saving
/// also updates the live <see cref="AspNetConfigForgeOptions.RemoteInstances"/> list, which the
/// generic RemoteInstances poller picks up on its next cycle.
/// </summary>
internal sealed class HubDocumentProvider(
    LocalInstanceStore localStore,
    AspNetConfigForgeOptions options
)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(
        JsonSerializerDefaults.Web
    );

    public async Task<string?> LoadAsync(string schemaId)
    {
        if (!string.Equals(schemaId, HubLocalSchema.SchemaId, StringComparison.Ordinal))
        {
            return null;
        }

        Dictionary<string, StoredInstance> stored = await localStore.LoadAsync();
        return BuildDocumentJson(stored);
    }

    public async Task SaveAsync(string schemaId, string json)
    {
        if (!string.Equals(schemaId, HubLocalSchema.SchemaId, StringComparison.Ordinal))
        {
            return;
        }

        Dictionary<string, StoredInstance> stored = ParseDocumentJson(json);
        ThrowIfDuplicateNames(stored);
        await localStore.SaveAsync(stored);

        options.RemoteInstances =
        [
            .. stored.Values.Select(StoredInstanceMapper.ToRemoteInstanceOptions),
        ];
    }

    private static void ThrowIfDuplicateNames(Dictionary<string, StoredInstance> stored)
    {
        string? duplicateName = stored
            .Values.Select(i => i.Name)
            .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .FirstOrDefault();

        if (duplicateName is not null)
        {
            throw new InvalidOperationException(
                $"Instance name '{duplicateName}' is used by more than one instance. Instance names must be unique."
            );
        }
    }

    private static string BuildDocumentJson(Dictionary<string, StoredInstance> stored)
    {
        var root = new JsonObject
        {
            [HubLocalSchema.ManageInstancesCollectionKey] = new JsonObject(
                stored.Select(kv =>
                    KeyValuePair.Create<string, JsonNode?>(
                        kv.Key,
                        new JsonObject
                        {
                            ["name"] = kv.Value.Name,
                            ["baseUrl"] = kv.Value.BaseUrl,
                            ["pathPrefix"] = kv.Value.PathPrefix,
                            ["username"] = kv.Value.Username,
                            ["password"] = kv.Value.Password,
                            ["capabilityAssemblyPaths"] = new JsonArray(
                                kv.Value.CapabilityAssemblyPaths.Select((string p) => (JsonNode)p)
                                    .ToArray()
                            ),
                        }
                    )
                )
            ),
        };

        return root.ToJsonString(SerializerOptions);
    }

    private static Dictionary<string, StoredInstance> ParseDocumentJson(string json)
    {
        Dictionary<string, StoredInstance> result = new(StringComparer.Ordinal);

        JsonNode? root = JsonNode.Parse(json);
        if (root?[HubLocalSchema.ManageInstancesCollectionKey] is not JsonObject instances)
        {
            return result;
        }

        foreach (KeyValuePair<string, JsonNode?> entry in instances)
        {
            if (entry.Value is not JsonObject value)
            {
                continue;
            }

            result[entry.Key] = new StoredInstance
            {
                Name = value["name"]?.GetValue<string>() ?? string.Empty,
                BaseUrl = value["baseUrl"]?.GetValue<string>() ?? string.Empty,
                PathPrefix = value["pathPrefix"]?.GetValue<string>() is { Length: > 0 } prefix
                    ? prefix
                    : "/config-ui",
                Username = value["username"]?.GetValue<string>() ?? string.Empty,
                Password = value["password"]?.GetValue<string>() ?? string.Empty,
                CapabilityAssemblyPaths = value["capabilityAssemblyPaths"] is JsonArray paths
                    ? paths
                        .Select(n => n?.GetValue<string>())
                        .Where(p => !string.IsNullOrWhiteSpace(p))
                        .Select(p => p!)
                        .ToList()
                    : [],
            };
        }

        return result;
    }
}
