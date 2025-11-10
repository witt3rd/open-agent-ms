namespace OpenAgent.Core;

/// <summary>
/// Message in a conversation.
/// </summary>
public record LlmMessage(string Role, string Content);

/// <summary>
/// Tool definition for the LLM.
/// </summary>
public record LlmTool(string Name, string Description, object InputSchema);

/// <summary>
/// Tool use request from the LLM.
/// </summary>
public record LlmToolUse(string Id, string Name, Dictionary<string, object?> Input);

/// <summary>
/// Response from the LLM.
/// </summary>
public record LlmResponse(
    string? TextContent,
    List<LlmToolUse> ToolUses,
    bool IsComplete,
    string? StopReason);

/// <summary>
/// Interface for LLM clients that the agent loop can use.
/// </summary>
public interface ILlmClient
{
    /// <summary>
    /// Send a conversation to the LLM and get a response.
    /// </summary>
    /// <param name="messages">Conversation history.</param>
    /// <param name="systemPrompt">System prompt.</param>
    /// <param name="tools">Available tools.</param>
    /// <param name="maxTokens">Maximum tokens to generate.</param>
    /// <param name="temperature">Sampling temperature.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>LLM response.</returns>
    Task<LlmResponse> SendAsync(
        List<LlmMessage> messages,
        string? systemPrompt = null,
        List<LlmTool>? tools = null,
        int? maxTokens = null,
        float? temperature = null,
        CancellationToken cancellationToken = default);
}
