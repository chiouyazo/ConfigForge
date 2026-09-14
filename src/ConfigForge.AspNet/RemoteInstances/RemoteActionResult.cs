namespace ConfigForge.AspNet.RemoteInstances;

/// <summary>The outcome of relaying an action invocation to a remote instance.</summary>
internal sealed record RemoteActionResult(
    string InstanceName,
    string ActionId,
    bool Success,
    string? Message,
    string? Severity
)
{
    public static RemoteActionResult Completed(
        string instanceName,
        string actionId,
        bool success,
        string? message,
        string? severity
    ) => new(instanceName, actionId, success, message, severity);

    public static RemoteActionResult NotRegistered(string instanceName, string actionId) =>
        new(
            instanceName,
            actionId,
            false,
            $"Instance '{instanceName}' has no handler for '{actionId}'.",
            "Danger"
        );

    public static RemoteActionResult Rejected(
        string instanceName,
        string actionId,
        string reason
    ) => new(instanceName, actionId, false, reason, "Danger");

    public static RemoteActionResult Unauthorized(string instanceName, string actionId) =>
        new(
            instanceName,
            actionId,
            false,
            $"Instance '{instanceName}' rejected the configured credentials.",
            "Danger"
        );

    public static RemoteActionResult Failed(string instanceName, string actionId, string reason) =>
        new(instanceName, actionId, false, reason, "Danger");
}
