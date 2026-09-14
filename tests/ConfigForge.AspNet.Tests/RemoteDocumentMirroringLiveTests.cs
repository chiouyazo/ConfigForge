using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ConfigForge.AspNet.Endpoints;
using ConfigForge.AspNet.RemoteInstances;
using ConfigForge.Core.Schema;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ConfigForge.AspNet.Tests;

/// <summary>
/// Live, real-HTTP proof that full-document mirroring gives a mirrored plain category and a
/// document-backed mirrored collection real read/write parity, and that a save is always split and
/// routed to the correct origin - never the wrong one, never an origin whose fields did not
/// change. Two separately running ConfigForge.AspNet instances (real Kestrel, real sockets) are
/// aggregated by a third; every assertion is made by calling the aggregator's own headless
/// endpoints and then independently re-querying each origin instance directly.
/// </summary>
public sealed class RemoteDocumentMirroringLiveTests : IAsyncLifetime
{
    private const string RemoteSchemaJson = """
        {
          "schema": {
            "type": "object",
            "properties": {
              "endpointUrl": { "type": "string", "title": "Endpoint URL", "pattern": "^https://" },
              "users": {
                "type": "object",
                "additionalProperties": { "type": "object", "properties": { "name": { "type": "string", "title": "Name" } } }
              }
            },
            "required": [ "endpointUrl" ]
          },
          "uiSchema": {
            "type": "Categorization",
            "elements": [
              { "type": "Category", "label": "General", "elements": [ { "type": "Control", "scope": "#/properties/endpointUrl" } ] },
              { "type": "Category", "label": "Users", "elements": [ { "type": "Control", "scope": "#/properties/users" } ] }
            ]
          },
          "x-cf": {
            "id": "remote",
            "categories": { "Users": { "collection": "users", "collectionLabel": "name" } }
          }
        }
        """;

    private const string LocalSchemaJson = """
        {
          "schema": { "type": "object", "properties": { "hostField": { "type": "string", "title": "Host Field" } } },
          "uiSchema": {
            "type": "Categorization",
            "elements": [ { "type": "Category", "label": "Host", "elements": [ { "type": "Control", "scope": "#/properties/hostField" } ] } ]
          },
          "x-cf": { "id": "host", "name": "Host App" }
        }
        """;

    private WebApplication? _instanceA;
    private WebApplication? _instanceB;
    private WebApplication? _host;
    private string _instanceAStored =
        """{ "endpointUrl": "https://a.example.com", "users": { "u1": { "name": "Ada" } } }""";
    private string _instanceBStored = """{ "endpointUrl": "https://b.example.com", "users": {} }""";

    public async Task InitializeAsync()
    {
        _instanceA = await StartRemoteInstanceAsync(
            json => _instanceAStored = json,
            () => _instanceAStored
        );
        _instanceB = await StartRemoteInstanceAsync(
            json => _instanceBStored = json,
            () => _instanceBStored
        );
        _host = await StartHostWithRemoteInstancesAsync(
            BaseUrlOf(_instanceA),
            BaseUrlOf(_instanceB)
        );
    }

    public async Task DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.DisposeAsync();
        }
        if (_instanceA is not null)
        {
            await _instanceA.DisposeAsync();
        }
        if (_instanceB is not null)
        {
            await _instanceB.DisposeAsync();
        }
    }

    private static async Task<WebApplication> StartRemoteInstanceAsync(
        Action<string> onSave,
        Func<string> onLoad
    )
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddConfigForge(options =>
        {
            options.PathPrefix = "/config-ui";
            options.OnLoad = _ => Task.FromResult<string?>(onLoad());
            options.OnSave = (_, json) =>
            {
                onSave(json);
                return Task.CompletedTask;
            };
        });

        WebApplication app = builder.Build();
        app.UseRouting();

        var parser = app.Services.GetRequiredService<IJsonFormsSchemaParser>();
        ConfigSchema schema = parser.Parse(RemoteSchemaJson);
        app.Services.GetRequiredService<IConfigForgeHostState>().UpsertSchema(schema);
        ((IEndpointRouteBuilder)app).MapDashboardEndpoints("/config-ui");

        await app.StartAsync();
        return app;
    }

    private static async Task<WebApplication> StartHostWithRemoteInstancesAsync(
        string remoteAUrl,
        string remoteBUrl
    )
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddConfigForge(options =>
        {
            options.PathPrefix = "/config-ui";
            options.OnLoad = _ => Task.FromResult<string?>("""{ "hostField": "unchanged" }""");
            options.OnSave = (_, _) => Task.CompletedTask;
            options.RemoteInstancePollIntervalSeconds = 1;
            options.RemoteInstances =
            [
                new RemoteInstanceOptions { Name = "instanceA", BaseUrl = new Uri(remoteAUrl) },
                new RemoteInstanceOptions { Name = "instanceB", BaseUrl = new Uri(remoteBUrl) },
            ];
        });

        WebApplication app = builder.Build();
        app.UseRouting();

        var parser = app.Services.GetRequiredService<IJsonFormsSchemaParser>();
        ConfigSchema localSchema = parser.Parse(LocalSchemaJson);
        app.Services.GetRequiredService<IConfigForgeHostState>().UpsertSchema(localSchema);
        ((IEndpointRouteBuilder)app).MapDashboardEndpoints("/config-ui");

        await app.StartAsync();
        return app;
    }

    private static string BaseUrlOf(WebApplication app) =>
        app
            .Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!
            .Addresses.First();

    private static async Task<JsonElement> GetMergedDocumentAsync(WebApplication host)
    {
        using var client = new HttpClient { BaseAddress = new Uri(BaseUrlOf(host)) };
        JsonElement document = default;
        for (int attempt = 0; attempt < 50; attempt++)
        {
            HttpResponseMessage response = await client.GetAsync(
                new Uri("/config-ui/document?schemaId=host", UriKind.Relative)
            );
            response.EnsureSuccessStatusCode();
            using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            document = doc.RootElement.Clone();
            if (document.TryGetProperty("instanceA__endpointUrl", out _))
            {
                return document;
            }

            await Task.Delay(100);
        }

        return document;
    }

    [Fact]
    public async Task GetDocument_MirrorsPlainFieldsAndDocumentBackedCollectionEntries_FromBothInstances()
    {
        JsonElement merged = await GetMergedDocumentAsync(_host!);

        Assert.Equal(
            "https://a.example.com",
            merged.GetProperty("instanceA__endpointUrl").GetString()
        );
        Assert.Equal(
            "https://b.example.com",
            merged.GetProperty("instanceB__endpointUrl").GetString()
        );
        Assert.Equal(
            "Ada",
            merged.GetProperty("instanceA__users").GetProperty("u1").GetProperty("name").GetString()
        );
    }

    [Fact]
    public async Task PostDocument_ChangedFieldOnOneInstance_ReachesOnlyThatInstance()
    {
        JsonElement merged = await GetMergedDocumentAsync(_host!);
        Dictionary<string, object?> patch = ToDictionary(merged);
        patch["instanceA__endpointUrl"] = "https://a-changed.example.com";

        using var client = new HttpClient { BaseAddress = new Uri(BaseUrlOf(_host!)) };
        HttpResponseMessage response = await client.PostAsJsonAsync(
            new Uri("/config-ui/document?schemaId=host", UriKind.Relative),
            patch
        );
        response.EnsureSuccessStatusCode();

        using JsonDocument aDoc = JsonDocument.Parse(_instanceAStored);
        Assert.Equal(
            "https://a-changed.example.com",
            aDoc.RootElement.GetProperty("endpointUrl").GetString()
        );

        using JsonDocument bDoc = JsonDocument.Parse(_instanceBStored);
        Assert.Equal(
            "https://b.example.com",
            bDoc.RootElement.GetProperty("endpointUrl").GetString()
        );
    }

    [Fact]
    public async Task PostDocument_AddedCollectionEntry_RoundTripsThroughOriginsRealSaveDocumentBacked()
    {
        JsonElement merged = await GetMergedDocumentAsync(_host!);
        Dictionary<string, object?> patch = ToDictionary(merged);
        var users = (Dictionary<string, object?>)patch["instanceA__users"]!;
        users["u2"] = new Dictionary<string, object?> { ["name"] = "Grace" };

        using var client = new HttpClient { BaseAddress = new Uri(BaseUrlOf(_host!)) };
        HttpResponseMessage response = await client.PostAsJsonAsync(
            new Uri("/config-ui/document?schemaId=host", UriKind.Relative),
            patch
        );
        response.EnsureSuccessStatusCode();

        using JsonDocument aDoc = JsonDocument.Parse(_instanceAStored);
        Assert.Equal(
            "Grace",
            aDoc.RootElement.GetProperty("users").GetProperty("u2").GetProperty("name").GetString()
        );
    }

    [Fact]
    public async Task PostDocument_OriginRejectsPatternOnlyItEnforces_SurfacesStructuredError_AndOriginUnchanged()
    {
        // The "pattern" constraint on endpointUrl is enforced by the origin's real schema, but the
        // manifest DTO the aggregator mirrors carries only Key/ControlType/Title/Required - not
        // arbitrary schema constraints. So the host's own local validation of the merged document
        // cannot catch this; only the origin's real POST /document validation can, proving the
        // rejection genuinely comes from the origin's own pipeline, not a shadow copy of it.
        JsonElement merged = await GetMergedDocumentAsync(_host!);
        Dictionary<string, object?> patch = ToDictionary(merged);
        patch["instanceA__endpointUrl"] = "not-a-url";

        using var client = new HttpClient { BaseAddress = new Uri(BaseUrlOf(_host!)) };
        HttpResponseMessage response = await client.PostAsJsonAsync(
            new Uri("/config-ui/document?schemaId=host", UriKind.Relative),
            patch
        );

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Contains(
            "instanceA",
            body.RootElement.GetProperty("error").GetString(),
            StringComparison.Ordinal
        );

        using JsonDocument aDoc = JsonDocument.Parse(_instanceAStored);
        Assert.Equal(
            "https://a.example.com",
            aDoc.RootElement.GetProperty("endpointUrl").GetString()
        );
    }

    private static Dictionary<string, object?> ToDictionary(JsonElement element)
    {
        var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            dict[property.Name] = ToValue(property.Value);
        }

        return dict;
    }

    private static object? ToValue(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.Object => ToDictionary(element),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.ToString(),
        };
}
