using System.Runtime.CompilerServices;
using OpenAgent.Core.Hooks;
using OpenAgent.Core.Tools;

namespace OpenAgent.Core;

/// <summary>
/// Core agent loop implementing the gather → act → verify → repeat pattern.
/// </summary>
public class AgentLoop
{
    private readonly ILlmClient _llmClient;
    private readonly IToolRegistry _toolRegistry;
    private readonly AgentOptions _options;
    private readonly Dictionary<HookType, List<HookCallback>> _hooks;

    public AgentLoop(
        ILlmClient llmClient,
        IToolRegistry toolRegistry,
        AgentOptions? options = null,
        Dictionary<HookType, List<HookCallback>>? hooks = null)
    {
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _options = options ?? new AgentOptions();
        _hooks = hooks ?? new Dictionary<HookType, List<HookCallback>>();
    }

    /// <summary>
    /// Execute the agent loop for a user prompt.
    /// </summary>
    /// <param name="userPrompt">User's input message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Stream of agent events.</returns>
    public async IAsyncEnumerable<AgentEvent> ExecuteAsync(
        string userPrompt,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var conversationHistory = new List<LlmMessage>();
        await foreach (var evt in ExecuteAsync(conversationHistory, userPrompt, cancellationToken))
        {
            yield return evt;
        }
    }

    /// <summary>
    /// Execute the agent loop with existing conversation history.
    /// </summary>
    public async IAsyncEnumerable<AgentEvent> ExecuteAsync(
        List<LlmMessage> conversationHistory,
        string userPrompt,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Add user message to history
        conversationHistory.Add(new LlmMessage("user", userPrompt));

        int turnCount = 0;
        bool isDone = false;
        var systemPrompt = _options.SystemPrompt ?? AgentOptions.DefaultSystemPrompt;

        while (!isDone && turnCount < _options.MaxTurns)
        {
            turnCount++;

            try
            {
                // 1. GATHER CONTEXT
                yield return new AgentEvent.GatheringContext();

                var availableTools = _toolRegistry.GetAll()
                    .Select(t => new LlmTool(
                        t.Name,
                        t.Description,
                        new { type = "object", properties = new { } })) // Simplified schema for now
                    .ToList();

                // Execute pre-model-call hooks
                var hookContext = new HookContext
                {
                    TurnNumber = turnCount,
                    Metadata = new Dictionary<string, object>
                    {
                        ["conversation_length"] = conversationHistory.Count
                    }
                };

                var preModelResult = await ExecuteHooksAsync(HookType.PreModelCall, conversationHistory, hookContext);
                if (!preModelResult.IsAllowed)
                {
                    yield return new AgentEvent.Error(
                        new InvalidOperationException(preModelResult.Reason),
                        $"Model call denied: {preModelResult.Reason}");
                    break;
                }

                // 2. TAKE ACTION (call LLM)
                yield return new AgentEvent.CallingModel();

                var response = await _llmClient.SendAsync(
                    conversationHistory,
                    systemPrompt,
                    availableTools,
                    _options.MaxTokens,
                    _options.Temperature,
                    cancellationToken);

                // Execute post-model-call hooks
                await ExecuteHooksAsync(HookType.PostModelCall, response, hookContext);

                // Handle text content
                if (!string.IsNullOrEmpty(response.TextContent))
                {
                    yield return new AgentEvent.ModelResponse(response.TextContent);
                }

                // 3. EXECUTE TOOLS (if requested)
                if (response.ToolUses.Count > 0)
                {
                    var toolResultsContent = new List<string>();

                    foreach (var toolUse in response.ToolUses)
                    {
                        // Execute pre-tool-use hooks
                        var preToolResult = await ExecuteHooksAsync(HookType.PreToolUse, toolUse, hookContext);
                        if (!preToolResult.IsAllowed)
                        {
                            yield return new AgentEvent.ToolDenied(toolUse.Name, preToolResult.Reason ?? "Denied by hook");

                            // Add tool error to conversation
                            toolResultsContent.Add($"[Tool {toolUse.Name} was denied: {preToolResult.Reason}]");
                            continue;
                        }

                        // Execute the tool
                        yield return new AgentEvent.ExecutingTool(toolUse.Name, toolUse.Input);

                        try
                        {
                            var result = await _toolRegistry.ExecuteAsync(
                                toolUse.Name,
                                toolUse.Input,
                                cancellationToken);

                            yield return new AgentEvent.ToolResult(toolUse.Name, result);

                            // Execute post-tool-use hooks
                            await ExecuteHooksAsync(HookType.PostToolUse, result, hookContext);

                            // Add tool result to conversation
                            toolResultsContent.Add($"[Tool {toolUse.Name} result: {result}]");
                        }
                        catch (Exception ex)
                        {
                            yield return new AgentEvent.Error(ex, $"Tool {toolUse.Name} failed: {ex.Message}");
                            toolResultsContent.Add($"[Tool {toolUse.Name} error: {ex.Message}]");
                        }
                    }

                    // Add assistant's response (with tool uses) to history
                    var assistantContent = response.TextContent ?? "";
                    if (toolResultsContent.Count > 0)
                    {
                        assistantContent += "\n" + string.Join("\n", toolResultsContent);
                    }
                    conversationHistory.Add(new LlmMessage("assistant", assistantContent));

                    // Continue the loop to let the model respond to tool results
                }
                else
                {
                    // 4. CHECK COMPLETION
                    // No tool uses, so the model is done
                    conversationHistory.Add(new LlmMessage("assistant", response.TextContent ?? ""));

                    isDone = true;
                    yield return new AgentEvent.Completed(response.TextContent ?? "");
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                yield return new AgentEvent.Error(ex, $"Error in turn {turnCount}: {ex.Message}");
                break;
            }
        }

        if (turnCount >= _options.MaxTurns)
        {
            yield return new AgentEvent.MaxTurnsReached(turnCount);
        }

        // Execute post-agent-stop hooks
        await ExecuteHooksAsync(
            HookType.PostAgentStop,
            conversationHistory,
            new HookContext { TurnNumber = turnCount });
    }

    private async Task<HookResult> ExecuteHooksAsync(HookType hookType, object? data, HookContext context)
    {
        if (!_hooks.TryGetValue(hookType, out var hooks) || hooks.Count == 0)
        {
            return HookResult.Allow();
        }

        foreach (var hook in hooks)
        {
            var result = await hook(data, context);
            if (!result.IsAllowed)
            {
                return result;
            }
        }

        return HookResult.Allow();
    }
}
