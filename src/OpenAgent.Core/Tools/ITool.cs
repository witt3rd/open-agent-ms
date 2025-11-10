namespace OpenAgent.Core.Tools;

/// <summary>
/// Represents a tool that can be called by the agent.
/// </summary>
public interface ITool
{
    /// <summary>
    /// Name of the tool.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Description of what the tool does.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Execute the tool with the given arguments.
    /// </summary>
    /// <param name="arguments">Tool arguments as a dictionary.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool execution result.</returns>
    Task<object> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default);
}
