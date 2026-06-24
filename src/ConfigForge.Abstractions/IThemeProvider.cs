namespace ConfigForge.Abstractions;

/// <summary>
/// Supplies the visual theme applied to the entire ConfigForge UI.
/// </summary>
public interface IThemeProvider
{
    /// <summary>Returns the theme definition to apply.</summary>
    /// <returns>The theme definition.</returns>
    ThemeDefinition GetTheme();
}
