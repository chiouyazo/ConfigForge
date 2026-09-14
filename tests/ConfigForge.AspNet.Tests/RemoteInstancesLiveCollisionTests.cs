using System.Net.Http.Json;
using System.Text.Json;
using ConfigForge.Abstractions;
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
/// Live, real-HTTP proof that <see cref="AspNetConfigForgeOptions.RemoteInstances"/> is a genuine
/// host-agnostic feature and not Hub-specific: a host with its OWN real local schema ("General")
/// enables <see cref="AspNetConfigForgeOptions.RemoteInstances"/> alongside it, pointed at two
/// separately running ConfigForge.AspNet instances that - on purpose - both expose a category
/// labelled "Users". All three processes are real Kestrel servers on real loopback sockets, not
/// in-memory <c>TestServer</c>s, so the poll and the action relay are genuine network round trips.
/// </summary>
public sealed class RemoteInstancesLiveCollisionTests : IAsyncLifetime
{
    private const string RemoteSchemaJson = """
        {
          "schema": {
            "type": "object",
            "properties": {
              "users": {
                "type": "object",
                "x-key-format": "uuid",
                "additionalProperties": { "type": "object", "properties": { "name": { "type": "string", "title": "Name" } } }
              }
            }
          },
          "uiSchema": {
            "type": "Categorization",
            "elements": [ { "type": "Category", "label": "Users", "elements": [ { "type": "Control", "scope": "#/properties/users" } ] } ]
          },
          "x-cf": {
            "id": "remote",
            "categories": { "Users": { "collection": "users", "collectionLabel": "name" } },
            "actions": [ { "actionId": "users.sync", "label": "Sync", "placement": { "category": "Users" } } ]
          }
        }
        """;

    private const string LocalSchemaJson = """
        {
          "schema": { "type": "object", "properties": { "endpointUrl": { "type": "string", "title": "Endpoint URL" } } },
          "uiSchema": {
            "type": "Categorization",
            "elements": [ { "type": "Category", "label": "General", "elements": [ { "type": "Control", "scope": "#/properties/endpointUrl" } ] } ]
          },
          "x-cf": { "id": "host", "name": "Host App" }
        }
        """;

    private WebApplication? _instanceA;
    private WebApplication? _instanceB;
    private WebApplication? _host;
    private readonly List<string> _relayHits = [];

    public async Task InitializeAsync()
    {
        _instanceA = await StartRemoteInstanceAsync("instanceA");
        _instanceB = await StartRemoteInstanceAsync("instanceB");
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

    private async Task<WebApplication> StartRemoteInstanceAsync(string relayTag)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddConfigForge(options =>
        {
            options.PathPrefix = "/config-ui";
            options.OnLoad = _ =>
                Task.FromResult<string?>(
                    """{ "users": { "11111111-1111-1111-1111-111111111111": { "name": "Ada" } } }"""
                );
        });

        WebApplication app = builder.Build();
        app.UseRouting();

        var parser = app.Services.GetRequiredService<IJsonFormsSchemaParser>();
        ConfigSchema schema = parser.Parse(RemoteSchemaJson);
        app.Services.GetRequiredService<IConfigForgeHostState>().UpsertSchema(schema);
        ((IEndpointRouteBuilder)app).MapDashboardEndpoints("/config-ui");

        IPluginRegistry registry = app.Services.GetRequiredService<IPluginRegistry>();
        registry.RegisterAction(
            "users.sync",
            ctx =>
            {
                _relayHits.Add(relayTag);
                return ctx.ShowToastAsync("synced", ToastSeverity.Success);
            }
        );

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
            options.OnLoad = _ => Task.FromResult<string?>(null);
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

    private static async Task<JsonElement> GetManifestAsync(WebApplication app, string schemaId)
    {
        string baseUrl = BaseUrlOf(app);
        using var client = new HttpClient { BaseAddress = new Uri(baseUrl) };
        JsonElement manifest = default;
        for (int attempt = 0; attempt < 50; attempt++)
        {
            HttpResponseMessage response = await client.GetAsync(
                new Uri($"/config-ui/manifest?schemaId={schemaId}", UriKind.Relative)
            );
            response.EnsureSuccessStatusCode();
            using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            manifest = doc.RootElement.Clone();
            if (manifest.GetProperty("categories").GetArrayLength() >= 3)
            {
                return manifest;
            }

            await Task.Delay(100);
        }

        return manifest;
    }

    [Fact]
    public async Task HostManifest_ComposesOwnLocalCategoryWithBothRemoteInstancesCategories()
    {
        JsonElement manifest = await GetManifestAsync(_host!, "host");

        JsonElement categories = manifest.GetProperty("categories");
        List<string> labels =
        [
            .. categories.EnumerateArray().Select(c => c.GetProperty("label").GetString()!),
        ];

        Assert.Contains("General", labels);
        Assert.Equal(2, labels.Count(l => l == "Users"));

        List<string?> collectionKeys =
        [
            .. categories.EnumerateArray().Select(c => c.GetProperty("collectionKey").GetString()),
        ];
        Assert.Contains("instanceA__users", collectionKeys);
        Assert.Contains("instanceB__users", collectionKeys);

        JsonElement actions = manifest.GetProperty("actions");
        List<string> actionIds =
        [
            .. actions.EnumerateArray().Select(a => a.GetProperty("actionId").GetString()!),
        ];
        Assert.Contains("instanceA::users.sync", actionIds);
        Assert.Contains("instanceB::users.sync", actionIds);
    }

    [Fact]
    public async Task ActionRelay_EachNamespacedActionId_ReachesOnlyItsOwnInstance_NeverTheOther()
    {
        await GetManifestAsync(_host!, "host");
        string hostBaseUrl = BaseUrlOf(_host!);
        using var client = new HttpClient { BaseAddress = new Uri(hostBaseUrl) };

        _relayHits.Clear();
        HttpResponseMessage responseA = await client.PostAsJsonAsync(
            new Uri("/config-ui/action/instanceA::users.sync?schemaId=host", UriKind.Relative),
            new { }
        );
        responseA.EnsureSuccessStatusCode();
        Assert.Equal(["instanceA"], _relayHits);

        _relayHits.Clear();
        HttpResponseMessage responseB = await client.PostAsJsonAsync(
            new Uri("/config-ui/action/instanceB::users.sync?schemaId=host", UriKind.Relative),
            new { }
        );
        responseB.EnsureSuccessStatusCode();
        Assert.Equal(["instanceB"], _relayHits);
    }
}
