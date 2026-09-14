using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ConfigForge.Abstractions;
using ConfigForge.AspNet.Endpoints;
using ConfigForge.Core.Schema;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ConfigForge.AspNet.Tests;

/// <summary>
/// End-to-end tests of the headless <c>GET/POST {prefix}/document</c> endpoint pair
/// (<see cref="DashboardEndpoints.MapDashboardEndpoints"/>) over a real <see cref="TestServer"/>:
/// they must call the same <c>OnLoad</c>/<c>OnSave</c>/validation pipeline the interactive editor
/// uses, not a parallel reimplementation.
/// </summary>
public sealed class DocumentEndpointsTests : IAsyncDisposable
{
    private const string SchemaJson = """
        {
          "schema": {
            "type": "object",
            "properties": {
              "name": { "type": "string", "title": "Name" },
              "apiKey": { "type": "string", "title": "API Key", "x-control": "secret" },
              "endpointUrl": { "type": "string", "title": "Endpoint URL" }
            },
            "required": [ "name" ]
          },
          "uiSchema": {
            "type": "Categorization",
            "elements": [
              { "type": "Category", "label": "General", "elements": [
                { "type": "Control", "scope": "#/properties/name" },
                { "type": "Control", "scope": "#/properties/apiKey" },
                { "type": "Control", "scope": "#/properties/endpointUrl" }
              ] }
            ]
          },
          "x-cf": { "id": "dash", "name": "Dashboard Demo" }
        }
        """;

    private WebApplication? _app;

    private async Task<(HttpClient Client, List<(string SchemaId, string Json)> Saves)> StartAsync(
        string? storedDocumentJson = null,
        bool withSecretProtector = false
    )
    {
        List<(string SchemaId, string Json)> saves = [];

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        if (withSecretProtector)
        {
            builder.Services.AddSingleton<IConfigSecretProtector, FakeSecretProtector>();
        }

        string? stored = storedDocumentJson;
        builder.Services.AddConfigForge(options =>
        {
            options.PathPrefix = "/config-ui";
            options.OnLoad = _ => Task.FromResult(stored);
            options.OnSave = (schemaId, json) =>
            {
                stored = json;
                saves.Add((schemaId, json));
                return Task.CompletedTask;
            };
        });

        _app = builder.Build();
        _app.UseRouting();
        ((IEndpointRouteBuilder)_app).MapDashboardEndpoints("/config-ui");

        var parser = _app.Services.GetRequiredService<IJsonFormsSchemaParser>();
        ConfigSchema schema = parser.Parse(SchemaJson);
        var state = _app.Services.GetRequiredService<IConfigForgeHostState>();
        state.UpsertSchema(schema);

        await _app.StartAsync();

        return (_app.GetTestClient(), saves);
    }

    [Fact]
    public async Task GetDocument_ReturnsStoredDocumentAsIs()
    {
        const string stored = """{ "name": "Existing", "endpointUrl": "https://example.com" }""";
        (HttpClient client, _) = await StartAsync(stored);

        HttpResponseMessage response = await client.GetAsync(
            new Uri("/config-ui/document", UriKind.Relative)
        );
        response.EnsureSuccessStatusCode();

        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Existing", doc.RootElement.GetProperty("name").GetString());
        Assert.Equal("https://example.com", doc.RootElement.GetProperty("endpointUrl").GetString());
    }

    [Fact]
    public async Task GetDocument_NoOnLoadConfigured_ReturnsNotFound()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddConfigForge(options => options.PathPrefix = "/config-ui");
        _app = builder.Build();
        _app.UseRouting();
        ((IEndpointRouteBuilder)_app).MapDashboardEndpoints("/config-ui");
        var parser = _app.Services.GetRequiredService<IJsonFormsSchemaParser>();
        _app.Services.GetRequiredService<IConfigForgeHostState>()
            .UpsertSchema(parser.Parse(SchemaJson));
        await _app.StartAsync();

        HttpResponseMessage response = await _app.GetTestClient()
            .GetAsync(new Uri("/config-ui/document", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostDocument_Valid_InvokesRealOnSave_WithSerializedSchemaShapedPayload()
    {
        (HttpClient client, List<(string SchemaId, string Json)> saves) = await StartAsync(
            """{ "name": "Old" }"""
        );

        HttpResponseMessage response = await client.PostAsJsonAsync(
            new Uri("/config-ui/document", UriKind.Relative),
            JsonDocument
                .Parse("""{ "name": "New", "endpointUrl": "https://new.example.com" }""")
                .RootElement
        );
        response.EnsureSuccessStatusCode();

        (string schemaId, string json) = Assert.Single(saves);
        Assert.Equal("dash", schemaId);
        using JsonDocument saved = JsonDocument.Parse(json);
        Assert.Equal("New", saved.RootElement.GetProperty("name").GetString());
        Assert.Equal(
            "https://new.example.com",
            saved.RootElement.GetProperty("endpointUrl").GetString()
        );
    }

    [Fact]
    public async Task PostDocument_MissingRequiredField_ReturnsStructuredValidationError_AndDoesNotSave()
    {
        (HttpClient client, List<(string SchemaId, string Json)> saves) = await StartAsync(
            """{ "name": "Old" }"""
        );

        HttpResponseMessage response = await client.PostAsJsonAsync(
            new Uri("/config-ui/document", UriKind.Relative),
            JsonDocument.Parse("""{ "endpointUrl": "https://new.example.com" }""").RootElement
        );

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Contains(
            body.RootElement.GetProperty("missingRequiredKeys").EnumerateArray(),
            k => k.GetString() == "name"
        );
        Assert.Empty(saves);
    }

    [Fact]
    public async Task PostDocument_MalformedJson_ReturnsBadRequest_WithJsonError()
    {
        (HttpClient client, List<(string SchemaId, string Json)> saves) = await StartAsync();

        using StringContent content = new(
            "{ not json",
            System.Text.Encoding.UTF8,
            "application/json"
        );
        HttpResponseMessage response = await client.PostAsync(
            new Uri("/config-ui/document", UriKind.Relative),
            content
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.NotNull(body.RootElement.GetProperty("jsonError").GetString());
        Assert.Empty(saves);
    }

    [Fact]
    public async Task GetDocument_RedactsSecretFields_LikeTheInteractiveEditor()
    {
        const string stored = """{ "name": "Existing", "apiKey": "super-secret-value" }""";
        (HttpClient client, _) = await StartAsync(stored, withSecretProtector: true);

        HttpResponseMessage response = await client.GetAsync(
            new Uri("/config-ui/document", UriKind.Relative)
        );
        response.EnsureSuccessStatusCode();

        string body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("super-secret-value", body, StringComparison.Ordinal);
        using JsonDocument doc = JsonDocument.Parse(body);
        Assert.Equal(
            ConfigForgeSecret.StoredMarker,
            doc.RootElement.GetProperty("apiKey").GetString()
        );
    }

    [Fact]
    public async Task PostDocument_UnchangedStoredMarker_PreservesRealSecret_NotOverwritten()
    {
        FakeSecretProtector protectorForSeeding = new();
        string stored =
            $$"""{ "name": "Existing", "apiKey": "{{protectorForSeeding.Protect("super-secret-value")}}" }""";
        (HttpClient client, List<(string SchemaId, string Json)> saves) = await StartAsync(
            stored,
            withSecretProtector: true
        );

        HttpResponseMessage getResponse = await client.GetAsync(
            new Uri("/config-ui/document", UriKind.Relative)
        );
        using JsonDocument redacted = JsonDocument.Parse(
            await getResponse.Content.ReadAsStringAsync()
        );
        Assert.Equal(
            ConfigForgeSecret.StoredMarker,
            redacted.RootElement.GetProperty("apiKey").GetString()
        );

        HttpResponseMessage postResponse = await client.PostAsJsonAsync(
            new Uri("/config-ui/document", UriKind.Relative),
            JsonDocument
                .Parse(
                    $$"""{ "name": "Renamed", "apiKey": "{{ConfigForgeSecret.StoredMarker}}" }"""
                )
                .RootElement
        );
        postResponse.EnsureSuccessStatusCode();

        (_, string savedJson) = Assert.Single(saves);
        using JsonDocument savedDoc = JsonDocument.Parse(savedJson);
        string savedSecret = savedDoc.RootElement.GetProperty("apiKey").GetString()!;
        Assert.NotEqual(ConfigForgeSecret.StoredMarker, savedSecret);
        Assert.NotEqual("super-secret-value", savedSecret);

        IConfigSecretProtector protector =
            _app!.Services.GetRequiredService<IConfigSecretProtector>();
        Assert.Equal("super-secret-value", protector.Unprotect(savedSecret));
    }

    [Fact]
    public async Task PostDocument_NoOnSaveConfigured_ReturnsNotFound()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddConfigForge(options => options.PathPrefix = "/config-ui");
        _app = builder.Build();
        _app.UseRouting();
        ((IEndpointRouteBuilder)_app).MapDashboardEndpoints("/config-ui");
        var parser = _app.Services.GetRequiredService<IJsonFormsSchemaParser>();
        _app.Services.GetRequiredService<IConfigForgeHostState>()
            .UpsertSchema(parser.Parse(SchemaJson));
        await _app.StartAsync();

        HttpResponseMessage response = await _app.GetTestClient()
            .PostAsJsonAsync(
                new Uri("/config-ui/document", UriKind.Relative),
                JsonDocument.Parse("""{ "name": "X" }""").RootElement
            );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    // CA1812: instantiated by the DI container via AddSingleton<IConfigSecretProtector, ...>,
    // which the analyzer cannot see.
#pragma warning disable CA1812
    private sealed class FakeSecretProtector : IConfigSecretProtector
    {
        private const string Marker = "enc:";

        public string Protect(string plaintext) => Marker + plaintext;

        public string Unprotect(string protectedValue) => protectedValue[Marker.Length..];

        public bool IsProtected(string value) =>
            value is not null && value.StartsWith(Marker, StringComparison.Ordinal);
    }
#pragma warning restore CA1812
}
