using Bunit;
using ConfigForge.Blazor.Components;
using ConfigForge.Blazor.Services;
using ConfigForge.Core;
using ConfigForge.Core.Documents;
using ConfigForge.Core.Plugins;
using ConfigForge.Core.Schema;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ConfigForge.Blazor.Tests;

/// <summary>
/// Reproduces the real plugin scenario: load the EXTERNAL Showcase plugin DLL via
/// PluginLoader (a collectible AssemblyLoadContext) and exercise its composite
/// WeeklyScheduleControl's day-toggle interaction through the shell, exactly how
/// the demo/standalone run it, unlike controls compiled into the test assembly.
/// </summary>
public sealed class PluginLoadControlTests : BunitContext
{
    public PluginLoadControlTests()
    {
        Services.AddLogging();
        Services.AddConfigForgeCore();
        Services.AddConfigForgeBlazor();
    }

    [Fact]
    public async Task ExternalPluginControl_DayToggle_TogglesAndPersists()
    {
        string? dll = FindShowcasePluginDll();
        if (dll is null)
        {
            // The Showcase plugin is an external project (consumes the published
            // package, outside the solution), so its DLL isn't built in CI. This
            // test only exercises the real plugin locally, where it has been built.
            return;
        }

        var registry = Services.GetRequiredService<PluginRegistry>();
        await new PluginLoader(registry).LoadAssemblyAsync(dll!);

        Assert.True(
            ((IPluginCatalog)registry).TryGetControl("schedule.weekly", out _),
            "schedule.weekly control was not registered by the loaded plugin."
        );

        IJsonFormsSchemaParser parser = Services.GetRequiredService<IJsonFormsSchemaParser>();
        IConfigDocumentGenerator generator =
            Services.GetRequiredService<IConfigDocumentGenerator>();
        ConfigSchema schema = parser.Parse(ScheduleSchema);
        var document = generator.GenerateExample(schema);

        IRenderedComponent<ConfigForgeShell> cut = Render<ConfigForgeShell>(parameters =>
            parameters.Add(p => p.Schema, schema).Add(p => p.Document, document)
        );

        int before = cut.FindAll("button.cfs-day.cfs-day-on").Count;

        var tue = cut.FindAll("button.cfs-day")
            .First(b => string.Equals(b.TextContent.Trim(), "Tue", StringComparison.Ordinal));
        await tue.ClickAsync(new MouseEventArgs());

        int after = cut.FindAll("button.cfs-day.cfs-day-on").Count;
        Assert.Equal(before + 1, after);
    }

    private const string ScheduleSchema = """
        {
          "schema": {
            "type": "object",
            "properties": {
              "win": {
                "type": "array",
                "title": "Windows",
                "default": [ { "label": "A", "enabled": true, "days": ["Mon"] } ]
              }
            }
          },
          "uiSchema": {
            "type": "Categorization",
            "elements": [
              { "type": "Category", "label": "Sched", "elements": [ { "type": "Control", "scope": "#/properties/win" } ] }
            ]
          },
          "x-cf": { "id": "s", "name": "S", "controls": { "win": { "type": "schedule.weekly" } } }
        }
        """;

    private static string? FindShowcasePluginDll()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ConfigForge.slnx")))
        {
            dir = dir.Parent;
        }

        if (dir is null)
        {
            return null;
        }

        string pluginBin = Path.Combine(
            dir.FullName,
            "plugins",
            "ConfigForge.Plugin.Showcase",
            "bin"
        );
        if (!Directory.Exists(pluginBin))
        {
            return null;
        }

        return Directory
            .EnumerateFiles(
                pluginBin,
                "ConfigForge.Plugin.Showcase.dll",
                SearchOption.AllDirectories
            )
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }
}
