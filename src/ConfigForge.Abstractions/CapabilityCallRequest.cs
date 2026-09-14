namespace ConfigForge.Abstractions;

/// <summary>
/// The wire request body a remote capability proxy posts to <c>{prefix}/action/{interface}.{method}</c>.
/// </summary>
/// <param name="Args">
/// The call's arguments, in declaration order, with any trailing <see cref="System.Threading.CancellationToken"/>
/// parameter omitted. Each element is serialized using its own runtime type.
/// </param>
public sealed record CapabilityCallRequest(IReadOnlyList<object?> Args);
