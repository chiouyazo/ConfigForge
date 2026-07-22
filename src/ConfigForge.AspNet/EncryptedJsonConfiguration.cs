using ConfigForge.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

namespace ConfigForge.AspNet;

/// <summary>
/// A JSON configuration provider that transparently decrypts protected secret values
/// (see <see cref="IConfigSecretProtector.IsProtected"/>) as the file is loaded, so every
/// downstream consumer (options binding, <c>GetConnectionString</c>, and reloads via
/// <c>IOptionsMonitor</c>) sees plaintext without knowing secrets were encrypted at rest.
/// </summary>
public sealed class EncryptedJsonConfigurationProvider : JsonConfigurationProvider
{
    private readonly IConfigSecretProtector _protector;

    /// <summary>Creates the provider for a source and cipher.</summary>
    /// <param name="source">The configuration source.</param>
    /// <param name="protector">The cipher used to decrypt protected values.</param>
    public EncryptedJsonConfigurationProvider(
        EncryptedJsonConfigurationSource source,
        IConfigSecretProtector protector
    )
        : base(source)
    {
        _protector = protector;
    }

    /// <inheritdoc />
    public override void Load(Stream stream)
    {
        base.Load(stream);

        foreach (string key in Data.Keys.ToList())
        {
            string? value = Data[key];
            if (value is not null && _protector.IsProtected(value))
            {
                Data[key] = _protector.Unprotect(value);
            }
        }
    }
}

/// <summary>A JSON configuration source whose provider decrypts protected secret values on load.</summary>
public sealed class EncryptedJsonConfigurationSource : JsonConfigurationSource
{
    private readonly IConfigSecretProtector _protector;

    /// <summary>Creates the source with the cipher its provider will decrypt with.</summary>
    /// <param name="protector">The cipher used to decrypt protected values.</param>
    public EncryptedJsonConfigurationSource(IConfigSecretProtector protector)
    {
        _protector = protector;
    }

    /// <inheritdoc />
    public override IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        EnsureDefaults(builder);
        return new EncryptedJsonConfigurationProvider(this, _protector);
    }
}

/// <summary>Adds a JSON configuration file whose protected secret values are decrypted on load.</summary>
public static class EncryptedJsonConfigurationExtensions
{
    /// <summary>
    /// Adds <paramref name="path"/> as a JSON configuration source that decrypts any value protected
    /// by <paramref name="protector"/> as it loads, so bound options and connection strings are
    /// plaintext at the point of use. Honours <paramref name="reloadOnChange"/>, so a live edit of the
    /// file re-decrypts and flows through <c>IOptionsMonitor</c>.
    /// </summary>
    /// <param name="builder">The configuration builder to add to.</param>
    /// <param name="protector">The cipher used to decrypt protected values.</param>
    /// <param name="path">The JSON file path (relative to the builder's base path).</param>
    /// <param name="optional">Whether a missing file is tolerated.</param>
    /// <param name="reloadOnChange">Whether to reload (and re-decrypt) when the file changes.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IConfigurationBuilder AddEncryptedJsonFile(
        this IConfigurationBuilder builder,
        IConfigSecretProtector protector,
        string path,
        bool optional,
        bool reloadOnChange
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(protector);
        ArgumentException.ThrowIfNullOrEmpty(path);

        EncryptedJsonConfigurationSource source = new(protector)
        {
            Path = path,
            Optional = optional,
            ReloadOnChange = reloadOnChange,
        };
        source.ResolveFileProvider();
        builder.Add(source);
        return builder;
    }
}
