namespace OpenAgent.Core.Hooks;

/// <summary>
/// Types of hooks that can intercept agent execution.
/// </summary>
public enum HookType
{
    /// <summary>
    /// Before a tool is executed.
    /// </summary>
    PreToolUse,

    /// <summary>
    /// After a tool is executed.
    /// </summary>
    PostToolUse,

    /// <summary>
    /// Before calling the LLM.
    /// </summary>
    PreModelCall,

    /// <summary>
    /// After the LLM returns a response.
    /// </summary>
    PostModelCall,

    /// <summary>
    /// Before the agent stops execution.
    /// </summary>
    PreAgentStop,

    /// <summary>
    /// After the agent stops execution.
    /// </summary>
    PostAgentStop
}
