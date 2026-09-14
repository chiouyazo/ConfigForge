using ConfigForge.Core.Plugins;
using ConfigForge.Plugin.CapabilityWidgetFixture;
using Xunit;

namespace ConfigForge.Core.Tests;

/// <summary>
/// <see cref="CapabilityLoader"/>'s widget discovery: the same never-instantiate guarantee
/// <see cref="CapabilityLoaderTests"/> proves for <c>[CapabilityContract]</c> interfaces also
/// applies to <c>[CapabilityWidget]</c> component classes.
/// </summary>
public sealed class CapabilityWidgetDiscoveryTests
{
    private static string FixtureAssemblyPath => typeof(IWidgetFixtureCapability).Assembly.Location;

    [Fact]
    public void Load_DiscoversWidgetsByAttributeId_WithoutEverInstantiatingTheRealCapability()
    {
        int instantiationsBefore = WidgetFixtureCapability.InstantiationCount;
        int invocationsBefore = WidgetFixtureCapability.InvocationCount;

        using CapabilityLoader loader = new();
        CapabilityLoadResult result = loader.Load(FixtureAssemblyPath);

        Assert.Equal(CapabilityLoadStatus.Loaded, result.Status);
        Assert.True(result.Widgets.ContainsKey("fixture.logviewer"));
        Assert.Equal("FixtureLogViewerWidget", result.Widgets["fixture.logviewer"].Name);
        Assert.True(loader.AvailableWidgets.ContainsKey("fixture.logviewer"));

        Assert.Equal(instantiationsBefore, WidgetFixtureCapability.InstantiationCount);
        Assert.Equal(invocationsBefore, WidgetFixtureCapability.InvocationCount);
    }

    [Fact]
    public void Load_WhenAssemblyFileIsMissing_KeepsPreviouslyLoadedWidgets()
    {
        using CapabilityLoader loader = new();
        loader.Load(FixtureAssemblyPath);
        Assert.Single(loader.AvailableWidgets);

        string missingPath = Path.Combine(
            Path.GetTempPath(),
            $"cf-capability-widget-missing-{Guid.NewGuid():N}.dll"
        );

        CapabilityLoadResult missing = loader.Load(missingPath);

        Assert.Equal(CapabilityLoadStatus.AssemblyNotFound, missing.Status);
        Assert.Single(missing.Widgets);
        Assert.Single(loader.AvailableWidgets);
    }
}
