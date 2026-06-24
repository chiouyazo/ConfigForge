using ConfigForge.Abstractions;

namespace ConfigForge.Blazor.Services;

/// <summary>
/// Fallback <see cref="IThemeProvider"/> returning the built-in neutral theme.
/// Registered via <c>TryAddSingleton</c> in <see cref="ServiceCollectionExtensions"/>
/// so a host (e.g. the ASP.NET middleware) can override it with its own theme.
/// </summary>
public sealed class DefaultThemeProvider : IThemeProvider
{
    /// <inheritdoc />
    public ThemeDefinition GetTheme() => new();
}
