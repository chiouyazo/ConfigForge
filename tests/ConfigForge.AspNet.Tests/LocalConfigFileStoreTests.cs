using ConfigForge.AspNet;
using Xunit;

namespace ConfigForge.AspNet.Tests;

public sealed class LocalConfigFileStoreTests
{
    [Fact]
    public async Task SaveAsync_WritesCurrentAndRotatesBackups()
    {
        string dir = Path.Combine(Path.GetTempPath(), "cf-store-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new LocalConfigFileStore(dir, keepBackups: 3);

            for (int i = 0; i < 5; i++)
            {
                await store.SaveAsync("infra", $"{{\"v\":{i}}}");
                await Task.Delay(10);
            }

            // The current document is the latest save.
            Assert.Equal("{\"v\":4}", await store.LoadAsync("infra"));

            // Backups are kept but never exceed the configured cap.
            string[] backups = Directory.GetFiles(Path.Combine(dir, "backups"), "infra.*.json");
            Assert.InRange(backups.Length, 1, 3);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LoadAsync_ReturnsNullWhenAbsent()
    {
        string dir = Path.Combine(Path.GetTempPath(), "cf-store-" + Guid.NewGuid().ToString("N"));
        var store = new LocalConfigFileStore(dir);
        Assert.Null(await store.LoadAsync("missing"));
    }

    [Fact]
    public async Task SaveAsync_KeepBackupsZero_WritesNoBackups()
    {
        string dir = Path.Combine(Path.GetTempPath(), "cf-store-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new LocalConfigFileStore(dir, keepBackups: 0);
            await store.SaveAsync("infra", "{}");

            Assert.True(File.Exists(Path.Combine(dir, "infra.json")));
            Assert.False(Directory.Exists(Path.Combine(dir, "backups")));
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }
}
