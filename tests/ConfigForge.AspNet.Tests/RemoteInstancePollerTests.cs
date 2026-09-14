using System.Net;
using ConfigForge.AspNet.RemoteInstances;
using Xunit;

namespace ConfigForge.AspNet.Tests;

/// <summary>
/// <see cref="RemoteInstancePoller"/> must distinguish a connection failure, a timeout, and an
/// HTTP error status from one another, not collapse them into one generic "unreachable" reason,
/// so the merged sidebar can show which of the three actually happened.
/// </summary>
public sealed class RemoteInstancePollerTests
{
    [Fact]
    public async Task PollAsync_ConnectionRefused_ReturnsConnectionFailedReason()
    {
        RemoteInstanceSnapshot snapshot = await PollWithHandlerAsync(
            new ThrowingHandler(() => new HttpRequestException("Connection refused"))
        );

        Assert.Equal(RemoteInstanceStatus.Offline, snapshot.Status);
        Assert.Equal(RemoteInstanceFailureReason.ConnectionFailed, snapshot.FailureReason);
        Assert.Contains("Connection refused", snapshot.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PollAsync_RequestTimesOut_ReturnsTimedOutReason()
    {
        RemoteInstanceSnapshot snapshot = await PollWithHandlerAsync(
            new ThrowingHandler(() => new TaskCanceledException("The request timed out."))
        );

        Assert.Equal(RemoteInstanceStatus.Offline, snapshot.Status);
        Assert.Equal(RemoteInstanceFailureReason.TimedOut, snapshot.FailureReason);
    }

    [Fact]
    public async Task PollAsync_ServerReturns500_ReturnsHttpErrorReasonWithStatusCode()
    {
        RemoteInstanceSnapshot snapshot = await PollWithHandlerAsync(
            new RespondingHandler(HttpStatusCode.InternalServerError, "boom")
        );

        Assert.Equal(RemoteInstanceStatus.Offline, snapshot.Status);
        Assert.Equal(RemoteInstanceFailureReason.HttpError, snapshot.FailureReason);
        Assert.Equal(500, snapshot.HttpStatusCode);
    }

    [Fact]
    public async Task PollAsync_ServerReturns401_ReturnsUnauthorizedStatus()
    {
        RemoteInstanceSnapshot snapshot = await PollWithHandlerAsync(
            new RespondingHandler(HttpStatusCode.Unauthorized, string.Empty)
        );

        Assert.Equal(RemoteInstanceStatus.Unauthorized, snapshot.Status);
    }

    private static async Task<RemoteInstanceSnapshot> PollWithHandlerAsync(
        HttpMessageHandler handler
    )
    {
        using HttpMessageHandler disposableHandler = handler;
        using var httpClient = new HttpClient(disposableHandler, disposeHandler: false)
        {
            BaseAddress = new Uri("http://remote.test/config-ui/"),
        };
        var client = new RemoteInstanceClient("instanceX", httpClient);
        var poller = new RemoteInstancePoller(TimeProvider.System);

        return await poller.PollAsync(client, previous: null, CancellationToken.None);
    }

    private sealed class ThrowingHandler(Func<Exception> exceptionFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => throw exceptionFactory();
    }

    private sealed class RespondingHandler(HttpStatusCode statusCode, string body)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) =>
            Task.FromResult(
                new HttpResponseMessage(statusCode) { Content = new StringContent(body) }
            );
    }
}
