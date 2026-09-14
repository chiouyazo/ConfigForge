using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ConfigForge.AspNet.Endpoints;

namespace ConfigForge.AspNet.RemoteInstances;

internal sealed class RemoteInstanceClient : IRemoteInstanceClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(
        JsonSerializerDefaults.Web
    );

    private readonly HttpClient _httpClient;

    public RemoteInstanceClient(string instanceName, HttpClient httpClient)
    {
        InstanceName = instanceName;
        _httpClient = httpClient;
    }

    public string InstanceName { get; }

    public async Task<ManifestResponse> GetManifestAsync(CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _httpClient.GetAsync(
            new Uri("manifest", UriKind.Relative),
            cancellationToken
        );
        await ThrowIfUnsuccessfulAsync(response, cancellationToken);

        ManifestResponse? manifest = await response.Content.ReadFromJsonAsync<ManifestResponse>(
            SerializerOptions,
            cancellationToken
        );
        return manifest
            ?? throw new RemoteInstanceProtocolException(InstanceName, "Empty manifest body.");
    }

    public async Task<IReadOnlyList<CollectionEntryResponse>> GetDataAsync(
        string sectionId,
        CancellationToken cancellationToken
    )
    {
        using HttpResponseMessage response = await _httpClient.GetAsync(
            new Uri($"data/{Uri.EscapeDataString(sectionId)}", UriKind.Relative),
            cancellationToken
        );
        await ThrowIfUnsuccessfulAsync(response, cancellationToken);

        IReadOnlyList<CollectionEntryResponse>? entries = await response.Content.ReadFromJsonAsync<
            IReadOnlyList<CollectionEntryResponse>
        >(SerializerOptions, cancellationToken);
        return entries ?? [];
    }

    public async Task<RemoteActionResult> PostActionAsync(
        string actionId,
        string? entryKey,
        CancellationToken cancellationToken
    )
    {
        using HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
            new Uri($"action/{Uri.EscapeDataString(actionId)}", UriKind.Relative),
            new ActionRequest(entryKey, null),
            SerializerOptions,
            cancellationToken
        );

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return RemoteActionResult.NotRegistered(InstanceName, actionId);
        }

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            return RemoteActionResult.Rejected(InstanceName, actionId, body);
        }

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return RemoteActionResult.Unauthorized(InstanceName, actionId);
        }

        if (!response.IsSuccessStatusCode)
        {
            return RemoteActionResult.Failed(
                InstanceName,
                actionId,
                $"Unexpected status {(int)response.StatusCode}."
            );
        }

        ActionResponse? body2 = await response.Content.ReadFromJsonAsync<ActionResponse>(
            SerializerOptions,
            cancellationToken
        );
        return RemoteActionResult.Completed(
            InstanceName,
            actionId,
            body2?.Success ?? true,
            body2?.Message,
            body2?.Severity
        );
    }

    public async Task<string?> GetDocumentAsync(CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _httpClient.GetAsync(
            new Uri("document", UriKind.Relative),
            cancellationToken
        );

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await ThrowIfUnsuccessfulAsync(response, cancellationToken);
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    public async Task<RemoteDocumentSaveResult> PostDocumentAsync(
        string documentJson,
        CancellationToken cancellationToken
    )
    {
        using StringContent content = new(documentJson, Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await _httpClient.PostAsync(
            new Uri("document", UriKind.Relative),
            content,
            cancellationToken
        );

        if (response.IsSuccessStatusCode)
        {
            return RemoteDocumentSaveResult.Ok;
        }

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return RemoteDocumentSaveResult.Failed(
                $"Instance '{InstanceName}' rejected the configured credentials."
            );
        }

        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity)
        {
            DocumentSaveErrorResponse? body =
                await response.Content.ReadFromJsonAsync<DocumentSaveErrorResponse>(
                    SerializerOptions,
                    cancellationToken
                );
            return body is null
                ? RemoteDocumentSaveResult.Failed($"HTTP {(int)response.StatusCode}.")
                : RemoteDocumentSaveResult.Rejected(body);
        }

        return RemoteDocumentSaveResult.Failed(
            $"Unexpected status {(int)response.StatusCode} from instance '{InstanceName}'."
        );
    }

    private static async Task ThrowIfUnsuccessfulAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken
    )
    {
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new RemoteInstanceUnauthorizedException();
        }

        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new RemoteInstanceProtocolException(
                "unknown",
                $"HTTP {(int)response.StatusCode}: {body}"
            )
            {
                HttpStatusCode = (int)response.StatusCode,
            };
        }
    }
}
