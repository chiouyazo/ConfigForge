using System.Text.Json;

namespace ConfigForge.Hub.Core.Instances;

/// <summary>
/// Persists the Hub's own HTTP Basic Auth password (protected, never plaintext) to a local JSON
/// file, separate from <see cref="LocalInstanceStore"/> so a corrupt or hand-edited instance list
/// can never accidentally drop the Hub's own access control.
/// </summary>
internal sealed class HubSecurityStore(string filePath)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(
        JsonSerializerDefaults.Web
    )
    {
        WriteIndented = true,
    };

    public async Task<string?> LoadPasswordAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        await using FileStream stream = File.OpenRead(filePath);
        HubSecurityData? data = await JsonSerializer.DeserializeAsync<HubSecurityData>(
            stream,
            SerializerOptions,
            cancellationToken
        );
        return data?.PasswordProtected;
    }

    public async Task SavePasswordAsync(
        string? passwordProtected,
        CancellationToken cancellationToken = default
    )
    {
        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using FileStream stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(
            stream,
            new HubSecurityData { PasswordProtected = passwordProtected },
            SerializerOptions,
            cancellationToken
        );
    }

    private sealed class HubSecurityData
    {
        public string? PasswordProtected { get; set; }
    }
}
