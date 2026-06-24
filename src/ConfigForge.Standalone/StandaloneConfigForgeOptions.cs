namespace ConfigForge.Standalone;

/// <summary>
/// Host options for the standalone WPF ConfigForge application. These drive the
/// main window's title and initial dimensions and identify the embedded schema
/// the host edits against.
/// </summary>
public sealed class StandaloneConfigForgeOptions
{
    /// <summary>The window title shown in the title bar. Defaults to "Configuration".</summary>
    public string ApplicationTitle { get; set; } = "Configuration";

    /// <summary>The window's initial width, in device-independent pixels.</summary>
    public double InitialWidth { get; set; } = 1100;

    /// <summary>The window's initial height, in device-independent pixels.</summary>
    public double InitialHeight { get; set; } = 800;

    /// <summary>
    /// The logical name of the embedded schema resource to load. The build emits
    /// embedded schemas under logical names rooted at <c>schemas/</c>; an empty
    /// value means "load the first/default embedded schema".
    /// </summary>
    public string EmbeddedSchemaId { get; set; } = string.Empty;
}
