# Frontend Development Guide

This guide explains how to build frontends that consume the **OpenAgent.Core** agentic loop.

## Core Principle

**OpenAgent.Core is frontend-agnostic**. It:
- Yields `IAsyncEnumerable<AgentEvent>` for all outputs
- Has zero dependencies on UI frameworks, HTTP, or console I/O
- Works with any .NET frontend technology

## Event-Driven Architecture

### AgentEvent Types

All frontends consume the same event stream:

```csharp
public abstract record AgentEvent
{
    // Context gathering
    public record GatheringContext : AgentEvent;

    // LLM interaction
    public record CallingModel : AgentEvent;
    public record ModelResponse(ChatMessage Message) : AgentEvent;
    public record ThinkingUpdate(string Thinking) : AgentEvent;

    // Tool execution
    public record ExecutingTool(string Name, object? Args) : AgentEvent;
    public record ToolResult(string Name, object Result) : AgentEvent;
    public record ToolDenied(string Name, string Reason) : AgentEvent;

    // Completion
    public record Completed(ChatMessage FinalMessage) : AgentEvent;
    public record MaxTurnsReached : AgentEvent;
    public record Error(Exception Exception) : AgentEvent;
}
```

### Base Agent Consumption Pattern

```csharp
var agent = new AgentLoop(chatClient, toolRegistry, options);

await foreach (var evt in agent.ExecuteAsync(userPrompt))
{
    // Handle events based on frontend needs
    switch (evt)
    {
        case AgentEvent.ModelResponse(var msg):
            // Display message
            break;
        case AgentEvent.ExecutingTool(var name, var args):
            // Show tool indicator
            break;
        case AgentEvent.Completed(var finalMsg):
            // Finalize UI
            break;
    }
}
```

---

## Frontend 1: CLI/REPL

**Location**: `src/OpenAgent.Cli/`

**Use Case**: Command-line interface and REPL for terminal users

### Architecture

```csharp
// Program.cs
var rootCommand = new RootCommand("Open Agent MS CLI");

var chatCommand = new Command("chat", "Start an interactive chat");
chatCommand.SetHandler(async () =>
{
    var renderer = new ConsoleRenderer();
    var agent = CreateAgent();

    while (true)
    {
        Console.Write("\nYou: ");
        var input = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(input)) break;

        await foreach (var evt in agent.ExecuteAsync(input))
        {
            renderer.Render(evt);
        }
    }
});

rootCommand.Add(chatCommand);
await rootCommand.InvokeAsync(args);
```

### Console Rendering

```csharp
public class ConsoleRenderer
{
    public void Render(AgentEvent evt)
    {
        switch (evt)
        {
            case AgentEvent.CallingModel:
                Console.Write("Agent: ");
                break;

            case AgentEvent.ModelResponse(var msg):
                RenderMessage(msg);
                break;

            case AgentEvent.ThinkingUpdate(var thinking):
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"  [Thinking: {thinking}]");
                Console.ResetColor();
                break;

            case AgentEvent.ExecutingTool(var name, _):
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"  [Using tool: {name}]");
                Console.ResetColor();
                break;

            case AgentEvent.ToolResult(var name, var result):
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"  [Tool {name} completed]");
                Console.ResetColor();
                break;

            case AgentEvent.Completed(_):
                Console.WriteLine();
                break;

            case AgentEvent.Error(var ex):
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: {ex.Message}");
                Console.ResetColor();
                break;
        }
    }

    private void RenderMessage(ChatMessage msg)
    {
        foreach (var content in msg.Contents)
        {
            if (content is TextContent text)
            {
                Console.Write(text.Text);
            }
        }
    }
}
```

### Rich Terminal UI with Spectre.Console

```csharp
using Spectre.Console;

public class SpectreRenderer
{
    public async Task RenderStreamAsync(IAsyncEnumerable<AgentEvent> events)
    {
        await AnsiConsole.Status()
            .StartAsync("Thinking...", async ctx =>
            {
                await foreach (var evt in events)
                {
                    switch (evt)
                    {
                        case AgentEvent.ModelResponse(var msg):
                            AnsiConsole.MarkupLine($"[green]Agent:[/] {msg.Text}");
                            break;

                        case AgentEvent.ExecutingTool(var name, _):
                            ctx.Status($"Using tool: {name}");
                            break;
                    }
                }
            });
    }
}
```

### Dependencies

```xml
<ItemGroup>
  <PackageReference Include="System.CommandLine" Version="2.0.0-beta4.22272.1" />
  <PackageReference Include="Spectre.Console" Version="0.49.1" />
</ItemGroup>
```

---

## Frontend 2: WinUI3 Desktop

**Location**: `src/OpenAgent.WinUI/`

**Use Case**: Native Windows desktop application with modern UI

### Architecture (MVVM)

**ChatViewModel.cs**
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

public partial class ChatViewModel : ObservableObject
{
    private readonly IAgentSession _agentSession;

    [ObservableProperty]
    private string _inputText = "";

    [ObservableProperty]
    private bool _isBusy;

    public ObservableCollection<MessageViewModel> Messages { get; } = new();

    [RelayCommand]
    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(InputText)) return;

        var userMessage = InputText;
        InputText = "";

        // Add user message
        Messages.Add(new MessageViewModel
        {
            Text = userMessage,
            IsUser = true,
            Timestamp = DateTime.Now
        });

        // Add assistant placeholder
        var assistantMessage = new MessageViewModel
        {
            IsUser = false,
            Timestamp = DateTime.Now
        };
        Messages.Add(assistantMessage);

        IsBusy = true;

        try
        {
            await foreach (var evt in _agentSession.ExecuteAsync(userMessage))
            {
                // Dispatch to UI thread
                await DispatcherQueue.EnqueueAsync(() =>
                {
                    switch (evt)
                    {
                        case AgentEvent.ModelResponse(var msg):
                            assistantMessage.AppendText(GetTextFromMessage(msg));
                            break;

                        case AgentEvent.ThinkingUpdate(var thinking):
                            assistantMessage.SetThinking(thinking);
                            break;

                        case AgentEvent.ExecutingTool(var name, _):
                            assistantMessage.AddToolExecution(name);
                            break;

                        case AgentEvent.ToolResult(var name, var result):
                            assistantMessage.CompleteToolExecution(name, result);
                            break;

                        case AgentEvent.Completed(_):
                            assistantMessage.ClearThinking();
                            break;
                    }
                });
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
```

**MessageViewModel.cs**
```csharp
public partial class MessageViewModel : ObservableObject
{
    [ObservableProperty]
    private string _text = "";

    [ObservableProperty]
    private string? _thinking;

    [ObservableProperty]
    private bool _isUser;

    [ObservableProperty]
    private DateTime _timestamp;

    public ObservableCollection<ToolExecutionViewModel> Tools { get; } = new();

    public void AppendText(string text)
    {
        Text += text;
    }

    public void SetThinking(string thinking)
    {
        Thinking = thinking;
    }

    public void ClearThinking()
    {
        Thinking = null;
    }

    public void AddToolExecution(string toolName)
    {
        Tools.Add(new ToolExecutionViewModel
        {
            Name = toolName,
            IsExecuting = true
        });
    }

    public void CompleteToolExecution(string toolName, object result)
    {
        var tool = Tools.FirstOrDefault(t => t.Name == toolName);
        if (tool != null)
        {
            tool.IsExecuting = false;
            tool.Result = result.ToString();
        }
    }
}
```

**ChatPage.xaml**
```xml
<Page x:Class="OpenAgent.WinUI.Views.ChatPage"
      xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- Messages List -->
        <ListView ItemsSource="{x:Bind ViewModel.Messages}"
                  Grid.Row="0">
            <ListView.ItemTemplate>
                <DataTemplate x:DataType="local:MessageViewModel">
                    <Grid Padding="12">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="Auto"/>
                            <ColumnDefinition Width="*"/>
                        </Grid.ColumnDefinitions>

                        <!-- Avatar -->
                        <PersonPicture Grid.Column="0"
                                      Width="40" Height="40"
                                      Margin="0,0,12,0"/>

                        <!-- Message Content -->
                        <StackPanel Grid.Column="1">
                            <TextBlock Text="{x:Bind Text}"
                                      TextWrapping="Wrap"/>

                            <!-- Thinking Indicator -->
                            <TextBlock Text="{x:Bind Thinking}"
                                      Foreground="{ThemeResource SystemColorGrayText}"
                                      Visibility="{x:Bind Thinking, Converter={StaticResource NullToCollapsedConverter}}"/>

                            <!-- Tool Executions -->
                            <ItemsRepeater ItemsSource="{x:Bind Tools}">
                                <ItemsRepeater.ItemTemplate>
                                    <DataTemplate x:DataType="local:ToolExecutionViewModel">
                                        <Border Background="{ThemeResource CardBackgroundFillColorDefaultBrush}"
                                               Padding="8" Margin="0,4">
                                            <StackPanel Orientation="Horizontal">
                                                <ProgressRing IsActive="{x:Bind IsExecuting}"
                                                            Width="16" Height="16"
                                                            Margin="0,0,8,0"/>
                                                <TextBlock Text="{x:Bind Name}"/>
                                            </StackPanel>
                                        </Border>
                                    </DataTemplate>
                                </ItemsRepeater.ItemTemplate>
                            </ItemsRepeater>
                        </StackPanel>
                    </Grid>
                </DataTemplate>
            </ListView.ItemTemplate>
        </ListView>

        <!-- Input Box -->
        <Grid Grid.Row="1" Padding="12">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="Auto"/>
            </Grid.ColumnDefinitions>

            <TextBox Text="{x:Bind ViewModel.InputText, Mode=TwoWay}"
                    PlaceholderText="Type a message..."
                    Grid.Column="0"/>

            <Button Command="{x:Bind ViewModel.SendMessageCommand}"
                   Content="Send"
                   IsEnabled="{x:Bind ViewModel.IsBusy, Converter={StaticResource InverseBoolConverter}}"
                   Grid.Column="1"
                   Margin="8,0,0,0"/>
        </Grid>
    </Grid>
</Page>
```

### Dependencies

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.WindowsAppSDK" Version="1.6.250527000" />
  <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.0" />
  <PackageReference Include="CommunityToolkit.WinUI.Controls.Markdown" Version="8.2.0" />
  <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="9.0.0" />
</ItemGroup>
```

---

## Frontend 3: REST API

**Location**: `src/OpenAgent.Api/`

**Use Case**: HTTP API for web/mobile clients

### ASP.NET Core Controller

**ChatController.cs**
```csharp
[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IAgentSessionManager _sessionManager;
    private readonly ILogger<ChatController> _logger;

    // POST /api/chat
    [HttpPost]
    public async Task<ActionResult<ChatResponse>> Chat(
        [FromBody] ChatRequest request,
        CancellationToken ct)
    {
        var session = _sessionManager.GetOrCreate(request.SessionId);

        var messages = new List<MessageDto>();
        var toolExecutions = new List<ToolExecutionDto>();

        await foreach (var evt in session.ExecuteAsync(request.Message, ct))
        {
            switch (evt)
            {
                case AgentEvent.ModelResponse(var msg):
                    messages.Add(new MessageDto
                    {
                        Role = msg.Role.ToString(),
                        Content = GetTextContent(msg)
                    });
                    break;

                case AgentEvent.ExecutingTool(var name, var args):
                    toolExecutions.Add(new ToolExecutionDto
                    {
                        Name = name,
                        Status = "executing"
                    });
                    break;

                case AgentEvent.ToolResult(var name, var result):
                    var tool = toolExecutions.FirstOrDefault(t => t.Name == name);
                    if (tool != null)
                    {
                        tool.Status = "completed";
                        tool.Result = result;
                    }
                    break;
            }
        }

        return new ChatResponse
        {
            SessionId = request.SessionId,
            Messages = messages,
            ToolExecutions = toolExecutions
        };
    }

    // POST /api/chat/stream (Server-Sent Events)
    [HttpPost("stream")]
    public async Task StreamChat(
        [FromBody] ChatRequest request,
        CancellationToken ct)
    {
        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        var session = _sessionManager.GetOrCreate(request.SessionId);

        await foreach (var evt in session.ExecuteAsync(request.Message, ct))
        {
            var eventData = evt switch
            {
                AgentEvent.ModelResponse(var msg) => new
                {
                    type = "message",
                    data = GetTextContent(msg)
                },
                AgentEvent.ThinkingUpdate(var thinking) => new
                {
                    type = "thinking",
                    data = thinking
                },
                AgentEvent.ExecutingTool(var name, _) => new
                {
                    type = "tool_start",
                    data = name
                },
                AgentEvent.ToolResult(var name, var result) => new
                {
                    type = "tool_end",
                    data = new { name, result }
                },
                AgentEvent.Completed(_) => new
                {
                    type = "done",
                    data = (object?)null
                },
                _ => null
            };

            if (eventData != null)
            {
                var json = JsonSerializer.Serialize(eventData);
                await Response.WriteAsync($"data: {json}\n\n", ct);
                await Response.Body.FlushAsync(ct);
            }
        }
    }
}
```

**Session Manager**
```csharp
public class AgentSessionManager : IAgentSessionManager
{
    private readonly ConcurrentDictionary<string, IAgentSession> _sessions = new();
    private readonly IServiceProvider _serviceProvider;

    public IAgentSession GetOrCreate(string sessionId)
    {
        return _sessions.GetOrAdd(sessionId, _ =>
        {
            var scope = _serviceProvider.CreateScope();
            var chatClient = scope.ServiceProvider.GetRequiredService<IChatClient>();
            var toolRegistry = scope.ServiceProvider.GetRequiredService<IToolRegistry>();
            var options = scope.ServiceProvider.GetRequiredService<AgentOptions>();

            return new AgentSession(sessionId, chatClient, toolRegistry, options);
        });
    }

    public void Remove(string sessionId)
    {
        _sessions.TryRemove(sessionId, out _);
    }
}
```

### Dependencies

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.AspNetCore.App" />
  <PackageReference Include="Swashbuckle.AspNetCore" Version="7.2.0" />
</ItemGroup>
```

---

## Frontend 4: MCP Server

**Location**: `src/OpenAgent.Mcp/`

**Use Case**: Expose agent tools via Model Context Protocol

### MCP Server Implementation

```csharp
using System.IO.Pipelines;
using System.Text.Json;

public class McpServer
{
    private readonly IToolRegistry _toolRegistry;
    private readonly IChatClient _chatClient;

    public async Task StartAsync(CancellationToken ct)
    {
        // Read from stdin, write to stdout (MCP protocol)
        var pipeReader = PipeReader.Create(Console.OpenStandardInput());
        var pipeWriter = PipeWriter.Create(Console.OpenStandardOutput());

        await foreach (var request in ReadRequestsAsync(pipeReader, ct))
        {
            var response = await HandleRequestAsync(request, ct);
            await WriteResponseAsync(pipeWriter, response, ct);
        }
    }

    private async Task<McpResponse> HandleRequestAsync(
        McpRequest request,
        CancellationToken ct)
    {
        return request.Method switch
        {
            "tools/list" => ListTools(),
            "tools/call" => await CallToolAsync(request.Params, ct),
            "resources/list" => ListResources(),
            "resources/read" => await RunAgentAsync(request.Params, ct),
            _ => new McpResponse { Error = "Unknown method" }
        };
    }

    private McpResponse ListTools()
    {
        var tools = _toolRegistry.GetAll().Select(t => new
        {
            name = t.Name,
            description = t.Description,
            inputSchema = t.InputSchema
        });

        return new McpResponse { Result = tools };
    }

    private async Task<McpResponse> CallToolAsync(
        JsonElement paramsElem,
        CancellationToken ct)
    {
        var toolName = paramsElem.GetProperty("name").GetString();
        var args = paramsElem.GetProperty("arguments");

        var result = await _toolRegistry.ExecuteAsync(toolName!, args, ct);

        return new McpResponse
        {
            Result = new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = result.ToString()
                    }
                }
            }
        };
    }

    private McpResponse ListResources()
    {
        return new McpResponse
        {
            Result = new
            {
                resources = new[]
                {
                    new
                    {
                        uri = "agent://chat",
                        name = "Open Agent Chat",
                        description = "Run the Open Agent to answer questions"
                    }
                }
            }
        };
    }

    private async Task<McpResponse> RunAgentAsync(
        JsonElement paramsElem,
        CancellationToken ct)
    {
        var uri = paramsElem.GetProperty("uri").GetString();
        if (uri != "agent://chat")
        {
            return new McpResponse { Error = "Unknown resource" };
        }

        var prompt = paramsElem.GetProperty("prompt").GetString();
        var agent = new AgentLoop(_chatClient, _toolRegistry, new AgentOptions());

        var textBuilder = new StringBuilder();

        await foreach (var evt in agent.ExecuteAsync(prompt!, ct))
        {
            if (evt is AgentEvent.ModelResponse(var msg))
            {
                textBuilder.Append(GetTextContent(msg));
            }
        }

        return new McpResponse
        {
            Result = new
            {
                contents = new[]
                {
                    new
                    {
                        uri,
                        mimeType = "text/plain",
                        text = textBuilder.ToString()
                    }
                }
            }
        };
    }
}
```

### Dependencies

```xml
<ItemGroup>
  <PackageReference Include="System.IO.Pipelines" Version="9.0.0" />
</ItemGroup>
```

---

## Choosing a Frontend

| Frontend | Best For | Complexity | Platform |
|----------|----------|------------|----------|
| **CLI** | Developers, automation, scripting | Low | Cross-platform |
| **WinUI3** | Native Windows users, offline-first | Medium | Windows 10+ |
| **REST API** | Web/mobile clients, microservices | Medium | Cloud/server |
| **MCP Server** | Tool integration, IDE extensions | High | Cross-platform |

## Next Steps

1. **Start with CLI**: Simplest way to test the core
2. **Add REST API**: Enable web/mobile clients
3. **Build WinUI3**: For native Windows experience
4. **Implement MCP**: For advanced tool integration

All frontends share the same core agent logic and can be developed in parallel!
