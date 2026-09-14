using System.Net;
using System.Net.Http.Json;
using Bunit;
using ConfigForge.Abstractions;
using ConfigForge.Blazor.Components.Fields;
using ConfigForge.Core.Plugins;
using ConfigForge.Plugin.CapabilityWidgetFixture;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ConfigForge.Blazor.Tests;

/// <summary>
/// Proves <see cref="CapabilityWidgetHost"/>'s per-render DI isolation actually isolates: two
/// widgets built from two different <see cref="CapabilityWidgetRegistration"/>s (as two connected
/// remote instances would produce) render concurrently on screen without ever showing the other's
/// data, because each render builds its own <see cref="HtmlRenderer"/> and its own tiny
/// <see cref="IServiceProvider"/> rather than sharing one ambient "current instance".
/// </summary>
public sealed class CapabilityWidgetHostTests : BunitContext
{
    private static string FixtureAssemblyPath => typeof(IWidgetFixtureCapability).Assembly.Location;

    public CapabilityWidgetHostTests() => Services.AddLogging();

    [Fact]
    public async Task TwoRegistrations_RenderConcurrently_WithNoCrossInstanceLeakage()
    {
        using CapabilityLoader loaderA = new();
        loaderA.Load(FixtureAssemblyPath);
        using CapabilityLoader loaderB = new();
        loaderB.Load(FixtureAssemblyPath);

        // Each instance gets its own loaded copy of the assembly (a separate collectible
        // AssemblyLoadContext each), so the capability Type from loaderA is not the same Type
        // object as loaderB's copy even though both were loaded from the same file on disk - a
        // registration must always pair a widget/capability Type with the loader that produced it.
        Type capabilityTypeA = loaderA.AvailableCapabilities.Single(t =>
            t.Name == nameof(IWidgetFixtureCapability)
        );
        Type widgetTypeA = loaderA.AvailableWidgets["fixture.logviewer"];
        Type capabilityTypeB = loaderB.AvailableCapabilities.Single(t =>
            t.Name == nameof(IWidgetFixtureCapability)
        );
        Type widgetTypeB = loaderB.AvailableWidgets["fixture.logviewer"];

        using StubHttpMessageHandler handlerA = new(["entry-from-instance-A"]);
        using HttpClient clientA = new(handlerA)
        {
            BaseAddress = new Uri("http://instance-a/config-ui/"),
        };
        using StubHttpMessageHandler handlerB = new(["entry-from-instance-B"]);
        using HttpClient clientB = new(handlerB)
        {
            BaseAddress = new Uri("http://instance-b/config-ui/"),
        };

        CapabilityWidgetRegistration registrationA = new(
            widgetTypeA,
            capabilityTypeA,
            loaderA,
            clientA
        );
        CapabilityWidgetRegistration registrationB = new(
            widgetTypeB,
            capabilityTypeB,
            loaderB,
            clientB
        );

        FakeCatalog catalogA = new(registrationA);
        FakeCatalog catalogB = new(registrationB);

        IRenderedComponent<CapabilityWidgetHost> cutA = Render<CapabilityWidgetHost>(p =>
            p.Add(x => x.Catalog, catalogA)
                .Add(x => x.ControlType, "instanceA::fixture.logviewer")
                .Add(x => x.Control, new ControlDescriptor())
                .Add(x => x.Document, new ConfigDocument())
        );
        IRenderedComponent<CapabilityWidgetHost> cutB = Render<CapabilityWidgetHost>(p =>
            p.Add(x => x.Catalog, catalogB)
                .Add(x => x.ControlType, "instanceB::fixture.logviewer")
                .Add(x => x.Control, new ControlDescriptor())
                .Add(x => x.Document, new ConfigDocument())
        );

        cutA.WaitForAssertion(
            () => Assert.Contains("entry-from-instance-A", cutA.Markup, StringComparison.Ordinal),
            TimeSpan.FromSeconds(10)
        );
        cutB.WaitForAssertion(
            () => Assert.Contains("entry-from-instance-B", cutB.Markup, StringComparison.Ordinal),
            TimeSpan.FromSeconds(10)
        );

        Assert.DoesNotContain("entry-from-instance-B", cutA.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("entry-from-instance-A", cutB.Markup, StringComparison.Ordinal);
        Assert.Equal(1, handlerA.RequestCount);
        Assert.Equal(1, handlerB.RequestCount);
    }

    private sealed class FakeCatalog(CapabilityWidgetRegistration registration)
        : ICapabilityWidgetCatalog
    {
        public bool TryGetWidget(
            string controlType,
            [System.Diagnostics.CodeAnalysis.NotNullWhen(true)]
                out CapabilityWidgetRegistration? found
        )
        {
            found = registration;
            return true;
        }
    }

    private sealed class StubHttpMessageHandler(IReadOnlyList<string> entries) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            RequestCount++;
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(
                    new CapabilityCallResponse<IReadOnlyList<string>>(true, null, entries)
                ),
            };
            return Task.FromResult(response);
        }
    }
}
