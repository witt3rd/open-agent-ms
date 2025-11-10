using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using OpenAgent.Core;

namespace OpenAgent.Providers.Anthropic;

/// <summary>
/// Anthropic implementation of ILlmClient.
/// </summary>
public class AnthropicLlmClient : ILlmClient
{
    private readonly AnthropicClient _client;
    private readonly string _model;

    public AnthropicLlmClient(string apiKey, string model = "claude-sonnet-4-20250514")
    {
        _client = new AnthropicClient(apiKey);
        _model = model;
    }

    public async Task<LlmResponse> SendAsync(
        List<LlmMessage> messages,
        string? systemPrompt = null,
        List<LlmTool>? tools = null,
        int? maxTokens = null,
        float? temperature = null,
        CancellationToken cancellationToken = default)
    {
        // Convert our messages to Anthropic format
        var anthropicMessages = messages.Select(m => new Message
        {
            Role = m.Role,
            Content = new List<ContentBase> { new TextContent { Text = m.Content } }
        }).ToList();

        // Convert tools to Anthropic format
        List<Tool>? anthropicTools = null;
        if (tools != null && tools.Count > 0)
        {
            anthropicTools = tools.Select(t => new Tool
            {
                Name = t.Name,
                Description = t.Description,
                InputSchema = t.InputSchema
            }).ToList();
        }

        // Create the request
        var request = new MessageRequest
        {
            Model = _model,
            MaxTokens = maxTokens ?? 4096,
            Messages = anthropicMessages,
            Stream = false
        };

        if (!string.IsNullOrEmpty(systemPrompt))
        {
            request.System = new List<SystemMessage>
            {
                new SystemMessage(systemPrompt)
            };
        }

        if (temperature.HasValue)
        {
            request.Temperature = temperature.Value;
        }

        if (anthropicTools != null)
        {
            request.Tools = anthropicTools;
        }

        // Send the request
        var response = await _client.Messages.CreateAsync(request, cancellationToken);

        // Parse the response
        var textContent = string.Empty;
        var toolUses = new List<LlmToolUse>();

        foreach (var content in response.Content)
        {
            if (content is TextContent textBlock)
            {
                textContent += textBlock.Text;
            }
            else if (content is ToolUseContent toolUseBlock)
            {
                var input = new Dictionary<string, object?>();

                // Convert tool use input to dictionary
                if (toolUseBlock.Input is System.Text.Json.JsonElement jsonElement)
                {
                    foreach (var prop in jsonElement.EnumerateObject())
                    {
                        input[prop.Name] = ParseJsonValue(prop.Value);
                    }
                }

                toolUses.Add(new LlmToolUse(
                    toolUseBlock.Id,
                    toolUseBlock.Name,
                    input));
            }
        }

        return new LlmResponse(
            TextContent: textContent,
            ToolUses: toolUses,
            IsComplete: response.StopReason == "end_turn",
            StopReason: response.StopReason);
    }

    private static object? ParseJsonValue(System.Text.Json.JsonElement element)
    {
        return element.ValueKind switch
        {
            System.Text.Json.JsonValueKind.String => element.GetString(),
            System.Text.Json.JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            System.Text.Json.JsonValueKind.True => true,
            System.Text.Json.JsonValueKind.False => false,
            System.Text.Json.JsonValueKind.Null => null,
            System.Text.Json.JsonValueKind.Array => element.EnumerateArray().Select(ParseJsonValue).ToList(),
            System.Text.Json.JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(p => p.Name, p => ParseJsonValue(p.Value)),
            _ => null
        };
    }
}
