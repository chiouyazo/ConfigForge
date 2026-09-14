using ConfigForge.AspNet.Endpoints;

namespace ConfigForge.AspNet.RemoteInstances;

internal sealed class RemoteInstancePoller(TimeProvider timeProvider) : IRemoteInstancePoller
{
    public async Task<RemoteInstanceSnapshot> PollAsync(
        IRemoteInstanceClient client,
        RemoteInstanceSnapshot? previous,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(client);
        DateTimeOffset now = timeProvider.GetUtcNow();

        try
        {
            ManifestResponse manifest = await client.GetManifestAsync(cancellationToken);

            Dictionary<string, IReadOnlyList<CollectionEntryResponse>> sectionData = new(
                StringComparer.Ordinal
            );
            foreach (
                string collectionKey in manifest
                    .Categories.Select(c => c.CollectionKey)
                    .OfType<string>()
            )
            {
                sectionData[collectionKey] = await client.GetDataAsync(
                    collectionKey,
                    cancellationToken
                );
            }

            string? documentJson = await client.GetDocumentAsync(cancellationToken);

            return RemoteInstanceSnapshot.Online(
                client.InstanceName,
                now,
                manifest,
                sectionData,
                documentJson
            );
        }
        catch (RemoteInstanceUnauthorizedException)
        {
            return RemoteInstanceSnapshot.Unauthorized(client.InstanceName, now, previous);
        }
        catch (HttpRequestException ex)
        {
            return RemoteInstanceSnapshot.Unreachable(
                client.InstanceName,
                now,
                $"Connection failed: {ex.Message}",
                RemoteInstanceFailureReason.ConnectionFailed,
                null,
                previous
            );
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return RemoteInstanceSnapshot.Unreachable(
                client.InstanceName,
                now,
                "Timed out while polling the instance.",
                RemoteInstanceFailureReason.TimedOut,
                null,
                previous
            );
        }
        catch (RemoteInstanceProtocolException ex)
        {
            return RemoteInstanceSnapshot.Unreachable(
                client.InstanceName,
                now,
                ex.Message,
                RemoteInstanceFailureReason.HttpError,
                ex.HttpStatusCode,
                previous
            );
        }
    }
}
