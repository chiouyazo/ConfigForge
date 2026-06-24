using ConfigForge.Standalone;
using Xunit;

namespace ConfigForge.Standalone.Tests;

public sealed class EmbeddedOptionsLoaderTests
{
    [Fact]
    public void Load_ReturnsNonNullOptions()
    {
        StandaloneConfigForgeOptions options = EmbeddedOptionsLoader.Load();

        Assert.NotNull(options);
    }

    [Fact]
    public void Load_ReturnsOptionsWithSaneDefaults()
    {
        StandaloneConfigForgeOptions options = EmbeddedOptionsLoader.Load();

        Assert.False(string.IsNullOrWhiteSpace(options.ApplicationTitle));
        Assert.True(options.InitialWidth > 0, "InitialWidth should be positive.");
        Assert.True(options.InitialHeight > 0, "InitialHeight should be positive.");
        Assert.NotNull(options.EmbeddedSchemaId);
    }

    [Fact]
    public void Options_DefaultValues_MatchSpecification()
    {
        var options = new StandaloneConfigForgeOptions();

        Assert.Equal("Configuration", options.ApplicationTitle);
        Assert.Equal(1100, options.InitialWidth);
        Assert.Equal(800, options.InitialHeight);
        Assert.Equal(string.Empty, options.EmbeddedSchemaId);
    }
}
