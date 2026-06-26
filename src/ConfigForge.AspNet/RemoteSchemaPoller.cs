using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using ConfigForge.Core.Schema;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ConfigForge.AspNet;

/// <summary>
/// A hosted background service that loads schemas from a remote manifest
/// (<see cref="AspNetConfigForgeOptions.SchemaUrl"/>) over HTTP(S) and keeps the
/// <see cref="IConfigForgeHostState"/> in sync by re-polling on an interval.
/// </summary>
/// <remarks>
/// The manifest is a JSON array; each entry is a string URL or an object
/// <c>{ "url": "…" }</c>. Relative URLs resolve against the manifest URL, and each
/// schema's id comes from its <c>x-cf.id</c>. Schemas that disappear from the
/// manifest are removed from the host state. Only runs when a
/// <see cref="AspNetConfigForgeOptions.SchemaUrl"/> is configured.
/// </remarks>
public sealed class RemoteSchemaPoller : IHostedService, IDisposable
{
    private static readonly Action<ILogger, string, Exception?> LogManifestFailed =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(1, nameof(LogManifestFailed)),
            "Failed to load schema manifest from {ManifestUrl}."
        );

    private static readonly Action<ILogger, string, Exception?> LogSchemaFailed =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(2, nameof(LogSchemaFailed)),
            "Failed to load remote schema from {SchemaUrl}."
        );

    private static readonly Action<ILogger, int, string, Exception?> LogLoaded =
        LoggerMessage.Define<int, string>(
            LogLevel.Information,
            new EventId(3, nameof(LogLoaded)),
            "Loaded {Count} remote schema(s) from {ManifestUrl}."
        );

    private readonly IConfigForgeHostState _state;
    private readonly IJsonFormsSchemaParser _parser;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<RemoteSchemaPoller> _logger;

    private readonly HashSet<string> _remoteIds = new(StringComparer.Ordinal);
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private bool _disposed;

    /// <summary>Creates the poller.</summary>
    /// <param name="state">The shared host state to keep in sync.</param>
    /// <param name="parser">The schema parser.</param>
    /// <param name="httpClientFactory">Factory for the HTTP client used to fetch schemas.</param>
    /// <param name="logger">The logger for diagnostics.</param>
    public RemoteSchemaPoller(
        IConfigForgeHostState state,
        IJsonFormsSchemaParser parser,
        IHttpClientFactory httpClientFactory,
        ILogger<RemoteSchemaPoller> logger
    )
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(parser);
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(logger);

        _state = state;
        _parser = parser;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.Options.SchemaUrl))
        {
            return;
        }

        // Load once before returning so schemas are available as the app starts.
        await PollAsync(cancellationToken).ConfigureAwait(false);

        _cts = new CancellationTokenSource();
        _loop = RunAsync(_cts.Token);
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts is null)
        {
            return;
        }

        await _cts.CancelAsync().ConfigureAwait(false);
        if (_loop is not null)
        {
            try
            {
                await _loop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown.
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cts?.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        int seconds = Math.Max(5, _state.Options.SchemaRefreshSeconds);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(seconds));

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                await PollAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
    }

    private async Task PollAsync(CancellationToken cancellationToken)
    {
        string manifestUrl = _state.Options.SchemaUrl!;
        HttpClient client = _httpClientFactory.CreateClient(nameof(RemoteSchemaPoller));

        List<ManifestEntry> entries;
        try
        {
            var manifestUri = new Uri(manifestUrl);
            string manifestJson = await client
                .GetStringAsync(manifestUri, cancellationToken)
                .ConfigureAwait(false);
            entries = ParseManifest(manifestJson, manifestUri);
        }
        catch (Exception ex)
            when (ex is HttpRequestException or JsonException or UriFormatException)
        {
            LogManifestFailed(_logger, manifestUrl, ex);
            return;
        }

        var loadedIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (ManifestEntry entry in entries)
        {
            string? id = await LoadSchemaAsync(client, entry, cancellationToken)
                .ConfigureAwait(false);
            if (id is not null)
            {
                loadedIds.Add(id);
            }
        }

        foreach (string removed in _remoteIds.Except(loadedIds).ToList())
        {
            _state.RemoveSchema(removed);
        }

        _remoteIds.Clear();
        _remoteIds.UnionWith(loadedIds);
        LogLoaded(_logger, loadedIds.Count, manifestUrl, null);
    }

    private async Task<string?> LoadSchemaAsync(
        HttpClient client,
        ManifestEntry entry,
        CancellationToken cancellationToken
    )
    {
        try
        {
            string json = await client
                .GetStringAsync(entry.Url, cancellationToken)
                .ConfigureAwait(false);
            ConfigSchema schema = _parser.Parse(json);
            _state.UpsertSchema(schema);
            return schema.Id;
        }
        catch (Exception ex)
            when (ex is HttpRequestException or SchemaParseException or JsonException)
        {
            LogSchemaFailed(_logger, entry.Url.ToString(), ex);
            return null;
        }
    }

    private static List<ManifestEntry> ParseManifest(string json, Uri baseUri)
    {
        List<ManifestEntry> result = [];
        if (JsonNode.Parse(json) is not JsonArray array)
        {
            return result;
        }

        foreach (JsonNode? node in array)
        {
            switch (node)
            {
                case JsonValue value when value.GetValueKind() == JsonValueKind.String:
                    result.Add(new ManifestEntry(new Uri(baseUri, value.GetValue<string>())));
                    break;
                case JsonObject obj when obj["url"]?.GetValue<string>() is { Length: > 0 } url:
                    result.Add(new ManifestEntry(new Uri(baseUri, url)));
                    break;
                default:
                    break;
            }
        }

        return result;
    }

    private readonly record struct ManifestEntry(Uri Url);
}
