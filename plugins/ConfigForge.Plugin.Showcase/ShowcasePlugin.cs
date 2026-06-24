using ConfigForge.Abstractions;

namespace ConfigForge.Plugin.Showcase;

/// <summary>
/// External ConfigForge plugin shipped as a standalone NuGet-consuming DLL and
/// loaded into a host at runtime via <c>IPluginLoader.LoadFromDirectoryAsync</c>.
///
/// It registers two custom Blazor controls and one action:
/// <list type="bullet">
///   <item><c>cat.image</c> → <see cref="CatImageControl"/> (display-only).</item>
///   <item><c>schedule.weekly</c> → <see cref="WeeklyScheduleControl"/>
///   (a complex composite editor for an array of week-schedule objects).</item>
///   <item><c>cat.next</c> action → swaps in a fresh cataas.com cat URL.</item>
/// </list>
/// The enable-all / disable-all behaviour lives as BUTTONS inside
/// <see cref="WeeklyScheduleControl"/>, not as schema actions.
/// </summary>
public sealed class ShowcasePlugin : IPlugin
{
    /// <summary>The JSON Schema property key the cat image binds to.</summary>
    public const string CatFieldKey = "cat_image";

    /// <inheritdoc />
    public string Id => "ConfigForge.Plugin.Showcase";

    /// <inheritdoc />
    public string DisplayName => "Showcase Plugin (external)";

    /// <inheritdoc />
    public void Register(IPluginRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        registry.RegisterControl("cat.image", typeof(CatImageControl));
        registry.RegisterControl("schedule.weekly", typeof(WeeklyScheduleControl));

        registry.RegisterAction(
            "cat.next",
            ctx =>
                ctx.SetFieldValueAsync(
                    CatFieldKey,
                    $"https://cataas.com/cat?ts={DateTime.UtcNow.Ticks}"
                )
        );
    }
}
