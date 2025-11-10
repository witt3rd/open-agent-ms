namespace OpenAgent.Core.Hooks;

/// <summary>
/// Context information passed to hooks.
/// </summary>
public class HookContext
{
    /// <summary>
    /// Current turn number.
    /// </summary>
    public int TurnNumber { get; init; }

    /// <summary>
    /// Session identifier (if any).
    /// </summary>
    public string? SessionId { get; init; }

    /// <summary>
    /// Additional metadata.
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();
}
