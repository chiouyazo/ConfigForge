using System.Globalization;

namespace ConfigForge.AspNet;

/// <summary>
/// An optional local file store for configuration documents with rotating backups.
/// ConfigForge itself never persists anything; this is a ready-made implementation
/// of the <see cref="AspNetConfigForgeOptions.OnSave"/> / <see cref="AspNetConfigForgeOptions.OnLoad"/>
/// delegates that a host can opt into via
/// <see cref="AspNetConfigForgeOptions.UseLocalFileStore(string, int)"/>.
/// </summary>
/// <remarks>
/// Each save writes the current document to <c>{directory}/{schemaId}.json</c> and,
/// when <see cref="KeepBackups"/> is positive, also drops a timestamped copy in a
/// <c>backups</c> subfolder, pruning to the most recent <see cref="KeepBackups"/>
/// versions per schema.
/// </remarks>
public sealed class LocalConfigFileStore
{
    private readonly string _directory;
    private readonly string _backupDirectory;

    /// <summary>Creates a store rooted at <paramref name="directory"/>.</summary>
    /// <param name="directory">The folder current configurations are written to.</param>
    /// <param name="keepBackups">How many rotated backups to retain per schema. 0 disables backups.</param>
    public LocalConfigFileStore(string directory, int keepBackups = 10)
    {
        ArgumentException.ThrowIfNullOrEmpty(directory);
        _directory = directory;
        _backupDirectory = Path.Combine(directory, "backups");
        KeepBackups = Math.Max(0, keepBackups);
    }

    /// <summary>The number of rotated backups retained per schema.</summary>
    public int KeepBackups { get; }

    /// <summary>Reads the current document for a schema, or null when none is stored.</summary>
    /// <param name="schemaId">The schema identifier.</param>
    /// <returns>The stored JSON, or null.</returns>
    public async Task<string?> LoadAsync(string schemaId)
    {
        string path = MainPath(schemaId);
        if (!File.Exists(path))
        {
            return null;
        }

        return await File.ReadAllTextAsync(path).ConfigureAwait(false);
    }

    /// <summary>Writes the current document and rotates a timestamped backup copy.</summary>
    /// <param name="schemaId">The schema identifier.</param>
    /// <param name="json">The document JSON to persist.</param>
    public async Task SaveAsync(string schemaId, string json)
    {
        Directory.CreateDirectory(_directory);

        if (KeepBackups > 0)
        {
            Directory.CreateDirectory(_backupDirectory);
            string stamp = DateTime.Now.ToString(
                "yyyyMMdd-HHmmss-fff",
                CultureInfo.InvariantCulture
            );
            await File.WriteAllTextAsync(BackupPath(schemaId, stamp), json).ConfigureAwait(false);
            PruneBackups(schemaId);
        }

        await File.WriteAllTextAsync(MainPath(schemaId), json).ConfigureAwait(false);
    }

    private void PruneBackups(string schemaId)
    {
        string prefix = $"{Sanitize(schemaId)}.";
        List<string> backups =
        [
            .. Directory
                .EnumerateFiles(_backupDirectory, $"{prefix}*.json")
                .OrderByDescending(p => p, StringComparer.Ordinal),
        ];

        foreach (string stale in backups.Skip(KeepBackups))
        {
            try
            {
                File.Delete(stale);
            }
            catch (IOException)
            {
                // A backup that cannot be deleted is left in place rather than failing the save.
            }
        }
    }

    private string MainPath(string schemaId) =>
        Path.Combine(_directory, $"{Sanitize(schemaId)}.json");

    private string BackupPath(string schemaId, string stamp) =>
        Path.Combine(_backupDirectory, $"{Sanitize(schemaId)}.{stamp}.json");

    private static string Sanitize(string schemaId)
    {
        char[] chars = schemaId.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(Path.GetInvalidFileNameChars(), chars[i]) >= 0 || chars[i] == '.')
            {
                chars[i] = '_';
            }
        }

        return new string(chars);
    }
}
