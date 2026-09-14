using ConfigForge.Abstractions;
using ConfigForge.Blazor.Services;
using ConfigForge.Core.Plugins;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ConfigForge.Blazor.Tests;

/// <summary>
/// Direct tests of <see cref="ActionDispatcher"/> using a real
/// <see cref="PluginRegistry"/>, a <see cref="NullLogger{T}"/>, and a fake
/// <see cref="IActionContext"/>, without bUnit.
/// </summary>
public sealed class ActionDispatcherTests
{
    private static ActionDispatcher Create(PluginRegistry registry) =>
        new(registry, NullLogger<ActionDispatcher>.Instance);

    [Fact]
    public async Task DispatchActionAsync_UnregisteredAction_ToastsWarningAndDoesNotThrow()
    {
        var registry = new PluginRegistry();
        ActionDispatcher dispatcher = Create(registry);
        var context = new FakeActionContext();

        await dispatcher.DispatchActionAsync("does.not.exist", context);

        Assert.Single(context.Toasts);
        Assert.Equal(ToastSeverity.Warning, context.Toasts[0].Severity);
    }

    [Fact]
    public async Task DispatchActionAsync_RegisteredAction_InvokesHandler()
    {
        var registry = new PluginRegistry();
        var invoked = false;
        registry.RegisterAction(
            "act",
            _ =>
            {
                invoked = true;
                return Task.CompletedTask;
            }
        );
        ActionDispatcher dispatcher = Create(registry);

        await dispatcher.DispatchActionAsync("act", new FakeActionContext());

        Assert.True(invoked);
    }

    [Fact]
    public async Task DispatchActionAsync_HandlerThrows_ToastsDangerAndDoesNotThrow()
    {
        var registry = new PluginRegistry();
        registry.RegisterAction("boom", _ => throw new InvalidOperationException("boom"));
        ActionDispatcher dispatcher = Create(registry);
        var context = new FakeActionContext();

        await dispatcher.DispatchActionAsync("boom", context);

        Assert.Single(context.Toasts);
        Assert.Equal(ToastSeverity.Danger, context.Toasts[0].Severity);
    }

    [Fact]
    public async Task DispatchLoaderAsync_UnregisteredLoader_ReturnsEmptyAndDoesNotThrow()
    {
        var registry = new PluginRegistry();
        ActionDispatcher dispatcher = Create(registry);

        IReadOnlyList<SelectOption> options = await dispatcher.DispatchLoaderAsync(
            "missing",
            new FakeActionContext()
        );

        Assert.Empty(options);
    }

    [Fact]
    public async Task DispatchLoaderAsync_RegisteredLoader_ReturnsItsOptions()
    {
        var registry = new PluginRegistry();
        registry.RegisterLoader(
            "load",
            _ =>
                Task.FromResult<IReadOnlyList<SelectOption>>([
                    new SelectOption { Value = "a", Label = "A" },
                ])
        );
        ActionDispatcher dispatcher = Create(registry);

        IReadOnlyList<SelectOption> options = await dispatcher.DispatchLoaderAsync(
            "load",
            new FakeActionContext()
        );

        Assert.Single(options);
        Assert.Equal("a", options[0].Value);
    }

    [Fact]
    public async Task DispatchCollectionLoaderAsync_UnregisteredKey_ReturnsEmptyAndDoesNotThrow()
    {
        var registry = new PluginRegistry();
        ActionDispatcher dispatcher = Create(registry);

        IReadOnlyList<ConfigDocument> entries = await dispatcher.DispatchCollectionLoaderAsync(
            "missing",
            new FakeActionContext()
        );

        Assert.Empty(entries);
    }

    [Fact]
    public async Task DispatchCollectionLoaderAsync_RegisteredLoader_ReturnsItsEntries()
    {
        var registry = new PluginRegistry();
        registry.RegisterCollectionLoader(
            "connectors",
            (_, _) =>
                Task.FromResult<IReadOnlyList<ConfigDocument>>([
                    new ConfigDocument(new Dictionary<string, object?> { ["name"] = "a" }),
                ])
        );
        ActionDispatcher dispatcher = Create(registry);

        IReadOnlyList<ConfigDocument> entries = await dispatcher.DispatchCollectionLoaderAsync(
            "connectors",
            new FakeActionContext()
        );

        Assert.Single(entries);
        Assert.Equal("a", entries[0]["name"]);
    }

    [Fact]
    public async Task DispatchCollectionLoaderAsync_HandlerThrows_ToastsDangerAndReturnsEmpty()
    {
        var registry = new PluginRegistry();
        registry.RegisterCollectionLoader(
            "boom",
            (_, _) => throw new InvalidOperationException("boom")
        );
        ActionDispatcher dispatcher = Create(registry);
        var context = new FakeActionContext();

        IReadOnlyList<ConfigDocument> entries = await dispatcher.DispatchCollectionLoaderAsync(
            "boom",
            context
        );

        Assert.Empty(entries);
        Assert.Single(context.Toasts);
        Assert.Equal(ToastSeverity.Danger, context.Toasts[0].Severity);
    }
}
