using ConfigForge.Abstractions;

namespace ConfigForge.Sample.Web;

/// <summary>Demo theme: a clean system font stack with no external font dependency.</summary>
public sealed class DemoThemeProvider : IThemeProvider
{
    public ThemeDefinition GetTheme() =>
        new()
        {
            FontFamily = "system-ui, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif",
        };
}
