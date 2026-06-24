using ConfigForge.Abstractions;

namespace ConfigForge.Plugin.Template;

/// <summary>
/// Reference plugin demonstrating the action, loader and validator extension
/// points. Copy this project as the starting point for a new plugin.
/// </summary>
public sealed class ExamplePlugin : IPlugin
{
    /// <inheritdoc />
    public string Id => "ConfigForge.Plugin.Example";

    /// <inheritdoc />
    public string DisplayName => "Example Plugin";

    /// <inheritdoc />
    public void Register(IPluginRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        // Action extension point: a button handler.
        registry.RegisterAction("example.testConnection", TestConnectionAsync);

        // Loader extension point: populates a select field at runtime.
        registry.RegisterLoader("example.loadChannels", LoadChannelsAsync);

        // Validator extension point: synchronous field validation.
        registry.RegisterValidator("example.validateChannel", ValidateChannel);
    }

    /// <summary>
    /// Pings the example service using the current endpoint and secret fields,
    /// reporting the outcome via a toast.
    /// </summary>
    /// <param name="ctx">The action context.</param>
    /// <returns>A task that completes when the action finishes.</returns>
    private static async Task TestConnectionAsync(IActionContext ctx)
    {
        await ctx.SetFieldLoadingAsync("api_secret", true);
        try
        {
            var client = new ExampleServiceClient(ctx["endpoint_url"], ctx["api_secret"]);
            await client.PingAsync(ctx.CancellationToken);
            await ctx.ShowToastAsync("Connection successful", ToastSeverity.Success);
        }
        catch (ServiceConnectionException ex)
        {
            await ctx.ShowToastAsync(ex.Message, ToastSeverity.Danger);
        }
        finally
        {
            await ctx.SetFieldLoadingAsync("api_secret", false);
        }
    }

    /// <summary>
    /// Loads the available channels from the example service.
    /// </summary>
    /// <param name="ctx">The action context.</param>
    /// <returns>The channels as select options.</returns>
    private static async Task<IReadOnlyList<SelectOption>> LoadChannelsAsync(IActionContext ctx)
    {
        var client = new ExampleServiceClient(ctx["endpoint_url"], ctx["api_secret"]);
        var channels = await client.GetChannelsAsync(ctx.CancellationToken);
        return channels.Select(c => new SelectOption { Value = c.Id, Label = c.Name }).ToList();
    }

    /// <summary>
    /// Validates that a channel has been chosen.
    /// </summary>
    /// <param name="value">The current field value.</param>
    /// <returns>A passing result when a non-empty channel is selected.</returns>
    private static ValidationResult ValidateChannel(object? value) =>
        value is string s && !string.IsNullOrWhiteSpace(s)
            ? ValidationResult.Ok()
            : ValidationResult.Fail("Channel is required");
}
