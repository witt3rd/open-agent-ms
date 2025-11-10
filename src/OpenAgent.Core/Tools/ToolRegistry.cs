namespace OpenAgent.Core.Tools;

/// <summary>
/// Default implementation of tool registry.
/// </summary>
public class ToolRegistry : IToolRegistry
{
    private readonly Dictionary<string, ITool> _tools = new();

    public void Register(ITool tool)
    {
        _tools[tool.Name] = tool;
    }

    public ITool? GetTool(string name)
    {
        return _tools.TryGetValue(name, out var tool) ? tool : null;
    }

    public IReadOnlyList<ITool> GetAll()
    {
        return _tools.Values.ToList();
    }

    public async Task<object> ExecuteAsync(
        string toolName,
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var tool = GetTool(toolName);
        if (tool == null)
        {
            throw new InvalidOperationException($"Tool '{toolName}' not found");
        }

        return await tool.ExecuteAsync(arguments, cancellationToken);
    }
}
