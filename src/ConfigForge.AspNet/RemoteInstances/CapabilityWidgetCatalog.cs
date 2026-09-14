using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using ConfigForge.Abstractions;
using ConfigForge.Core.Plugins;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace ConfigForge.AspNet.RemoteInstances;

/// <summary>
/// Loads every configured <see cref="RemoteInstanceOptions.CapabilityAssemblyPaths"/> entry via
/// its own <see cref="CapabilityLoader"/> (a product commonly splits its widgets across more than
/// one assembly) and exposes the discovered widgets keyed by their composite control type (see
/// <see cref="RemoteSchemaAggregator.CompositeControlType"/>), each paired with an
/// <see cref="HttpClient"/> dedicated to that one instance so a proxy built from a registration
/// can never reach a different instance than the one its widget renders for.
/// </summary>
internal sealed partial class CapabilityWidgetCatalog(
    IHttpClientFactory httpClientFactory,
    ILogger<CapabilityWidgetCatalog> logger
) : ICapabilityWidgetCatalog
{
    // Keyed by "{instanceName}::{assemblyPath}" - one loader per assembly, not per instance, so
    // an instance with several capability assemblies never has one Load() call discard another's
    // widgets (CapabilityLoader.Load replaces its own state wholesale on every call).
    private readonly ConcurrentDictionary<string, CapabilityLoader> _loaders = new(
        StringComparer.Ordinal
    );
    private readonly ConcurrentDictionary<string, HttpClient> _httpClients = new(
        StringComparer.Ordinal
    );
    private volatile Dictionary<string, CapabilityWidgetRegistration> _registrations = new(
        StringComparer.Ordinal
    );

    /// <inheritdoc />
    public bool TryGetWidget(
        string controlType,
        [NotNullWhen(true)] out CapabilityWidgetRegistration? registration
    ) => _registrations.TryGetValue(controlType, out registration);

    /// <summary>
    /// Re-loads every configured instance's capability assembly (a no-op file read when it hasn't
    /// changed) and rebuilds the widget registration set. Safe to call repeatedly from every poll
    /// cycle, same as <see cref="RemoteSchemaAggregator.RegisterHandlers"/>.
    /// </summary>
    public void Refresh(IReadOnlyList<RemoteInstanceOptions> instances)
    {
        ArgumentNullException.ThrowIfNull(instances);

        Dictionary<string, CapabilityWidgetRegistration> next = new(StringComparer.Ordinal);

        foreach (RemoteInstanceOptions instance in instances)
        {
            if (instance.CapabilityAssemblyPaths.Count == 0)
            {
                continue;
            }

            HttpClient httpClient = _httpClients.GetOrAdd(
                instance.Name,
                _ => BuildHttpClient(instance)
            );

            foreach (string path in instance.CapabilityAssemblyPaths)
            {
                string loaderKey = $"{instance.Name}::{path}";
                CapabilityLoader loader = _loaders.GetOrAdd(loaderKey, _ => new CapabilityLoader());
                loader.Load(path);

                foreach ((string widgetId, Type componentType) in loader.AvailableWidgets)
                {
                    List<Type> declaredCapabilities = InjectedCapabilityContracts(componentType);
                    Type? capabilityInterface = declaredCapabilities.Find(t =>
                        loader.AvailableCapabilities.Contains(t)
                    );

                    // A widget that injects no [CapabilityContract] interface at all has no
                    // remote data dependency (e.g. it only renders content baked into the
                    // capability assembly itself, like a license report) and still registers,
                    // with a null CapabilityInterfaceType. One that declares such a dependency
                    // but it isn't among the loader's currently discovered capabilities is a real
                    // mismatch (the capability assembly's shape changed) and is skipped instead.
                    if (declaredCapabilities.Count > 0 && capabilityInterface is null)
                    {
                        LogWidgetHasNoKnownCapability(logger, widgetId, instance.Name);
                        continue;
                    }

                    string key = RemoteSchemaAggregator.CompositeControlType(
                        instance.Name,
                        widgetId
                    );
                    next[key] = new CapabilityWidgetRegistration(
                        componentType,
                        capabilityInterface,
                        loader,
                        httpClient
                    );
                }
            }
        }

        _registrations = next;
    }

    // @inject-generated properties are `protected`, not public, so NonPublic must be included.
#pragma warning disable S3011 // Reading (never invoking) a protected [Inject] property's declared type, the same reflection Blazor's own DI already performs on it.
    private static List<Type> InjectedCapabilityContracts(Type componentType) =>
        componentType
            .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<InjectAttribute>() is not null)
            .Select(p => p.PropertyType)
            .Where(t => t.GetCustomAttribute<CapabilityContractAttribute>() is not null)
            .ToList();
#pragma warning restore S3011

    private HttpClient BuildHttpClient(RemoteInstanceOptions instance)
    {
        HttpClient httpClient = httpClientFactory.CreateClient($"cf-capability-{instance.Name}");
        httpClient.BaseAddress = new Uri(
            $"{instance.BaseUrl.ToString().TrimEnd('/')}{NormalizePrefix(instance.PathPrefix)}"
        );
        httpClient.DefaultRequestHeaders.Authorization = BuildBasicAuth(
            instance.Username,
            instance.Password
        );
        return httpClient;
    }

    private static string NormalizePrefix(string prefix) =>
        prefix.StartsWith('/') ? $"{prefix}/" : $"/{prefix}/";

    private static AuthenticationHeaderValue BuildBasicAuth(string username, string password)
    {
        byte[] bytes = Encoding.UTF8.GetBytes($"{username}:{password}");
        return new AuthenticationHeaderValue("Basic", Convert.ToBase64String(bytes));
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Widget {WidgetId} from instance {Instance} injects a [CapabilityContract] interface that isn't among the loader's currently discovered capabilities; skipped."
    )]
    private static partial void LogWidgetHasNoKnownCapability(
        ILogger logger,
        string widgetId,
        string instance
    );
}
