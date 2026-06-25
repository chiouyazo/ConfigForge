using ConfigForge.Abstractions;

namespace ConfigForge.AspNet;

/// <summary>
/// Configuration options that control how the ConfigForge UI is hosted inside an
/// ASP.NET Core application. An instance is populated by the
/// <c>configure</c> delegate passed to
/// <see cref="ServiceCollectionExtensions.AddConfigForge(Microsoft.Extensions.DependencyInjection.IServiceCollection, System.Action{AspNetConfigForgeOptions})"/>.
/// </summary>
public sealed class AspNetConfigForgeOptions
{
    /// <summary>
    /// The URL path prefix under which the ConfigForge UI is mounted.
    /// Defaults to <c>/config-ui</c>.
    /// </summary>
    public string PathPrefix { get; set; } = "/config-ui";

    /// <summary>
    /// The directory, relative to the application content root, that is scanned
    /// for plugin assemblies. Defaults to <c>plugins</c>.
    /// </summary>
    public string PluginDirectory { get; set; } = "plugins";

    /// <summary>
    /// The directory, relative to the application content root, that is scanned
    /// for combined ConfigForge schema documents. Defaults to <c>schemas</c>.
    /// </summary>
    public string SchemaDirectory { get; set; } = "schemas";

    /// <summary>
    /// Determines whether the user may change the active schema. In
    /// <see cref="ConfigForgeMode.Locked"/> mode a <see cref="OnSave"/> handler is
    /// required. Defaults to <see cref="ConfigForgeMode.Open"/>.
    /// </summary>
    public ConfigForgeMode Mode { get; set; } = ConfigForgeMode.Open;

    /// <summary>
    /// Supplies the visual theme applied to the UI. Defaults to a
    /// <see cref="DefaultThemeProvider"/>.
    /// </summary>
    public IThemeProvider ThemeProvider { get; set; } = new DefaultThemeProvider();

    /// <summary>
    /// The title shown in the UI header. Defaults to <c>Configuration</c>.
    /// </summary>
    public string ApplicationTitle { get; set; } = "Configuration";

    /// <summary>
    /// Optional callback invoked when a configuration document is saved. The first
    /// argument is the schema or document identifier, the second is the serialized
    /// document payload. Required in <see cref="ConfigForgeMode.Locked"/> mode.
    /// </summary>
    public Func<string, string, Task>? OnSave { get; set; }

    /// <summary>
    /// Optional callback invoked to load a previously persisted configuration
    /// document by identifier. Returns the serialized payload, or <see langword="null"/>
    /// when no document exists.
    /// </summary>
    public Func<string, Task<string?>>? OnLoad { get; set; }

    /// <summary>
    /// Opt-in convenience: wires <see cref="OnSave"/> and <see cref="OnLoad"/> to a
    /// <see cref="LocalConfigFileStore"/> that persists each schema's document under
    /// <paramref name="directory"/> and keeps the most recent rotated backups. Hosts
    /// that manage their own storage simply set <see cref="OnSave"/>/<see cref="OnLoad"/>
    /// themselves and ignore this.
    /// </summary>
    /// <param name="directory">The folder to store current documents and backups in.</param>
    /// <param name="keepBackups">How many rotated backups to retain per schema. 0 disables backups.</param>
    /// <returns>The same options instance, for chaining.</returns>
    public AspNetConfigForgeOptions UseLocalFileStore(string directory, int keepBackups = 10)
    {
        var store = new LocalConfigFileStore(directory, keepBackups);
        OnSave = store.SaveAsync;
        OnLoad = store.LoadAsync;
        return this;
    }
}
