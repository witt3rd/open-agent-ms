namespace OpenAgent.Core.Hooks;

/// <summary>
/// Result from a hook execution.
/// </summary>
public class HookResult
{
    /// <summary>
    /// Whether the operation is allowed to proceed.
    /// </summary>
    public bool IsAllowed { get; init; }

    /// <summary>
    /// Reason for denial if not allowed.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Allow the operation.
    /// </summary>
    public static HookResult Allow() => new() { IsAllowed = true };

    /// <summary>
    /// Deny the operation with a reason.
    /// </summary>
    public static HookResult Deny(string reason) => new() { IsAllowed = false, Reason = reason };
}
