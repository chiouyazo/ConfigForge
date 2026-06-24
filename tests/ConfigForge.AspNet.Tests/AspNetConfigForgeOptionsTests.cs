using ConfigForge.Abstractions;
using ConfigForge.AspNet;
using Xunit;

namespace ConfigForge.AspNet.Tests;

public sealed class AspNetConfigForgeOptionsTests
{
    [Fact]
    public void Defaults_MatchSpecification()
    {
        var options = new AspNetConfigForgeOptions();

        Assert.Equal("/config-ui", options.PathPrefix);
        Assert.Equal("plugins", options.PluginDirectory);
        Assert.Equal("schemas", options.SchemaDirectory);
        Assert.Equal(ConfigForgeMode.Open, options.Mode);
        Assert.Equal("Configuration", options.ApplicationTitle);
        Assert.NotNull(options.ThemeProvider);
        Assert.Null(options.OnSave);
        Assert.Null(options.OnLoad);
    }
}
