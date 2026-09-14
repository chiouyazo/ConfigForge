using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ConfigForge.Abstractions;
using ConfigForge.AspNet.Endpoints;
using ConfigForge.Core.Schema;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ConfigForge.AspNet.Tests;

/// <summary>
/// End-to-end tests of the headless manifest/data/action endpoints
/// (<see cref="DashboardEndpoints.MapDashboardEndpoints"/>) over a real <see cref="TestServer"/>,
/// so they are exercised the same way a remote aggregator would reach them. Routing is wired
/// directly rather than through the full <c>UseConfigForge()</c>, which also maps the
/// interactive Razor/static-asset endpoints; those require a static-web-assets manifest that
/// only exists for a real host executable, not the test process. The endpoints under test are
/// mapped onto the same <see cref="IEndpointRouteBuilder"/> either way, so this does not change
/// what is being verified.
/// </summary>
public sealed class DashboardEndpointsTests : IAsyncDisposable
{
    private const string SchemaJson = """
        {
          "schema": {
            "type": "object",
            "properties": {
              "connectors": {
                "type": "object",
                "x-key-format": "uuid",
                "additionalProperties": {
                  "type": "object",
                  "properties": {
                    "name": { "type": "string", "title": "Name" }
                  }
                }
              },
              "endpointUrl": { "type": "string", "title": "Endpoint URL" }
            }
          },
          "uiSchema": {
            "type": "Categorization",
            "elements": [
              { "type": "Category", "label": "Connectors", "elements": [ { "type": "Control", "scope": "#/properties/connectors" } ] },
              { "type": "Category", "label": "General", "elements": [ { "type": "Control", "scope": "#/properties/endpointUrl" } ] }
            ]
          },
          "x-cf": {
            "id": "dash", "name": "Dashboard Demo",
            "categories": { "Connectors": { "collection": "connectors", "collectionLabel": "name" } },
            "actions": [
              { "actionId": "connector.test", "label": "Test connection", "requiresEntry": true, "placement": { "category": "Connectors" } },
              { "actionId": "global.ping", "label": "Ping" }
            ]
          }
        }
        """;

    // Mirrors a real host's "General" category: two CfCategory-bundled tabs ("Instance", "Hosting")
    // exposed as a nested Categorization, the same shape NavigationStructureTest asserts on that
    // host's own generated schema.
    private const string NestedGroupSchemaJson = """
        {
          "schema": {
            "type": "object",
            "properties": {
              "instanceName": { "type": "string", "title": "Instance Name" },
              "listenUrls": { "type": "array", "items": { "type": "string" }, "title": "Listen URLs" }
            }
          },
          "uiSchema": {
            "type": "Categorization",
            "elements": [
              {
                "type": "Category",
                "label": "General",
                "elements": [
                  {
                    "type": "Categorization",
                    "elements": [
                      {
                        "type": "Category",
                        "label": "Instance",
                        "elements": [ { "type": "Control", "scope": "#/properties/instanceName" } ]
                      },
                      {
                        "type": "Category",
                        "label": "Hosting",
                        "elements": [ { "type": "Control", "scope": "#/properties/listenUrls" } ]
                      }
                    ]
                  }
                ]
              }
            ]
          },
          "x-cf": { "id": "nested-demo", "name": "Nested Demo" }
        }
        """;

    private WebApplication? _app;

    private async Task<(
        HttpClient Client,
        IConfigForgeHostState State,
        IPluginRegistry Registry
    )> StartAsync(string? storedDocumentJson = null, string? schemaJson = null)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddConfigForge(options =>
        {
            options.PathPrefix = "/config-ui";
            options.OnLoad = _ => Task.FromResult(storedDocumentJson);
        });

        _app = builder.Build();
        _app.UseRouting();
        ((IEndpointRouteBuilder)_app).MapDashboardEndpoints("/config-ui");

        var parser = _app.Services.GetRequiredService<IJsonFormsSchemaParser>();
        ConfigSchema schema = parser.Parse(schemaJson ?? SchemaJson);
        var state = _app.Services.GetRequiredService<IConfigForgeHostState>();
        state.UpsertSchema(schema);

        await _app.StartAsync();

        return (_app.GetTestClient(), state, _app.Services.GetRequiredService<IPluginRegistry>());
    }

    [Fact]
    public async Task Manifest_ReturnsCategoriesAndActions_WithoutHandlerRegistrationStatus()
    {
        (HttpClient client, _, _) = await StartAsync();

        HttpResponseMessage response = await client.GetAsync(
            new Uri("/config-ui/manifest", UriKind.Relative)
        );
        response.EnsureSuccessStatusCode();

        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement root = doc.RootElement;

        Assert.Equal("dash", root.GetProperty("schemaId").GetString());
        JsonElement categories = root.GetProperty("categories");
        Assert.Equal(2, categories.GetArrayLength());

        JsonElement connectorsCategory = categories
            .EnumerateArray()
            .Single(c => c.GetProperty("label").GetString() == "Connectors");
        Assert.Equal("connectors", connectorsCategory.GetProperty("collectionKey").GetString());
        Assert.Contains(
            connectorsCategory.GetProperty("elements").EnumerateArray(),
            e => e.GetProperty("field").GetProperty("key").GetString() == "name"
        );

        JsonElement actions = root.GetProperty("actions");
        Assert.Equal(2, actions.GetArrayLength());
        JsonElement testAction = actions
            .EnumerateArray()
            .Single(a => a.GetProperty("actionId").GetString() == "connector.test");
        Assert.True(testAction.GetProperty("requiresEntry").GetBoolean());

        // No plugin registered any handler; the manifest still lists the action (schema-declared),
        // it just does not say whether a handler exists for it.
        foreach (JsonProperty property in testAction.EnumerateObject())
        {
            Assert.NotEqual("registered", property.Name, StringComparer.OrdinalIgnoreCase);
            Assert.NotEqual("hasHandler", property.Name, StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// The manifest must carry the real nested layout tree (tab groups containing fields, here),
    /// not the flat list of every field in the category the endpoint used to emit: a remote
    /// consumer can only reconstruct a real host's actual "Instance"/"Hosting" tabs from "General" if the
    /// nesting survives, not just the field set.
    /// </summary>
    [Fact]
    public async Task Manifest_CategoryWithNestedTabGroups_PreservesTheGroupingNotJustTheFields()
    {
        (HttpClient client, _, _) = await StartAsync(schemaJson: NestedGroupSchemaJson);

        HttpResponseMessage response = await client.GetAsync(
            new Uri("/config-ui/manifest", UriKind.Relative)
        );
        response.EnsureSuccessStatusCode();

        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement general = doc
            .RootElement.GetProperty("categories")
            .EnumerateArray()
            .Single(c => c.GetProperty("label").GetString() == "General");

        JsonElement categorization = Assert.Single(
            general.GetProperty("elements").EnumerateArray()
        );
        Assert.Equal("Categorization", categorization.GetProperty("type").GetString());

        JsonElement[] tabs = [.. categorization.GetProperty("elements").EnumerateArray()];
        Assert.Equal(2, tabs.Length);

        JsonElement instanceTab = tabs.Single(t =>
            t.GetProperty("label").GetString() == "Instance"
        );
        Assert.Equal("Category", instanceTab.GetProperty("type").GetString());
        JsonElement instanceField = Assert.Single(
            instanceTab.GetProperty("elements").EnumerateArray()
        );
        Assert.Equal("Control", instanceField.GetProperty("type").GetString());
        Assert.Equal(
            "instanceName",
            instanceField.GetProperty("field").GetProperty("key").GetString()
        );

        JsonElement hostingTab = tabs.Single(t => t.GetProperty("label").GetString() == "Hosting");
        JsonElement hostingField = Assert.Single(
            hostingTab.GetProperty("elements").EnumerateArray()
        );
        Assert.Equal(
            "listenUrls",
            hostingField.GetProperty("field").GetProperty("key").GetString()
        );
    }

    [Fact]
    public async Task Data_DocumentBackedCollection_ReturnsStoredEntries()
    {
        const string stored = """
            { "connectors": { "11111111-1111-1111-1111-111111111111": { "name": "Doc Row" } } }
            """;
        (HttpClient client, _, _) = await StartAsync(stored);

        HttpResponseMessage response = await client.GetAsync(
            new Uri("/config-ui/data/connectors", UriKind.Relative)
        );
        response.EnsureSuccessStatusCode();

        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement entry = Assert.Single(doc.RootElement.EnumerateArray());
        Assert.Equal("11111111-1111-1111-1111-111111111111", entry.GetProperty("key").GetString());
        Assert.Equal("Doc Row", entry.GetProperty("value").GetProperty("name").GetString());
    }

    [Fact]
    public async Task Data_LoaderBackedCollection_ReturnsLiveEntries_NotDocument()
    {
        const string stored = """
            { "connectors": { "11111111-1111-1111-1111-111111111111": { "name": "Doc Row" } } }
            """;
        (HttpClient client, _, IPluginRegistry registry) = await StartAsync(stored);
        registry.RegisterCollectionLoader(
            "connectors",
            (_, _) =>
                Task.FromResult<IReadOnlyList<ConfigDocument>>([
                    new ConfigDocument(new Dictionary<string, object?> { ["name"] = "Live Row" }),
                ])
        );

        HttpResponseMessage response = await client.GetAsync(
            new Uri("/config-ui/data/connectors", UriKind.Relative)
        );
        response.EnsureSuccessStatusCode();

        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement entry = Assert.Single(doc.RootElement.EnumerateArray());
        Assert.Equal("0", entry.GetProperty("key").GetString());
        Assert.Equal("Live Row", entry.GetProperty("value").GetProperty("name").GetString());
    }

    [Fact]
    public async Task Data_UnknownSection_ReturnsNotFound()
    {
        (HttpClient client, _, _) = await StartAsync();

        HttpResponseMessage response = await client.GetAsync(
            new Uri("/config-ui/data/does-not-exist", UriKind.Relative)
        );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Action_UnregisteredHandler_ReturnsNotFound()
    {
        (HttpClient client, _, _) = await StartAsync();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            new Uri("/config-ui/action/connector.test", UriKind.Relative),
            new { entryKey = "0" }
        );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Action_RequiresEntryWithoutEntryKey_ReturnsBadRequest()
    {
        (HttpClient client, _, IPluginRegistry registry) = await StartAsync();
        registry.RegisterAction("connector.test", _ => Task.CompletedTask);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            new Uri("/config-ui/action/connector.test", UriKind.Relative),
            new { }
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Action_RequiresEntry_AgainstLoaderBackedRow_ReadsItsFieldValues()
    {
        (HttpClient client, _, IPluginRegistry registry) = await StartAsync();
        registry.RegisterCollectionLoader(
            "connectors",
            (_, _) =>
                Task.FromResult<IReadOnlyList<ConfigDocument>>([
                    new ConfigDocument(new Dictionary<string, object?> { ["name"] = "Row X" }),
                ])
        );

        string? seenName = null;
        registry.RegisterAction(
            "connector.test",
            context =>
            {
                seenName = context[$"{context.CurrentFieldKey}/name"];
                return context.ShowToastAsync("done", ToastSeverity.Success);
            }
        );

        HttpResponseMessage response = await client.PostAsJsonAsync(
            new Uri("/config-ui/action/connector.test", UriKind.Relative),
            new { entryKey = "0" }
        );
        response.EnsureSuccessStatusCode();

        Assert.Equal("Row X", seenName);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("done", doc.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Action_GlobalAction_Succeeds()
    {
        (HttpClient client, _, IPluginRegistry registry) = await StartAsync();
        bool invoked = false;
        registry.RegisterAction(
            "global.ping",
            _ =>
            {
                invoked = true;
                return Task.CompletedTask;
            }
        );

        HttpResponseMessage response = await client.PostAsJsonAsync(
            new Uri("/config-ui/action/global.ping", UriKind.Relative),
            new { }
        );
        response.EnsureSuccessStatusCode();

        Assert.True(invoked);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }
}
