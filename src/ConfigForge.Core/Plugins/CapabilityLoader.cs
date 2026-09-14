using System.Reflection;
using System.Runtime.Loader;
using ConfigForge.Abstractions;
using ConfigForge.Core.CapabilityProxy;
using Serilog;

namespace ConfigForge.Core.Plugins;

/// <summary>
/// Loads a plugin assembly for its <c>[CapabilityContract]</c>-marked interfaces and builds
/// HTTP-calling <see cref="DispatchProxy"/> proxies for them, without ever calling
/// <see cref="Activator.CreateInstance(Type)"/> on a concrete type from the assembly or invoking
/// any of its methods.
/// </summary>
/// <remarks>
/// Deliberately not a modification of <see cref="PluginLoader"/>, which exists to execute a
/// plugin for the product that owns it, the opposite of this type's guarantee. Both share the
/// same collectible <see cref="AssemblyLoadContext"/> isolation pattern (private dependencies
/// resolved from the plugin's own folder; <c>ConfigForge.*</c> contract types left to the host's
/// default context so <see cref="CapabilityContractAttribute"/> instances compare equal across
/// the boundary) so a capability assembly and a plugin assembly can be the very same file.
/// </remarks>
public sealed class CapabilityLoader : ICapabilityLoader, IDisposable
{
    private const string ProxyAssemblyResourceName = "ConfigForge.Core.CapabilityProxy.dll";

    private readonly ILogger _logger;
    private State _state = new(null, [], new Dictionary<string, Type>(StringComparer.Ordinal));

    /// <summary>Initializes a new capability loader.</summary>
    /// <param name="logger">An optional Serilog logger; defaults to the static <see cref="Log.Logger"/>.</param>
    public CapabilityLoader(ILogger? logger = null) => _logger = logger ?? Log.Logger;

    /// <inheritdoc />
    public IReadOnlyCollection<Type> AvailableCapabilities =>
        Volatile.Read(ref _state).Capabilities;

    /// <inheritdoc />
    public IReadOnlyDictionary<string, Type> AvailableWidgets => Volatile.Read(ref _state).Widgets;

    /// <inheritdoc />
    public CapabilityLoadResult Load(string assemblyPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(assemblyPath);
        string fullPath = Path.GetFullPath(assemblyPath);

        if (!File.Exists(fullPath))
        {
            _logger.Warning(
                "Capability assembly {Assembly} does not exist; keeping previously loaded capabilities.",
                fullPath
            );
            return new CapabilityLoadResult(
                CapabilityLoadStatus.AssemblyNotFound,
                AvailableCapabilities,
                null,
                AvailableWidgets
            );
        }

        CapabilityLoadContext context = new(fullPath);

        try
        {
            // Loaded from an in-memory copy of the bytes, not context.LoadFromAssemblyPath(fullPath):
            // the latter keeps the file mapped for as long as the context is alive, which on Windows
            // blocks exactly the delete-then-recreate a deploy step does to replace this file.
            byte[] assemblyBytes = File.ReadAllBytes(fullPath);
            using MemoryStream assemblyStream = new(assemblyBytes);
            Assembly assembly = context.LoadFromStream(assemblyStream);
            HashSet<Type> found = [];
            Dictionary<string, Type> widgets = new(StringComparer.Ordinal);

            foreach (Type type in assembly.GetTypes())
            {
                if (
                    type.IsInterface
                    && type.GetCustomAttribute<CapabilityContractAttribute>() is not null
                )
                {
                    found.Add(type);
                }

                if (
                    type is { IsClass: true, IsAbstract: false }
                    && type.GetCustomAttribute<CapabilityWidgetAttribute>() is { } widgetAttribute
                )
                {
                    widgets[widgetAttribute.WidgetId] = type;
                }
            }

            Swap(context, found, widgets);
            _logger.Information(
                "Loaded {CapabilityCount} capability contract(s) and {WidgetCount} widget(s) from {Assembly}.",
                found.Count,
                widgets.Count,
                fullPath
            );
            return new CapabilityLoadResult(CapabilityLoadStatus.Loaded, found, null, widgets);
        }
        catch (Exception ex)
            when (ex is BadImageFormatException or ReflectionTypeLoadException or IOException)
        {
            context.Unload();
            _logger.Warning(
                ex,
                "Failed to load capability assembly {Assembly}; keeping previously loaded capabilities.",
                fullPath
            );
            return new CapabilityLoadResult(
                CapabilityLoadStatus.LoadFailed,
                AvailableCapabilities,
                ex,
                AvailableWidgets
            );
        }
    }

    /// <inheritdoc />
    public object? CreateProxy(Type capabilityInterface, HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(capabilityInterface);
        ArgumentNullException.ThrowIfNull(httpClient);

        State state = Volatile.Read(ref _state);
        if (state.Context is null || !state.Capabilities.Contains(capabilityInterface))
        {
            return null;
        }

        // DispatchProxy.Create's generated assembly is only collectible when its *base* type's
        // assembly is collectible (the interface's collectibility is not consulted), and a
        // non-collectible assembly may not reference a collectible one. capabilityInterface always
        // lives in this loader's collectible context, so the base type must too: we hand it a
        // private copy of ConfigForge.Core.CapabilityProxy.dll loaded into that same context (see
        // CapabilityLoadContext.GetProxyBaseType), and reach its Initialize method through
        // reflection since it is a distinct Type from this assembly's own referenced copy.
        Type proxyBaseType = state.Context.GetProxyBaseType();
        object proxy = DispatchProxy.Create(capabilityInterface, proxyBaseType);
        MethodInfo initialize = proxyBaseType.GetMethod(
            nameof(RemoteCapabilityDispatchProxy.Initialize),
            BindingFlags.Instance | BindingFlags.Public
        )!;
        initialize.Invoke(proxy, [httpClient, capabilityInterface]);
        return proxy;
    }

    /// <inheritdoc />
    public T? CreateProxy<T>(HttpClient httpClient)
        where T : class => (T?)CreateProxy(typeof(T), httpClient);

    /// <summary>
    /// Requests unload of the currently loaded capability assembly's context. Does not clear
    /// <see cref="AvailableCapabilities"/>: any proxy already built from it keeps working, and the
    /// underlying <see cref="AssemblyLoadContext"/> only actually collects once every such proxy
    /// (and any call in flight through one) is no longer reachable.
    /// </summary>
    public void Dispose() => Volatile.Read(ref _state).Context?.Unload();

    private void Swap(
        CapabilityLoadContext context,
        HashSet<Type> capabilities,
        Dictionary<string, Type> widgets
    )
    {
        State previous = Interlocked.Exchange(
            ref _state,
            new State(context, capabilities, widgets)
        );

        // Requests collection of the previous context; harmless when proxies built from it are
        // still reachable. See AssemblyLoadContext.Unloading: unload only completes once the
        // last such reference is released.
        previous.Context?.Unload();
    }

    private sealed record State(
        CapabilityLoadContext? Context,
        IReadOnlyCollection<Type> Capabilities,
        IReadOnlyDictionary<string, Type> Widgets
    );

    private sealed class CapabilityLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;
        private Type? _proxyBaseType;

        public CapabilityLoadContext(string mainAssemblyPath)
            : base(Path.GetFileNameWithoutExtension(mainAssemblyPath), isCollectible: true) =>
            _resolver = new AssemblyDependencyResolver(mainAssemblyPath);

        /// <summary>
        /// A copy of <see cref="RemoteCapabilityDispatchProxy"/> loaded into this very context,
        /// created (and cached) on first use. See the remark on <see cref="CreateProxy"/> for why.
        /// </summary>
        public Type GetProxyBaseType()
        {
            if (_proxyBaseType is not null)
            {
                return _proxyBaseType;
            }

            using Stream resourceStream =
                typeof(CapabilityLoader).Assembly.GetManifestResourceStream(
                    ProxyAssemblyResourceName
                )
                ?? throw new InvalidOperationException(
                    $"Embedded resource '{ProxyAssemblyResourceName}' is missing from "
                        + $"{typeof(CapabilityLoader).Assembly}."
                );
            Assembly proxyAssemblyCopy = LoadFromStream(resourceStream);
            _proxyBaseType = proxyAssemblyCopy.GetType(
                typeof(RemoteCapabilityDispatchProxy).FullName!,
                throwOnError: true
            )!;
            return _proxyBaseType;
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (assemblyName.Name?.StartsWith("ConfigForge.", StringComparison.Ordinal) == true)
            {
                return null;
            }

            string? path = _resolver.ResolveAssemblyToPath(assemblyName);
            return path is null ? null : LoadFromAssemblyPath(path);
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            string? path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            return path is null ? IntPtr.Zero : LoadUnmanagedDllFromPath(path);
        }
    }
}
