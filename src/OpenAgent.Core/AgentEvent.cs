namespace OpenAgent.Core;

/// <summary>
/// Events emitted by the agent loop for frontend consumption.
/// </summary>
public abstract record AgentEvent
{
    /// <summary>
    /// Agent is gathering context before making a model call.
    /// </summary>
    public sealed record GatheringContext : AgentEvent;

    /// <summary>
    /// Agent is calling the LLM.
    /// </summary>
    public sealed record CallingModel : AgentEvent;

    /// <summary>
    /// Model has returned a response.
    /// </summary>
    public sealed record ModelResponse(string Text, bool IsPartial = false) : AgentEvent;

    /// <summary>
    /// Model is thinking (extended thinking mode).
    /// </summary>
    public sealed record ThinkingUpdate(string Thinking) : AgentEvent;

    /// <summary>
    /// Agent is executing a tool.
    /// </summary>
    public sealed record ExecutingTool(string Name, object? Arguments) : AgentEvent;

    /// <summary>
    /// Tool execution completed.
    /// </summary>
    public sealed record ToolResult(string Name, object Result) : AgentEvent;

    /// <summary>
    /// Tool execution was denied by hook.
    /// </summary>
    public sealed record ToolDenied(string Name, string Reason) : AgentEvent;

    /// <summary>
    /// Agent has completed the task.
    /// </summary>
    public sealed record Completed(string FinalMessage) : AgentEvent;

    /// <summary>
    /// Agent reached maximum turns without completing.
    /// </summary>
    public sealed record MaxTurnsReached(int Turns) : AgentEvent;

    /// <summary>
    /// An error occurred during execution.
    /// </summary>
    public sealed record Error(Exception Exception, string Message) : AgentEvent;
}
