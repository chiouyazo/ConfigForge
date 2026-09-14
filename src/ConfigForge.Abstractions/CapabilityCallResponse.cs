namespace ConfigForge.Abstractions;

/// <summary>
/// The wire response body a capability action returns, extending the existing
/// <c>{prefix}/action/{actionId}</c> envelope (<c>success</c>/<c>message</c>) with the method's
/// return value.
/// </summary>
/// <typeparam name="T">The capability method's declared result type.</typeparam>
/// <param name="Success">Mirrors the action endpoint's existing success contract.</param>
/// <param name="Message">A human-readable failure reason, when <paramref name="Success"/> is <see langword="false"/>.</param>
/// <param name="Result">The method's return value, present when <paramref name="Success"/> is <see langword="true"/>.</param>
public sealed record CapabilityCallResponse<T>(bool Success, string? Message, T? Result);
