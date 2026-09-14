using System.Net.Http.Json;
using System.Reflection;
using ConfigForge.Abstractions;

namespace ConfigForge.Core.CapabilityProxy;

/// <summary>
/// A <see cref="DispatchProxy"/> that turns every call to a capability interface method into an
/// HTTP <c>POST {prefix}/action/{interface}.{method}</c> call, per the capability wire contract
/// documented in <c>docs/capability-loading.md</c>.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately its own tiny assembly, referencing only <c>ConfigForge.Abstractions</c>: a
/// generated <see cref="DispatchProxy"/> type is only collectible when its base type's own
/// assembly is loaded into a collectible <see cref="System.Runtime.Loader.AssemblyLoadContext"/>
/// (a non-collectible assembly cannot reference a collectible one, and a capability interface
/// always lives in one), so <c>ConfigForge.Core.CapabilityLoader</c> loads a private copy of THIS
/// assembly into the very same context as the interface it is proxying. Keeping
/// <see cref="CapabilityCallRequest"/>/<see cref="CapabilityCallResponse{T}"/>/
/// <see cref="CapabilityRemoteCallException"/> in <c>ConfigForge.Abstractions</c> rather than here
/// means that private copy still throws and serializes the SAME types the caller's own (default
/// load context) copy of <c>ConfigForge.Abstractions</c> declares, so a <c>catch
/// (CapabilityRemoteCallException)</c> written against the host's normal reference still catches
/// it, because the type resolves back to that one shared copy instead of a second one bundled
/// into this collectible copy.
/// </para>
/// <para>
/// Only ever holds an <see cref="HttpClient"/> and a string captured once at proxy-build time; it
/// never re-reads the capability interface's <see cref="Type"/> after construction, so an
/// in-flight call through an already-built proxy has nothing left to fail if the plugin assembly
/// it came from is later unloaded.
/// </para>
/// </remarks>
public class RemoteCapabilityDispatchProxy : DispatchProxy
{
    private static readonly MethodInfo InvokeAsyncGenericMethod =
        typeof(RemoteCapabilityDispatchProxy).GetMethod(
            nameof(InvokeAsyncGeneric),
            BindingFlags.Instance | BindingFlags.Public
        )!;

    private HttpClient _httpClient = null!;
    private string _capabilityId = null!;

    /// <summary>
    /// Captures the client to call through and the capability's stable id. Called reflectively by
    /// <c>CapabilityLoader.CreateProxy</c> right after <see cref="DispatchProxy.Create(Type, Type)"/>,
    /// since the loader only ever holds this type by reflection (see the remarks above).
    /// </summary>
    public void Initialize(HttpClient httpClient, Type capabilityInterface)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(capabilityInterface);

        _httpClient = httpClient;
        _capabilityId = capabilityInterface.FullName ?? capabilityInterface.Name;
    }

    /// <inheritdoc />
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);

        Type returnType = targetMethod.ReturnType;

        if (returnType == typeof(Task))
        {
            return InvokeVoidAsync(targetMethod, args);
        }

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            MethodInfo generic = InvokeAsyncGenericMethod.MakeGenericMethod(
                returnType.GetGenericArguments()[0]
            );
            return generic.Invoke(this, [targetMethod, args]);
        }

        throw new NotSupportedException(
            $"'{targetMethod.Name}' must return Task or Task<T> to be called through a remote "
                + "capability proxy."
        );
    }

    private async Task InvokeVoidAsync(MethodInfo targetMethod, object?[]? args) =>
        await SendAsync<object?>(targetMethod, args);

    /// <summary>
    /// Public so it can be located via <see cref="BindingFlags.Public"/> reflection (see
    /// <see cref="InvokeAsyncGenericMethod"/>) without bypassing normal accessibility rules.
    /// </summary>
    public async Task<T?> InvokeAsyncGeneric<T>(MethodInfo targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        return await SendAsync<T>(targetMethod, args);
    }

    private async Task<T?> SendAsync<T>(MethodInfo targetMethod, object?[]? args)
    {
        (object?[] payloadArgs, CancellationToken cancellationToken) = SplitCancellationToken(args);
        string actionId = $"{_capabilityId}.{targetMethod.Name}";

        using HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
            $"action/{actionId}",
            new CapabilityCallRequest(payloadArgs),
            cancellationToken
        );

        response.EnsureSuccessStatusCode();

        CapabilityCallResponse<T>? body = await response.Content.ReadFromJsonAsync<
            CapabilityCallResponse<T>
        >(cancellationToken);

        if (body is null)
        {
            throw new CapabilityRemoteCallException(
                $"Empty response calling capability action '{actionId}'."
            );
        }

        if (!body.Success)
        {
            throw new CapabilityRemoteCallException(
                body.Message ?? $"Capability action '{actionId}' failed."
            );
        }

        return body.Result;
    }

    private static (object?[] Args, CancellationToken CancellationToken) SplitCancellationToken(
        object?[]? args
    )
    {
        if (args is null || args.Length == 0)
        {
            return ([], CancellationToken.None);
        }

        return args[^1] is CancellationToken token
            ? (args[..^1], token)
            : (args, CancellationToken.None);
    }
}
