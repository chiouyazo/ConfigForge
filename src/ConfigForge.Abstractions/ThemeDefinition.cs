namespace ConfigForge.Abstractions;

/// <summary>
/// Defines the visual appearance of the ConfigForge UI.
/// All color values must be valid CSS color strings.
/// </summary>
public sealed class ThemeDefinition
{
    /// <summary>Primary accent color used for buttons, active states, and highlights.</summary>
    public string PrimaryColor { get; init; } = "#005fa3";

    /// <summary>Background color for cards, panels, and elevated surfaces.</summary>
    public string SurfaceColor { get; init; } = "#ffffff";

    /// <summary>Page-level background color.</summary>
    public string BackgroundColor { get; init; } = "#f4f5f7";

    /// <summary>Primary text color.</summary>
    public string TextPrimaryColor { get; init; } = "#111827";

    /// <summary>Secondary text color used for labels, hints, and descriptions.</summary>
    public string TextSecondaryColor { get; init; } = "#6b7280";

    /// <summary>Color for error states and validation failures.</summary>
    public string DangerColor { get; init; } = "#dc2626";

    /// <summary>Color for success states and confirmations.</summary>
    public string SuccessColor { get; init; } = "#16a34a";

    /// <summary>Color for warnings and non-blocking validation messages.</summary>
    public string WarningColor { get; init; } = "#d97706";

    /// <summary>CSS font-family string applied to the entire UI.</summary>
    public string FontFamily { get; init; } = "'Inter', sans-serif";

    /// <summary>CSS border-radius applied to controls and panels.</summary>
    public string BorderRadius { get; init; } = "6px";

    /// <summary>Optional logo. Null renders no logo.</summary>
    public LogoDefinition? Logo { get; init; }
}
