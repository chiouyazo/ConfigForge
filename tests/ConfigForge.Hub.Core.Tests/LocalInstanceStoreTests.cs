using ConfigForge.Hub.Core.Instances;
using Xunit;

namespace ConfigForge.Hub.Core.Tests;

public sealed class LocalInstanceStoreTests : IDisposable
{
    private readonly string _tempFile = Path.Combine(
        Path.GetTempPath(),
        $"cf-hub-instances-{Guid.NewGuid():N}.json"
    );

    [Fact]
    public async Task LoadAsync_FileMissing_ReturnsEmpty()
    {
        var store = new LocalInstanceStore(_tempFile);

        Dictionary<string, StoredInstance> result = await store.LoadAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task SaveThenLoad_RoundTripsInstances()
    {
        var store = new LocalInstanceStore(_tempFile);
        var instances = new Dictionary<string, StoredInstance>(StringComparer.Ordinal)
        {
            ["id1"] = new StoredInstance
            {
                Name = "instanceA",
                BaseUrl = "https://a.example.com",
                Username = "u",
                Password = "p",
            },
        };

        await store.SaveAsync(instances);
        Dictionary<string, StoredInstance> loaded = await store.LoadAsync();

        StoredInstance entry = Assert.Single(loaded).Value;
        Assert.Equal("instanceA", entry.Name);
        Assert.Equal("https://a.example.com", entry.BaseUrl);
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
        {
            File.Delete(_tempFile);
        }
    }
}
