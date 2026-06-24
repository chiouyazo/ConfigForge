namespace ConfigForge.Plugin.Template;

/// <summary>
/// Trivial in-memory stand-in for a real service SDK. Lets the reference
/// handler code compile and run with no external dependency.
/// </summary>
public sealed class ExampleServiceClient
{
    private readonly string _endpoint;
    private readonly string _secret;

    /// <summary>Initializes a new client.</summary>
    /// <param name="endpoint">The service endpoint URL.</param>
    /// <param name="secret">The API secret used to authenticate.</param>
    public ExampleServiceClient(string endpoint, string secret)
    {
        _endpoint = endpoint;
        _secret = secret;
    }

    /// <summary>Verifies connectivity. The stub always succeeds.</summary>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    /// <returns>A completed task.</returns>
    public Task PingAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = _endpoint;
        _ = _secret;
        return Task.CompletedTask;
    }

    /// <summary>Returns a fixed set of fake channels.</summary>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    /// <returns>A small list of stub channels.</returns>
    public Task<IReadOnlyList<Channel>> GetChannelsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<Channel> channels =
        [
            new Channel("general", "General"),
            new Channel("alerts", "Alerts"),
        ];
        return Task.FromResult(channels);
    }
}
