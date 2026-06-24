using ConfigForge.Abstractions;
using ConfigForge.Core.Plugins;
using ConfigForge.Core.Schema;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ConfigForge.AspNet;

/// <summary>
/// A hosted background service that watches the schema and plugin directories in
/// <see cref="ConfigForgeMode.Open"/> mode and keeps the
/// <see cref="IConfigForgeHostState"/> in sync.
/// </summary>
/// <remarks>
/// <para>
/// On schema add or change the document is reparsed and the available schema list
/// updated. On plugin add or change the assembly is reloaded through the
/// <see cref="IPluginLoader"/>. On plugin removal every loaded schema whose
/// <see cref="ConfigSchema.PluginId"/> is no longer present in
/// <see cref="IPluginCatalog.RegisteredPluginIds"/> is marked degraded: its actions
/// and loaders are disabled while its fields remain editable.
/// </para>
/// <para>
/// In <see cref="ConfigForgeMode.Locked"/> mode the watcher performs an initial scan
/// only and does not subscribe to file-system events, since the schema set is fixed.
/// </para>
/// </remarks>
public sealed class ConfigForgeDirectoryWatcher : IHostedService, IDisposable
{
    private static readonly Action<ILogger, string, Exception?> LogPluginLoadFailed =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(1, nameof(LogPluginLoadFailed)),
            "Failed to load plugins from {PluginDirectory}."
        );

    private static readonly Action<ILogger, string, Exception?> LogSchemaParseFailed =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(2, nameof(LogSchemaParseFailed)),
            "Failed to parse schema {SchemaPath}."
        );

    private static readonly Action<ILogger, string, Exception?> LogSchemaReadDeferred =
        LoggerMessage.Define<string>(
            LogLevel.Debug,
            new EventId(3, nameof(LogSchemaReadDeferred)),
            "Schema {SchemaPath} could not be read yet."
        );

    private static readonly Action<ILogger, string, string, Exception?> LogSchemaDegraded =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(4, nameof(LogSchemaDegraded)),
            "Schema {SchemaId} depends on plugin {PluginId}, which is not loaded; marking degraded."
        );

    private static readonly Action<ILogger, string, Exception?> LogPluginReloadFailed =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(5, nameof(LogPluginReloadFailed)),
            "Failed to reload plugin {PluginPath}."
        );

    private readonly IConfigForgeHostState _state;
    private readonly IJsonFormsSchemaParser _parser;
    private readonly IPluginLoader _pluginLoader;
    private readonly IPluginCatalog _pluginCatalog;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<ConfigForgeDirectoryWatcher> _logger;

    private FileSystemWatcher? _schemaWatcher;
    private FileSystemWatcher? _pluginWatcher;
    private bool _disposed;

    /// <summary>Creates the watcher.</summary>
    /// <param name="state">The shared host state to keep in sync.</param>
    /// <param name="parser">The schema parser used to reparse changed schemas.</param>
    /// <param name="pluginLoader">The loader used to (re)load plugin assemblies.</param>
    /// <param name="pluginCatalog">The catalog queried to detect missing plugins.</param>
    /// <param name="environment">The host environment, used to resolve the content root.</param>
    /// <param name="logger">The logger for diagnostics.</param>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    public ConfigForgeDirectoryWatcher(
        IConfigForgeHostState state,
        IJsonFormsSchemaParser parser,
        IPluginLoader pluginLoader,
        IPluginCatalog pluginCatalog,
        IHostEnvironment environment,
        ILogger<ConfigForgeDirectoryWatcher> logger
    )
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(parser);
        ArgumentNullException.ThrowIfNull(pluginLoader);
        ArgumentNullException.ThrowIfNull(pluginCatalog);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(logger);

        _state = state;
        _parser = parser;
        _pluginLoader = pluginLoader;
        _pluginCatalog = pluginCatalog;
        _environment = environment;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        string schemaDir = ResolveDirectory(_state.Options.SchemaDirectory);
        string pluginDir = ResolveDirectory(_state.Options.PluginDirectory);

        await LoadAllPluginsAsync(pluginDir).ConfigureAwait(false);
        LoadAllSchemas(schemaDir);
        ReconcileDegradedSchemas();

        if (_state.Options.Mode != ConfigForgeMode.Open)
        {
            return;
        }

        _schemaWatcher = CreateSchemaWatcher(schemaDir);
        _pluginWatcher = CreatePluginWatcher(pluginDir);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _schemaWatcher?.Dispose();
        _pluginWatcher?.Dispose();
        _schemaWatcher = null;
        _pluginWatcher = null;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _schemaWatcher?.Dispose();
        _pluginWatcher?.Dispose();
    }

    private string ResolveDirectory(string directory) =>
        Path.IsPathRooted(directory)
            ? directory
            : Path.Combine(_environment.ContentRootPath, directory);

    private FileSystemWatcher? CreateSchemaWatcher(string schemaDir)
    {
        if (!Directory.Exists(schemaDir))
        {
            return null;
        }

        var watcher = new FileSystemWatcher(schemaDir, "*.json")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
        };
        watcher.Created += OnSchemaChanged;
        watcher.Changed += OnSchemaChanged;
        watcher.Renamed += OnSchemaChanged;
        watcher.Deleted += OnSchemaDeleted;
        watcher.EnableRaisingEvents = true;
        return watcher;
    }

    private FileSystemWatcher? CreatePluginWatcher(string pluginDir)
    {
        if (!Directory.Exists(pluginDir))
        {
            return null;
        }

        var watcher = new FileSystemWatcher(pluginDir, "*.dll")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
        };
        watcher.Created += OnPluginChanged;
        watcher.Changed += OnPluginChanged;
        watcher.Renamed += OnPluginChanged;
        watcher.Deleted += OnPluginDeleted;
        watcher.EnableRaisingEvents = true;
        return watcher;
    }

    private void LoadAllSchemas(string schemaDir)
    {
        if (!Directory.Exists(schemaDir))
        {
            return;
        }

        foreach (string path in Directory.EnumerateFiles(schemaDir, "*.json"))
        {
            TryParseSchema(path);
        }
    }

    private async Task LoadAllPluginsAsync(string pluginDir)
    {
        if (!Directory.Exists(pluginDir))
        {
            return;
        }

        try
        {
            await _pluginLoader.LoadFromDirectoryAsync(pluginDir).ConfigureAwait(false);
        }
        catch (Exception ex)
            when (ex is IOException or BadImageFormatException or InvalidOperationException)
        {
            LogPluginLoadFailed(_logger, pluginDir, ex);
        }
    }

    private void TryParseSchema(string path)
    {
        try
        {
            ConfigSchema schema = _parser.ParseFromFile(path);
            _state.UpsertSchema(schema);
        }
        catch (SchemaParseException ex)
        {
            LogSchemaParseFailed(_logger, path, ex);
        }
        catch (IOException ex)
        {
            LogSchemaReadDeferred(_logger, path, ex);
        }
    }

    /// <summary>
    /// Marks every loaded schema whose owning plugin is no longer registered as
    /// degraded, and clears the flag for schemas whose plugin is present again.
    /// </summary>
    private void ReconcileDegradedSchemas()
    {
        var registered = new HashSet<string>(
            _pluginCatalog.RegisteredPluginIds,
            StringComparer.Ordinal
        );

        foreach (ConfigSchema schema in _state.Schemas.Values)
        {
            if (string.IsNullOrEmpty(schema.PluginId))
            {
                continue;
            }

            if (registered.Contains(schema.PluginId))
            {
                _state.ClearDegraded(schema.Id);
            }
            else
            {
                LogSchemaDegraded(_logger, schema.Id, schema.PluginId, null);
                _state.MarkDegraded(schema.Id);
            }
        }
    }

    private void OnSchemaChanged(object sender, FileSystemEventArgs e)
    {
        TryParseSchema(e.FullPath);
        ReconcileDegradedSchemas();
    }

    private void OnSchemaDeleted(object sender, FileSystemEventArgs e)
    {
        ReconcileDegradedSchemas();
    }

    private async void OnPluginChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            await _pluginLoader.LoadAssemblyAsync(e.FullPath).ConfigureAwait(false);
        }
        catch (Exception ex)
            when (ex is IOException or BadImageFormatException or InvalidOperationException)
        {
            LogPluginReloadFailed(_logger, e.FullPath, ex);
        }

        ReconcileDegradedSchemas();
    }

    private void OnPluginDeleted(object sender, FileSystemEventArgs e)
    {
        ReconcileDegradedSchemas();
    }
}
