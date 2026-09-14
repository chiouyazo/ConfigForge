using System.Net.Http.Json;
using ConfigForge.Abstractions;
using ConfigForge.AspNet.Endpoints;
using ConfigForge.AspNet.RemoteInstances;
using ConfigForge.Core.Plugins;
using ConfigForge.Core.Schema;
using ConfigForge.Plugin.CapabilityWidgetFixture;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ConfigForge.AspNet.Tests;

/// <summary>
/// End-to-end, real-process proof that a mirrored capability widget field resolves to a
/// registration whose proxy calls back to the right originating instance: two real Kestrel
/// "product" instances, each serving its own capability action endpoint with its own distinct
/// data and each pointed at by its own <see cref="RemoteInstanceOptions.CapabilityAssemblyPaths"/>
/// deployment, polled by one Hub-like host. No Blazor rendering here (see
/// <c>ConfigForge.Blazor.Tests.CapabilityWidgetHostTests</c> for the per-render DI isolation
/// proof); this test is the network/wiring layer underneath it.
/// </summary>
public sealed class RemoteInstancesCapabilityWidgetTests : IAsyncLifetime
{
    private const string LogsCategorySchema = """
        {
          "schema": { "type": "object", "properties": { "entries": { "type": "string", "title": "Entries" } } },
          "uiSchema": {
            "type": "Categorization",
            "elements": [ { "type": "Category", "label": "Logs", "elements": [ { "type": "Control", "scope": "#/properties/entries" } ] } ]
          },
          "x-cf": { "id": "product", "controls": { "entries": { "type": "fixture.logviewer" } } }
        }
        """;

    private WebApplication? _instanceA;
    private WebApplication? _instanceB;
    private WebApplication? _hub;

    public async Task InitializeAsync()
    {
        _instanceA = await StartProductInstanceAsync(["A-entry-1", "A-entry-2"]);
        _instanceB = await StartProductInstanceAsync(["B-entry-1"]);
        _hub = await StartHubAsync(BaseUrlOf(_instanceA), BaseUrlOf(_instanceB));
    }

    public async Task DisposeAsync()
    {
        if (_hub is not null)
        {
            await _hub.DisposeAsync();
        }
        if (_instanceA is not null)
        {
            await _instanceA.DisposeAsync();
        }
        if (_instanceB is not null)
        {
            await _instanceB.DisposeAsync();
        }
    }

    /// <summary>
    /// A real "product" instance: ConfigForge with a "Logs" category whose one field is the
    /// fixture widget's control type, plus the product's own literal-path mapping for the
    /// capability action - more specific than ConfigForge's own parameterized
    /// <c>/action/{actionId}</c> route, so it wins routing precedence with no ambiguity, exactly
    /// the "product's own composition-root responsibility" <c>docs/capability-loading.md</c>
    /// describes for serving a capability call.
    /// </summary>
    private static async Task<WebApplication> StartProductInstanceAsync(
        IReadOnlyList<string> ownEntries
    )
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddConfigForge(options =>
        {
            options.PathPrefix = "/config-ui";
            options.OnLoad = _ => Task.FromResult<string?>(null);
        });

        WebApplication app = builder.Build();
        app.UseRouting();

        var parser = app.Services.GetRequiredService<IJsonFormsSchemaParser>();
        ConfigSchema schema = parser.Parse(LogsCategorySchema);
        app.Services.GetRequiredService<IConfigForgeHostState>().UpsertSchema(schema);
        ((IEndpointRouteBuilder)app).MapDashboardEndpoints("/config-ui");

        string capabilityActionId =
            $"{typeof(IWidgetFixtureCapability).FullName}.{nameof(IWidgetFixtureCapability.GetEntriesAsync)}";
        app.MapPost(
            $"/config-ui/action/{capabilityActionId}",
            () =>
                Results.Ok(
                    new CapabilityCallResponse<IReadOnlyList<string>>(true, null, ownEntries)
                )
        );

        await app.StartAsync();
        return app;
    }

    private static async Task<WebApplication> StartHubAsync(string urlA, string urlB)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddConfigForge(options =>
        {
            options.PathPrefix = "/config-ui";
            options.OnLoad = _ => Task.FromResult<string?>(null);
            options.RemoteInstancePollIntervalSeconds = 1;
            options.RemoteInstances =
            [
                new RemoteInstanceOptions
                {
                    Name = "instanceA",
                    BaseUrl = new Uri(urlA),
                    CapabilityAssemblyPaths = [FixtureAssemblyPath],
                },
                new RemoteInstanceOptions
                {
                    Name = "instanceB",
                    BaseUrl = new Uri(urlB),
                    CapabilityAssemblyPaths = [FixtureAssemblyPath],
                },
            ];
        });

        WebApplication app = builder.Build();
        app.UseRouting();

        var parser = app.Services.GetRequiredService<IJsonFormsSchemaParser>();
        ConfigSchema localSchema = parser.Parse(
            """{ "schema": { "type": "object", "properties": {} }, "uiSchema": { "type": "Categorization", "elements": [] }, "x-cf": { "id": "hub" } }"""
        );
        app.Services.GetRequiredService<IConfigForgeHostState>().UpsertSchema(localSchema);
        ((IEndpointRouteBuilder)app).MapDashboardEndpoints("/config-ui");

        await app.StartAsync();
        return app;
    }

    private static string FixtureAssemblyPath => typeof(IWidgetFixtureCapability).Assembly.Location;

    private static string BaseUrlOf(WebApplication app) =>
        app
            .Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!
            .Addresses.First();

    [Fact]
    public async Task EachInstancesWidgetRegistration_CallsBackOnlyItsOwnInstance_NeverTheOther()
    {
        ICapabilityWidgetCatalog catalog =
            _hub!.Services.GetRequiredService<ICapabilityWidgetCatalog>();

        CapabilityWidgetRegistration registrationA = await WaitForRegistrationAsync(
            catalog,
            "instanceA::fixture.logviewer"
        );
        CapabilityWidgetRegistration registrationB = await WaitForRegistrationAsync(
            catalog,
            "instanceB::fixture.logviewer"
        );

        object proxyA = registrationA.Loader.CreateProxy(
            registrationA.CapabilityInterfaceType!,
            registrationA.HttpClient
        )!;
        object proxyB = registrationB.Loader.CreateProxy(
            registrationB.CapabilityInterfaceType!,
            registrationB.HttpClient
        )!;

        dynamic dynamicProxyA = proxyA;
        dynamic dynamicProxyB = proxyB;
        IReadOnlyList<string> entriesFromA = await dynamicProxyA.GetEntriesAsync(
            default(CancellationToken)
        );
        IReadOnlyList<string> entriesFromB = await dynamicProxyB.GetEntriesAsync(
            default(CancellationToken)
        );

        Assert.Equal(["A-entry-1", "A-entry-2"], entriesFromA);
        Assert.Equal(["B-entry-1"], entriesFromB);
    }

    private static async Task<CapabilityWidgetRegistration> WaitForRegistrationAsync(
        ICapabilityWidgetCatalog catalog,
        string controlType
    )
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            if (catalog.TryGetWidget(controlType, out CapabilityWidgetRegistration? registration))
            {
                return registration!;
            }

            await Task.Delay(100);
        }

        throw new InvalidOperationException($"'{controlType}' was never registered.");
    }
}
