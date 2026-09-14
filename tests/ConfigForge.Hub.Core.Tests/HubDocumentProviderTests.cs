using ConfigForge.AspNet;
using ConfigForge.Hub.Core.Instances;
using ConfigForge.Hub.Web.Instances;
using Xunit;

namespace ConfigForge.Hub.Core.Tests;

public sealed class HubDocumentProviderTests : IDisposable
{
    private readonly string _tempFile = Path.Combine(
        Path.GetTempPath(),
        $"cf-hub-doc-{Guid.NewGuid():N}.json"
    );

    [Fact]
    public async Task LoadAsync_UnknownSchemaId_ReturnsNull()
    {
        var provider = new HubDocumentProvider(
            new LocalInstanceStore(_tempFile),
            new AspNetConfigForgeOptions()
        );

        string? result = await provider.LoadAsync("not-hub");

        Assert.Null(result);
    }

    [Fact]
    public async Task SaveThenLoad_RoundTripsDocument_AndUpdatesRemoteInstancesLive()
    {
        var options = new AspNetConfigForgeOptions();
        var provider = new HubDocumentProvider(new LocalInstanceStore(_tempFile), options);

        const string document = """
            {
              "instances": {
                "id1": { "name": "instanceA", "baseUrl": "https://a.example.com", "pathPrefix": "/config-ui", "username": "u", "password": "p" }
              }
            }
            """;

        await provider.SaveAsync("hub", document);

        Assert.Single(options.RemoteInstances);
        Assert.Equal("instanceA", options.RemoteInstances[0].Name);
        Assert.Equal(new Uri("https://a.example.com"), options.RemoteInstances[0].BaseUrl);

        string? loaded = await provider.LoadAsync("hub");
        Assert.Contains("instanceA", loaded, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveAsync_UnknownSchemaId_DoesNotTouchStoreOrOptions()
    {
        var options = new AspNetConfigForgeOptions();
        var localStore = new LocalInstanceStore(_tempFile);
        var provider = new HubDocumentProvider(localStore, options);

        await provider.SaveAsync("not-hub", """{ "instances": {} }""");

        Assert.False(File.Exists(_tempFile));
        Assert.Empty(options.RemoteInstances);
    }

    [Fact]
    public async Task SaveAsync_TwoInstancesSameNameDifferentCase_ThrowsAndDoesNotPersist()
    {
        var options = new AspNetConfigForgeOptions();
        var provider = new HubDocumentProvider(new LocalInstanceStore(_tempFile), options);

        const string document = """
            {
              "instances": {
                "id1": { "name": "instanceA", "baseUrl": "https://a.example.com", "pathPrefix": "/config-ui", "username": "", "password": "" },
                "id2": { "name": "INSTANCEA", "baseUrl": "https://b.example.com", "pathPrefix": "/config-ui", "username": "", "password": "" }
              }
            }
            """;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.SaveAsync("hub", document)
        );

        Assert.False(File.Exists(_tempFile));
        Assert.Empty(options.RemoteInstances);
    }

    [Fact]
    public async Task SaveAsync_TwoInstancesDifferentNamesSameBaseUrl_IsAllowed()
    {
        var options = new AspNetConfigForgeOptions();
        var provider = new HubDocumentProvider(new LocalInstanceStore(_tempFile), options);

        const string document = """
            {
              "instances": {
                "id1": { "name": "staging", "baseUrl": "https://shared.example.com", "pathPrefix": "/config-ui", "username": "", "password": "" },
                "id2": { "name": "prod", "baseUrl": "https://shared.example.com", "pathPrefix": "/config-ui", "username": "", "password": "" }
              }
            }
            """;

        await provider.SaveAsync("hub", document);

        Assert.Equal(2, options.RemoteInstances.Count);
        Assert.Equal(
            ["prod", "staging"],
            options.RemoteInstances.Select(i => i.Name).OrderBy(n => n, StringComparer.Ordinal)
        );
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
        {
            File.Delete(_tempFile);
        }
    }
}
