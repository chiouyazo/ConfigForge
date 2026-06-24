using ConfigForge.Abstractions;
using ConfigForge.AspNet;
using Xunit;

namespace ConfigForge.AspNet.Tests;

public sealed class DefaultThemeProviderTests
{
    [Fact]
    public void GetTheme_ReturnsNonNullThemeWithSaneDefaults()
    {
        var provider = new DefaultThemeProvider();

        ThemeDefinition theme = provider.GetTheme();

        Assert.NotNull(theme);
        Assert.False(string.IsNullOrWhiteSpace(theme.PrimaryColor));
        Assert.False(string.IsNullOrWhiteSpace(theme.BackgroundColor));
        Assert.False(string.IsNullOrWhiteSpace(theme.SurfaceColor));
        Assert.False(string.IsNullOrWhiteSpace(theme.TextPrimaryColor));
        Assert.False(string.IsNullOrWhiteSpace(theme.FontFamily));
    }
}
