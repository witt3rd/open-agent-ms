namespace OpenAgent.Core.Tools;

/// <summary>
/// Registry for managing available tools.
/// </summary>
public interface IToolRegistry
{
    /// <summary>
    /// Register a tool.
    /// </summary>
    void Register(ITool tool);

    /// <summary>
    /// Get a tool by name.
    /// </summary>
    ITool? GetTool(string name);

    /// <summary>
    /// Get all registered tools.
    /// </summary>
    IReadOnlyList<ITool> GetAll();

    /// <summary>
    /// Execute a tool by name.
    /// </summary>
    Task<object> ExecuteAsync(string toolName, Dictionary<string, object?> arguments, CancellationToken cancellationToken = default);
}
