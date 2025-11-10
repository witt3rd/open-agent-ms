# Open Agent MS - Architecture Design

## Overview

Windows C# implementation of an Open Agent using an **imperative agent loop** paradigm (gather → act → verify → repeat) with a unified multi-provider LLM abstraction layer.

**Key Principle**: Use **Microsoft.Extensions.AI** as the foundation for provider abstraction, NOT Microsoft Agent Framework workflow graphs.

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│                  User Application / CLI                  │
└─────────────────────┬───────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────────┐
│              OpenAgent.Core (Agent Loop)                 │
│  ┌───────────────────────────────────────────────────┐  │
│  │  while (!done) {                                  │  │
│  │    1. Gather context (tools, knowledge, history) │  │
│  │    2. Take action (LLM call with tools)          │  │
│  │    3. Execute tools (with permission hooks)      │  │
│  │    4. Verify/check completion                    │  │
│  │  }                                                │  │
│  └───────────────────────────────────────────────────┘  │
│  - IAsyncEnumerable<Message> streaming                   │
│  - Hook system (pre/post tool use)                       │
│  - Multi-turn conversation state                         │
└─────────────────────┬───────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────────┐
│         OpenAgent.Providers (Unified Abstraction)        │
│                                                           │
│  Microsoft.Extensions.AI.IChatClient                      │
│  ├─ CompleteAsync(messages, options, cancellationToken)  │
│  └─ CompleteStreamingAsync(messages, ...)                │
│                                                           │
│  Middleware Pipeline:                                     │
│  ├─ Function calling middleware                          │
│  ├─ Caching middleware (distributed/in-memory)           │
│  ├─ Telemetry/logging middleware                         │
│  ├─ Retry/resilience middleware                          │
│  └─ Rate limiting middleware                             │
└─────────────────────┬───────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────────┐
│              Provider Implementations                    │
│                                                           │
│  CLOUD PROVIDERS:                                         │
│  ├─ OpenAI (Microsoft.Extensions.AI.OpenAI)             │
│  ├─ Azure OpenAI (Microsoft.Extensions.AI.OpenAI)       │
│  ├─ Anthropic (Anthropic.SDK → IChatClient adapter)     │
│  ├─ Google Gemini (GeminiDotnet.Extensions.AI)          │
│  ├─ AWS Bedrock (Amazon.BedrockRuntime → adapter)       │
│  └─ OpenRouter (OpenRouter.NET → IChatClient adapter)   │
│                                                           │
│  LOCAL PROVIDERS:                                         │
│  ├─ Ollama (OllamaSharp → IChatClient)                  │
│  ├─ Llama.cpp (LLamaSharp → IChatClient adapter)        │
│  └─ Windows ML/ONNX (OnnxRuntimeGenAI.DirectML adapter) │
└───────────────────────────────────────────────────────────┘
```

## Core Components

### 1. Message Types (OpenAgent.Core.Messages)

Based on Microsoft.Extensions.AI message format:

```csharp
// Base message abstraction from Microsoft.Extensions.AI
using Microsoft.Extensions.AI;

// ChatMessage hierarchy:
// - ChatMessage (base)
//   - UserChatMessage
//   - AssistantChatMessage
//   - SystemChatMessage
//   - FunctionCallContent
//   - FunctionResultContent

// Content types:
// - TextContent
// - ImageContent
// - AudioContent
// - DataContent
```

### 2. Agent Loop (OpenAgent.Core.AgentLoop)

**Imperative, event-driven control loop**:

```csharp
public class AgentLoop
{
    private readonly IChatClient _chatClient;
    private readonly IToolRegistry _toolRegistry;
    private readonly AgentOptions _options;

    public async IAsyncEnumerable<AgentEvent> ExecuteAsync(
        string userPrompt,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var conversationHistory = new List<ChatMessage>();
        conversationHistory.Add(new ChatMessage(ChatRole.User, userPrompt));

        int turnCount = 0;
        bool isDone = false;

        while (!isDone && turnCount < _options.MaxTurns)
        {
            // 1. GATHER CONTEXT
            yield return new AgentEvent.GatheringContext();
            var context = await GatherContextAsync(conversationHistory);

            // 2. TAKE ACTION (call LLM)
            yield return new AgentEvent.CallingModel();
            var response = await _chatClient.CompleteAsync(
                conversationHistory,
                new ChatOptions
                {
                    Tools = _toolRegistry.GetTools(),
                    Temperature = _options.Temperature
                },
                ct);

            conversationHistory.Add(response.Message);
            yield return new AgentEvent.ModelResponse(response.Message);

            // 3. EXECUTE TOOLS (if requested)
            if (response.Message.Contents.OfType<FunctionCallContent>().Any())
            {
                foreach (var toolCall in response.Message.Contents.OfType<FunctionCallContent>())
                {
                    // Execute hooks
                    var permitted = await ExecuteHooksAsync(
                        HookType.PreToolUse,
                        toolCall);

                    if (!permitted.IsAllowed)
                    {
                        yield return new AgentEvent.ToolDenied(
                            toolCall.Name,
                            permitted.Reason);
                        continue;
                    }

                    // Execute tool
                    yield return new AgentEvent.ExecutingTool(toolCall.Name);
                    var result = await _toolRegistry.ExecuteAsync(
                        toolCall.Name,
                        toolCall.Arguments);

                    conversationHistory.Add(new ChatMessage(
                        ChatRole.Tool,
                        new FunctionResultContent(
                            toolCall.CallId,
                            toolCall.Name,
                            result)));

                    yield return new AgentEvent.ToolResult(
                        toolCall.Name,
                        result);

                    await ExecuteHooksAsync(HookType.PostToolUse, result);
                }
            }
            else
            {
                // 4. CHECK COMPLETION
                isDone = true;
                yield return new AgentEvent.Completed(response.Message);
            }

            turnCount++;
        }

        if (turnCount >= _options.MaxTurns)
        {
            yield return new AgentEvent.MaxTurnsReached();
        }
    }
}
```

### 3. Provider Abstraction Layer

**Use Microsoft.Extensions.AI.IChatClient as the unified interface:**

```csharp
// All providers implement this interface
public interface IChatClient
{
    Task<ChatCompletion> CompleteAsync(
        IList<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<StreamingChatCompletionUpdate> CompleteStreamingAsync(
        IList<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default);

    ChatClientMetadata Metadata { get; }

    TService? GetService<TService>(object? key = null) where TService : class;
}
```

### 4. Provider Implementations

#### Cloud Providers

| Provider | Package | Status | Adapter Needed |
|----------|---------|--------|----------------|
| **OpenAI** | `Microsoft.Extensions.AI.OpenAI` | ✅ Official | No |
| **Azure OpenAI** | `Microsoft.Extensions.AI.OpenAI` | ✅ Official | No |
| **Anthropic** | `Anthropic.SDK` v5.8+ | 🔶 3rd Party | Yes - wrap SDK |
| **Google Gemini** | `GeminiDotnet.Extensions.AI` | 🔶 3rd Party | No |
| **AWS Bedrock** | `Amazon.BedrockRuntime` | 🔶 Official AWS | Yes - wrap SDK |
| **OpenRouter** | `OpenRouter.NET` | 🔶 3rd Party | Yes - wrap SDK |

#### Local Providers

| Provider | Package | Status | Adapter Needed |
|----------|---------|--------|----------------|
| **Ollama** | `OllamaSharp` v4+ | ✅ Implements IChatClient | No |
| **Llama.cpp** | `LLamaSharp` v0.4+ | 🔶 Native bindings | Yes - wrap API |
| **Windows ML** | `Microsoft.ML.OnnxRuntimeGenAI.DirectML` | ✅ Official | Yes - wrap API |

### 5. Provider-Specific Features

#### Feature Matrix

| Feature | OpenAI | Azure | Anthropic | Gemini | Bedrock | OpenRouter | Ollama | Llama.cpp | Windows ML |
|---------|--------|-------|-----------|--------|---------|------------|--------|-----------|------------|
| **Streaming** | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| **Function Calling** | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ⚠️ Limited | ⚠️ Limited | ⚠️ Limited |
| **Prompt Caching** | ✅ o1+ | ✅ o1+ | ✅ All | ❌ | Varies | Varies | ❌ | ❌ | ❌ |
| **Vision/Multimodal** | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ LLaVA | ⚠️ Via ONNX |
| **Structured Output** | ✅ | ✅ | ✅ | ✅ | ⚠️ | ⚠️ | ❌ | ❌ | ❌ |
| **Reasoning Models** | ✅ o1/o3 | ✅ o1/o3 | ❌ | ✅ 2.0 | ❌ | ✅ Pass-through | ❌ | ❌ | ❌ |

#### Caching Implementations

**Anthropic Prompt Caching:**
```csharp
// Anthropic.SDK built-in support
var message = new Message
{
    Model = "claude-3-5-sonnet-20241022",
    MaxTokens = 1024,
    System = new List<SystemMessage>
    {
        new SystemMessage("You are an AI assistant.", CacheControlEphemeral.Create())
    },
    Messages = new List<Message> { ... }
};
```

**OpenAI/Azure Prompt Caching:**
```csharp
// Automatic for o1/o3 models, enabled by default
// Caches first 1024+ tokens automatically
var options = new ChatOptions
{
    ModelId = "o1-2024-12-17"
    // Caching automatic - no config needed
};
```

**Microsoft.Extensions.AI Distributed Caching:**
```csharp
// Response caching (not prompt caching)
var cachedClient = new DistributedCachingChatClient(
    innerClient,
    distributedCache);
```

### 6. Middleware Pipeline

**Composable middleware using Microsoft.Extensions.AI patterns:**

```csharp
IChatClient client = new OpenAIClient(apiKey)
    .AsChatClient("gpt-4o")
    .UseFunctionInvocation()           // Auto function calling
    .UseDistributedCache(cache)         // Response caching
    .UseOpenTelemetry()                 // Telemetry
    .UseRateLimiting(rateLimiter);      // Rate limiting

// Custom middleware
public class RetryMiddleware : DelegatingChatClient
{
    public RetryMiddleware(IChatClient innerClient)
        : base(innerClient) { }

    public override async Task<ChatCompletion> CompleteAsync(
        IList<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken ct = default)
    {
        int retries = 3;
        while (retries > 0)
        {
            try
            {
                return await base.CompleteAsync(messages, options, ct);
            }
            catch (HttpRequestException) when (retries > 1)
            {
                retries--;
                await Task.Delay(1000 * (4 - retries));
            }
        }
        throw;
    }
}
```

### 7. Tool System

**Using AIFunction from Microsoft.Extensions.AI:**

```csharp
// Method-based tools
public class FileTools
{
    [Description("Read contents of a file")]
    public static async Task<string> ReadFile(
        [Description("Path to the file")] string path)
    {
        return await File.ReadAllTextAsync(path);
    }

    [Description("Write contents to a file")]
    public static async Task WriteFile(
        [Description("Path to the file")] string path,
        [Description("Content to write")] string content)
    {
        await File.WriteAllTextAsync(path, content);
    }
}

// Register with client
var chatOptions = new ChatOptions
{
    Tools =
    [
        AIFunctionFactory.Create(FileTools.ReadFile),
        AIFunctionFactory.Create(FileTools.WriteFile),
        AIFunctionFactory.Create(ExecuteBashCommand),
    ]
};
```

### 8. Hook System

**Event-driven control points:**

```csharp
public enum HookType
{
    PreToolUse,
    PostToolUse,
    PreModelCall,
    PostModelCall,
    PreAgentStop,
    PostAgentStop
}

public delegate Task<HookResult> HookCallback(
    object data,
    HookContext context);

public class AgentOptions
{
    public Dictionary<HookType, List<HookCallback>> Hooks { get; init; } = new();
    public int MaxTurns { get; init; } = 20;
    public float Temperature { get; init; } = 0.7f;
    public string? SystemPrompt { get; init; }
}

// Usage
var options = new AgentOptions
{
    Hooks = new()
    {
        [HookType.PreToolUse] = new List<HookCallback>
        {
            async (data, context) =>
            {
                if (data is FunctionCallContent tool &&
                    tool.Name == "ExecuteBashCommand")
                {
                    var args = tool.Arguments;
                    if (args?.ToString()?.Contains("rm -rf") == true)
                    {
                        return HookResult.Deny("Dangerous command blocked");
                    }
                }
                return HookResult.Allow();
            }
        }
    }
};
```

## Project Structure

```
OpenAgentMS/
├── src/
│   ├── OpenAgent.Core/                          # Core agent loop
│   │   ├── AgentLoop.cs                         # Main imperative loop
│   │   ├── AgentOptions.cs                      # Configuration
│   │   ├── AgentEvent.cs                        # Event types
│   │   ├── Hooks/
│   │   │   ├── HookType.cs
│   │   │   ├── HookCallback.cs
│   │   │   ├── HookResult.cs
│   │   │   └── HookContext.cs
│   │   └── Tools/
│   │       ├── IToolRegistry.cs
│   │       └── ToolRegistry.cs
│   │
│   ├── OpenAgent.Providers/                     # Provider abstraction
│   │   ├── ProviderFactory.cs                   # Create IChatClient
│   │   ├── ProviderType.cs                      # Enum of providers
│   │   ├── Adapters/                            # Wrap non-IChatClient
│   │   │   ├── AnthropicChatClient.cs
│   │   │   ├── BedrockChatClient.cs
│   │   │   ├── OpenRouterChatClient.cs
│   │   │   ├── LlamaSharpChatClient.cs
│   │   │   └── WindowsMLChatClient.cs
│   │   └── Middleware/
│   │       ├── RetryMiddleware.cs
│   │       ├── LoggingMiddleware.cs
│   │       └── CustomCachingMiddleware.cs
│   │
│   ├── OpenAgent.Tools/                         # Built-in tools
│   │   ├── FileTools.cs                         # Read, Write, Edit
│   │   ├── BashTools.cs                         # Execute commands
│   │   ├── WebTools.cs                          # Fetch, Search
│   │   └── CodeTools.cs                         # Analyze, Format
│   │
│   └── OpenAgent.Cli/                           # Console application
│       ├── Program.cs                           # Entry point
│       ├── Commands/                            # CLI commands
│       │   ├── ChatCommand.cs
│       │   ├── ConfigCommand.cs
│       │   └── ProviderCommand.cs
│       └── UI/
│           └── ConsoleRenderer.cs               # Display agent events
│
├── tests/
│   ├── OpenAgent.Core.Tests/
│   ├── OpenAgent.Providers.Tests/
│   └── OpenAgent.Integration.Tests/
│
├── samples/
│   ├── BasicChat/
│   ├── MultiProviderDemo/
│   ├── CustomTools/
│   └── LocalModels/
│
├── docs/
│   ├── ARCHITECTURE.md                          # This file
│   ├── PROVIDERS.md                             # Provider setup guide
│   ├── TOOLS.md                                 # Tool development guide
│   └── HOOKS.md                                 # Hook usage guide
│
├── ARCHITECTURE.md
├── README.md
├── OpenAgentMS.sln
└── .gitignore
```

## NuGet Dependencies

### Core Dependencies
```xml
<ItemGroup>
  <!-- Microsoft.Extensions.AI - THE unified abstraction -->
  <PackageReference Include="Microsoft.Extensions.AI" Version="9.10.2" />
  <PackageReference Include="Microsoft.Extensions.AI.Abstractions" Version="9.10.2" />

  <!-- Dependency Injection -->
  <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="9.0.0" />
  <PackageReference Include="Microsoft.Extensions.Hosting" Version="9.0.0" />

  <!-- Configuration -->
  <PackageReference Include="Microsoft.Extensions.Configuration" Version="9.0.0" />
  <PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="9.0.0" />
  <PackageReference Include="Microsoft.Extensions.Configuration.UserSecrets" Version="9.0.0" />

  <!-- Logging & Telemetry -->
  <PackageReference Include="Microsoft.Extensions.Logging" Version="9.0.0" />
  <PackageReference Include="OpenTelemetry" Version="1.10.0" />
  <PackageReference Include="OpenTelemetry.Exporter.Console" Version="1.10.0" />
</ItemGroup>
```

### Provider Packages
```xml
<ItemGroup>
  <!-- Cloud Providers -->
  <PackageReference Include="Microsoft.Extensions.AI.OpenAI" Version="9.10.1-preview.1.25521.4" />
  <PackageReference Include="Anthropic.SDK" Version="5.8.0" />
  <PackageReference Include="GeminiDotnet.Extensions.AI" Version="0.14.1" />
  <PackageReference Include="AWSSDK.BedrockRuntime" Version="3.7.0" />
  <PackageReference Include="OpenRouter.NET" Version="1.0.0" />

  <!-- Local Providers -->
  <PackageReference Include="OllamaSharp" Version="5.4.8" />
  <PackageReference Include="LLamaSharp" Version="0.4.0" />
  <PackageReference Include="LLama.Backend.Cpu" Version="0.4.0" />
  <PackageReference Include="Microsoft.ML.OnnxRuntimeGenAI.DirectML" Version="0.4.0" />
</ItemGroup>
```

## Implementation Phases

### Phase 1: Foundation (Week 1-2)
- [ ] Set up solution structure
- [ ] Implement core message types using Microsoft.Extensions.AI
- [ ] Create basic AgentLoop with OpenAI provider
- [ ] Implement streaming with IAsyncEnumerable
- [ ] Build simple console CLI

### Phase 2: Multi-Provider Support (Week 3-4)
- [ ] Implement ProviderFactory
- [ ] Add Azure OpenAI support
- [ ] Create Anthropic adapter with caching support
- [ ] Create Gemini adapter
- [ ] Add provider configuration system

### Phase 3: Tool System (Week 5-6)
- [ ] Implement IToolRegistry
- [ ] Create built-in tools (File, Bash, Web)
- [ ] Add function calling middleware
- [ ] Implement tool permission system

### Phase 4: Hooks & Control (Week 7-8)
- [ ] Implement hook callback system
- [ ] Add pre/post tool use hooks
- [ ] Create permission hooks
- [ ] Add agent lifecycle hooks

### Phase 5: Local Models (Week 9-10)
- [ ] Add Ollama support (OllamaSharp)
- [ ] Create LLamaSharp adapter
- [ ] Create Windows ML adapter
- [ ] Optimize for local inference

### Phase 6: Advanced Features (Week 11-12)
- [ ] Add AWS Bedrock support
- [ ] Add OpenRouter support
- [ ] Implement advanced caching strategies
- [ ] Add telemetry and observability
- [ ] Create comprehensive documentation

## Key Design Decisions

### ✅ DO: Use Microsoft.Extensions.AI
- **IChatClient** is the standard .NET abstraction
- Middleware pattern for composability
- Growing ecosystem support
- Official Microsoft backing

### ❌ DON'T: Use Microsoft Agent Framework
- Workflow graphs are wrong paradigm for imperative loop
- Too heavyweight for single-agent scenarios
- Adds unnecessary complexity

### ✅ DO: Build adapters for non-compliant providers
- Wrap Anthropic.SDK to expose IChatClient
- Wrap AWS Bedrock SDK to expose IChatClient
- Wrap LLamaSharp to expose IChatClient
- Maintains unified interface

### ✅ DO: Leverage provider-specific features
- Use Anthropic's native caching API
- Use OpenAI's o1 reasoning capabilities
- Use AWS Bedrock's Converse API
- Use local model optimizations

### ✅ DO: Maximize flexibility
- Support seamless model switching
- Allow multiple providers simultaneously
- Enable custom middleware injection
- Support both cloud and local models

## Configuration Example

```json
{
  "OpenAgent": {
    "DefaultProvider": "OpenAI",
    "MaxTurns": 20,
    "Temperature": 0.7,
    "Providers": {
      "OpenAI": {
        "ApiKey": "sk-...",
        "Model": "gpt-4o",
        "EnableCaching": true
      },
      "Anthropic": {
        "ApiKey": "sk-ant-...",
        "Model": "claude-3-5-sonnet-20241022",
        "CacheType": "FineGrained"
      },
      "AzureOpenAI": {
        "Endpoint": "https://your-resource.openai.azure.com/",
        "ApiKey": "...",
        "DeploymentName": "gpt-4o"
      },
      "Ollama": {
        "Endpoint": "http://localhost:11434",
        "Model": "llama3.1:8b"
      },
      "LlamaSharp": {
        "ModelPath": "C:/models/llama-3.1-8b.gguf",
        "ContextSize": 4096,
        "GpuLayers": 32
      }
    }
  }
}
```

## Usage Example

```csharp
using OpenAgent.Core;
using OpenAgent.Providers;
using Microsoft.Extensions.AI;

// Create provider
var chatClient = ProviderFactory.Create(
    ProviderType.Anthropic,
    new ProviderOptions
    {
        ApiKey = "sk-ant-...",
        Model = "claude-3-5-sonnet-20241022"
    })
    .UseFunctionInvocation()
    .UseLogging();

// Configure agent
var agentOptions = new AgentOptions
{
    MaxTurns = 20,
    Temperature = 0.7f,
    Hooks = new()
    {
        [HookType.PreToolUse] = new() { ValidateToolSafety }
    }
};

// Create agent
var agent = new AgentLoop(chatClient, toolRegistry, agentOptions);

// Execute
await foreach (var evt in agent.ExecuteAsync("Help me analyze this codebase"))
{
    switch (evt)
    {
        case AgentEvent.ModelResponse(var msg):
            Console.WriteLine($"Agent: {msg}");
            break;
        case AgentEvent.ExecutingTool(var name):
            Console.WriteLine($"Using tool: {name}");
            break;
        case AgentEvent.ToolResult(var name, var result):
            Console.WriteLine($"Tool {name} returned: {result}");
            break;
    }
}
```

## Benefits of This Architecture

1. **Flexibility**: Seamless switching between cloud and local models
2. **Provider Features**: Leverage unique capabilities (caching, reasoning models, etc.)
3. **Standard Interface**: Microsoft.Extensions.AI provides consistent API
4. **Composability**: Middleware pattern for cross-cutting concerns
5. **Extensibility**: Easy to add new providers via adapters
6. **Control**: Hook system for fine-grained permission and lifecycle management
7. **Performance**: Native streaming support, async throughout
8. **Local-First Option**: Run entirely offline with local models

## References

- [Microsoft.Extensions.AI Documentation](https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai)
- [Claude Agent SDK Architecture](https://www.anthropic.com/engineering/building-agents-with-the-claude-agent-sdk)
- [Anthropic.SDK NuGet](https://www.nuget.org/packages/Anthropic.SDK)
- [OllamaSharp GitHub](https://github.com/awaescher/OllamaSharp)
- [LLamaSharp Documentation](https://scisharp.github.io/LLamaSharp/)
- [Windows ML Overview](https://learn.microsoft.com/en-us/windows/ai/new-windows-ml/overview)
