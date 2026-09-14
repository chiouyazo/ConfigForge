namespace ConfigForge.AspNet.RemoteInstances;

/// <summary>
/// Where one composite (namespaced) field key mirrored from a remote instance actually comes
/// from: which instance, and its original (un-namespaced) key on that instance.
/// </summary>
/// <param name="InstanceName">The originating instance's configured name.</param>
/// <param name="OriginalKey">The field's path key on the origin, before namespacing.</param>
internal sealed record RemoteFieldOrigin(string InstanceName, string OriginalKey);
