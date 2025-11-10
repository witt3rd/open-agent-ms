namespace OpenAgent.Core;

/// <summary>
/// Configuration options for the agent loop.
/// </summary>
public class AgentOptions
{
    /// <summary>
    /// Maximum number of turns before the agent stops.
    /// </summary>
    public int MaxTurns { get; init; } = 20;

    /// <summary>
    /// Temperature for LLM sampling (0.0 to 2.0).
    /// </summary>
    public float? Temperature { get; init; } = 0.7f;

    /// <summary>
    /// Maximum tokens to generate in response.
    /// </summary>
    public int? MaxTokens { get; init; } = 4096;

    /// <summary>
    /// System prompt to use for the agent.
    /// </summary>
    public string? SystemPrompt { get; init; }

    /// <summary>
    /// Model identifier to use.
    /// </summary>
    public string? ModelId { get; init; }

    /// <summary>
    /// Whether to enable streaming mode.
    /// </summary>
    public bool EnableStreaming { get; init; } = true;

    /// <summary>
    /// Default system prompt for the agent.
    /// </summary>
    public static string DefaultSystemPrompt =>
        "You are a helpful AI assistant. You have access to various tools to help complete tasks. " +
        "Use tools when appropriate to gather information or perform actions. " +
        "Be concise and helpful in your responses.";
}
