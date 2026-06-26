using System.Net;
using ConfigForge.AspNet;
using ConfigForge.Core.Schema;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ConfigForge.AspNet.Tests;

public sealed class RemoteSchemaPollerTests
{
    private const string SchemaJson = """
        {
          "schema": { "type": "object", "properties": { "A": { "type": "string" } } },
          "x-cf": { "id": "remote-x", "name": "Remote X" }
        }
        """;

    [Fact]
    public async Task StartAsync_LoadsSchemasFromManifestOverHttp()
    {
        var responses = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["http://test.local/schemas.json"] = "[\"remote.json\"]",
            ["http://test.local/remote.json"] = SchemaJson,
        };

        var options = new AspNetConfigForgeOptions
        {
            SchemaUrl = "http://test.local/schemas.json",
            SchemaRefreshSeconds = 3600,
        };
        var state = new ConfigForgeHostState(options);

        using var handler = new StubHandler(responses);
        using var poller = new RemoteSchemaPoller(
            state,
            new JsonFormsSchemaParser(),
            new StubHttpClientFactory(handler),
            NullLogger<RemoteSchemaPoller>.Instance
        );

        await poller.StartAsync(CancellationToken.None);
        try
        {
            Assert.True(state.Schemas.ContainsKey("remote-x"));
            Assert.Equal("Remote X", state.Schemas["remote-x"].Name);
        }
        finally
        {
            await poller.StopAsync(CancellationToken.None);
        }
    }

    private sealed class StubHandler(IReadOnlyDictionary<string, string> responses)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            string url = request.RequestUri!.ToString();
            HttpResponseMessage response = responses.TryGetValue(url, out string? body)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) }
                : new HttpResponseMessage(HttpStatusCode.NotFound);
            return Task.FromResult(response);
        }
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}
