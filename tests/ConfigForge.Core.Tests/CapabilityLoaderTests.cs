using System.Net;
using System.Net.Http.Json;
using ConfigForge.Abstractions;
using ConfigForge.Core.Plugins;
using ConfigForge.Plugin.CapabilityFixture;
using Xunit;

namespace ConfigForge.Core.Tests;

/// <remarks>
/// The fixture plugin is a normal <c>ProjectReference</c> of this test project, so
/// <c>typeof(IFixtureCapability)</c> here names the copy already loaded into THIS process's
/// default load context, not the separate copy <see cref="CapabilityLoader"/> loads into its own
/// isolated context from the built DLL on disk. That is exactly the isolation the loader exists
/// to provide, so tests that need the loader's own <see cref="Type"/> (to build or call a proxy)
/// fetch it from <see cref="ICapabilityLoader.AvailableCapabilities"/> by name and dispatch
/// through <c>dynamic</c> rather than casting to the compile-time interface.
/// </remarks>
public sealed class CapabilityLoaderTests
{
    private static string FixtureAssemblyPath => typeof(IFixtureCapability).Assembly.Location;

    [Fact]
    public void Load_DiscoversOnlyMarkedInterfaces_AndNeverExecutesTheRealImplementation()
    {
        int instantiationsBefore = FixtureCapability.InstantiationCount;
        int invocationsBefore = FixtureCapability.InvocationCount;

        using var loader = new CapabilityLoader();
        CapabilityLoadResult result = loader.Load(FixtureAssemblyPath);

        Assert.Equal(CapabilityLoadStatus.Loaded, result.Status);
        Assert.Contains(result.Capabilities, t => t.Name == nameof(IFixtureCapability));
        Assert.DoesNotContain(result.Capabilities, t => t.Name == nameof(IUnmarkedCapability));
        Assert.Contains(loader.AvailableCapabilities, t => t.Name == nameof(IFixtureCapability));

        Assert.Equal(instantiationsBefore, FixtureCapability.InstantiationCount);
        Assert.Equal(invocationsBefore, FixtureCapability.InvocationCount);
    }

    [Fact]
    public void CreateProxy_ForAnUnmarkedOrUnknownInterface_ReturnsNullInsteadOfThrowing()
    {
        using var loader = new CapabilityLoader();
        loader.Load(FixtureAssemblyPath);
        using var client = new HttpClient { BaseAddress = new Uri("http://localhost/config-ui/") };

        // Neither of these is ever in AvailableCapabilities: IUnmarkedCapability lacks
        // [CapabilityContract], and IDisposable isn't from the loaded assembly at all.
        Assert.Null(loader.CreateProxy(typeof(IUnmarkedCapability), client));
        Assert.Null(loader.CreateProxy(typeof(IDisposable), client));
        Assert.Null(loader.CreateProxy<IUnmarkedCapability>(client));
    }

    [Fact]
    public void Load_WhenAssemblyFileIsMissing_ReturnsCleanFailure_WithoutCrashingOrResettingState()
    {
        using var loader = new CapabilityLoader();
        CapabilityLoadResult first = loader.Load(FixtureAssemblyPath);
        Assert.Equal(CapabilityLoadStatus.Loaded, first.Status);

        string missingPath = Path.Combine(
            Path.GetTempPath(),
            $"cf-capability-missing-{Guid.NewGuid():N}.dll"
        );

        CapabilityLoadResult missing = loader.Load(missingPath);

        Assert.Equal(CapabilityLoadStatus.AssemblyNotFound, missing.Status);
        Assert.Contains(missing.Capabilities, t => t.Name == nameof(IFixtureCapability));
        Assert.Contains(loader.AvailableCapabilities, t => t.Name == nameof(IFixtureCapability));
    }

    [Fact]
    public void AssemblyDisappearsThenReappears_RecoversWithoutPersistedStateMutation()
    {
        string tempDir = Directory.CreateTempSubdirectory("cf-capability-fixture-").FullName;
        string tempAssembly = Path.Combine(tempDir, Path.GetFileName(FixtureAssemblyPath));

        try
        {
            File.Copy(FixtureAssemblyPath, tempAssembly);

            using var loader = new CapabilityLoader();
            CapabilityLoadResult loaded = loader.Load(tempAssembly);
            Assert.Equal(CapabilityLoadStatus.Loaded, loaded.Status);
            Assert.Single(loader.AvailableCapabilities);

            // Loaded from an in-memory copy, so this succeeds even though the assembly is loaded.
            File.Delete(tempAssembly);

            CapabilityLoadResult afterDelete = loader.Load(tempAssembly);
            Assert.Equal(CapabilityLoadStatus.AssemblyNotFound, afterDelete.Status);
            Assert.Single(loader.AvailableCapabilities);

            File.Copy(FixtureAssemblyPath, tempAssembly);
            CapabilityLoadResult afterRecreate = loader.Load(tempAssembly);

            Assert.Equal(CapabilityLoadStatus.Loaded, afterRecreate.Status);
            Assert.Single(loader.AvailableCapabilities);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task CreateProxy_RoundTripsACallThroughTheDocumentedActionWireShape()
    {
        using var loader = new CapabilityLoader();
        loader.Load(FixtureAssemblyPath);

        using var handler = new StubHttpMessageHandler(
            (request, cancellationToken) =>
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal(
                    "http://localhost/config-ui/action/ConfigForge.Plugin.CapabilityFixture.IFixtureCapability.EchoAsync",
                    request.RequestUri!.ToString()
                );

                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(
                        new CapabilityCallResponse<string>(true, null, "hello")
                    ),
                };
                return Task.FromResult(response);
            }
        );

        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost/config-ui/"),
        };

        Type capabilityType = loader.AvailableCapabilities.Single(t =>
            t.Name == nameof(IFixtureCapability)
        );
        dynamic proxy = loader.CreateProxy(capabilityType, client)!;

        string result = await proxy.EchoAsync("hello", default(CancellationToken));

        Assert.Equal("hello", result);
        Assert.Equal(0, FixtureCapability.InvocationCount);
    }

    [Fact]
    public async Task CreateProxy_WhenTheRemoteActionReportsFailure_ThrowsACatchableException()
    {
        using var loader = new CapabilityLoader();
        loader.Load(FixtureAssemblyPath);

        using var handler = new StubHttpMessageHandler(
            (_, _) =>
                Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = JsonContent.Create(
                            new CapabilityCallResponse<string>(false, "boom", null)
                        ),
                    }
                )
        );

        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost/config-ui/"),
        };

        Type capabilityType = loader.AvailableCapabilities.Single(t =>
            t.Name == nameof(IFixtureCapability)
        );
        dynamic proxy = loader.CreateProxy(capabilityType, client)!;

        CapabilityRemoteCallException exception =
            await Assert.ThrowsAsync<CapabilityRemoteCallException>(async () =>
                await proxy.EchoAsync("hello", default(CancellationToken))
            );
        Assert.Equal("boom", exception.Message);
    }

    [Fact]
    public async Task ProxyBuiltBeforeReload_KeepsWorkingWhileTheOldContextIsUnloading()
    {
        string tempDir = Directory.CreateTempSubdirectory("cf-capability-unload-").FullName;
        string tempAssembly = Path.Combine(tempDir, Path.GetFileName(FixtureAssemblyPath));

        try
        {
            File.Copy(FixtureAssemblyPath, tempAssembly);

            using var loader = new CapabilityLoader();
            loader.Load(tempAssembly);

            using var handler = new StubHttpMessageHandler(
                (_, _) =>
                    Task.FromResult(
                        new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = JsonContent.Create(
                                new CapabilityCallResponse<string>(true, null, "still alive")
                            ),
                        }
                    )
            );
            using var client = new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost/config-ui/"),
            };

            Type capabilityType = loader.AvailableCapabilities.Single(t =>
                t.Name == nameof(IFixtureCapability)
            );
            dynamic proxy = loader.CreateProxy(capabilityType, client)!;

            // Reloading swaps in a new load context and requests Unload() of the previous one.
            // The proxy above was built from that previous context and, per AssemblyLoadContext's
            // collectible-unload contract, must keep working for as long as it (and the Type it
            // was built from) stays reachable, which it does, right here.
            loader.Load(tempAssembly);

            string result = await proxy.EchoAsync("still alive", default(CancellationToken));
            Assert.Equal("still alive", result);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond
    ) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => respond(request, cancellationToken);
    }
}
