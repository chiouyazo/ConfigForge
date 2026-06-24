using ConfigForge.Abstractions;

namespace ConfigForge.AspNet;

/// <summary>
/// An <see cref="IThemeProvider"/> that returns a neutral default theme. Used when
/// the host application does not supply its own theme and no <c>theme.json</c>
/// override is present.
/// </summary>
public sealed class DefaultThemeProvider : IThemeProvider
{
    /// <summary>Returns a neutral <see cref="ThemeDefinition"/> using the built-in defaults.</summary>
    /// <returns>A new <see cref="ThemeDefinition"/> instance.</returns>
    public ThemeDefinition GetTheme() => new();
}
