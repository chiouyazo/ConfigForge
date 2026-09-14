using System.Net.Http.Headers;
using System.Text;

namespace ConfigForge.AspNet.RemoteInstances;

internal sealed class RemoteInstanceClientFactory(
    IHttpClientFactory httpClientFactory,
    AspNetConfigForgeOptions options
) : IRemoteInstanceClientFactory
{
    public IReadOnlyList<string> InstanceNames => [.. options.RemoteInstances.Select(i => i.Name)];

    public IRemoteInstanceClient Create(string instanceName)
    {
        RemoteInstanceOptions instance =
            options.RemoteInstances.FirstOrDefault(i =>
                string.Equals(i.Name, instanceName, StringComparison.Ordinal)
            ) ?? throw new InvalidOperationException($"Unknown remote instance '{instanceName}'.");

        HttpClient httpClient = httpClientFactory.CreateClient($"cf-remote-{instance.Name}");
        httpClient.BaseAddress = new Uri(
            $"{instance.BaseUrl.ToString().TrimEnd('/')}{NormalizePrefix(instance.PathPrefix)}"
        );
        httpClient.DefaultRequestHeaders.Authorization = BuildBasicAuth(
            instance.Username,
            instance.Password
        );

        return new RemoteInstanceClient(instance.Name, httpClient);
    }

    private static string NormalizePrefix(string prefix) =>
        prefix.StartsWith('/') ? $"{prefix}/" : $"/{prefix}/";

    private static AuthenticationHeaderValue BuildBasicAuth(string username, string password)
    {
        byte[] bytes = Encoding.UTF8.GetBytes($"{username}:{password}");
        return new AuthenticationHeaderValue("Basic", Convert.ToBase64String(bytes));
    }
}
