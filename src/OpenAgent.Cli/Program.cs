using OpenAgent.Core;
using OpenAgent.Core.Tools;
using OpenAgent.Providers.Anthropic;
using OpenAgent.Tools;
using OpenAgent.Cli.Rendering;
using Spectre.Console;
using Microsoft.Extensions.Configuration;

namespace OpenAgent.Cli;

class Program
{
    static async Task Main(string[] args)
    {
        // Load configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddUserSecrets<Program>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        // Get API key
        var apiKey = configuration["Anthropic:ApiKey"]
            ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");

        if (string.IsNullOrEmpty(apiKey))
        {
            AnsiConsole.MarkupLine("[red]Error: Anthropic API key not found.[/]");
            AnsiConsole.MarkupLine("Set it via:");
            AnsiConsole.MarkupLine("  1. Environment variable: [cyan]ANTHROPIC_API_KEY[/]");
            AnsiConsole.MarkupLine("  2. User secrets: [cyan]dotnet user-secrets set \"Anthropic:ApiKey\" \"your-key\"[/]");
            AnsiConsole.MarkupLine("  3. appsettings.json: [cyan]\"Anthropic\": { \"ApiKey\": \"your-key\" }[/]");
            return;
        }

        // Initialize components
        var llmClient = new AnthropicLlmClient(
            apiKey,
            model: configuration["Anthropic:Model"] ?? "claude-sonnet-4-20250514");

        var toolRegistry = new ToolRegistry();
        toolRegistry.Register(new CalculatorTool());
        toolRegistry.Register(new GetTimeTool());

        var options = new AgentOptions
        {
            MaxTurns = 20,
            Temperature = 0.7f,
            MaxTokens = 4096,
            SystemPrompt = AgentOptions.DefaultSystemPrompt
        };

        var renderer = new ConsoleRenderer();
        renderer.RenderWelcome();

        // REPL loop
        var conversationHistory = new List<LlmMessage>();

        while (true)
        {
            // Get user input
            var input = AnsiConsole.Prompt(
                new TextPrompt<string>("[bold green]You:[/]")
                    .AllowEmpty());

            if (string.IsNullOrWhiteSpace(input))
            {
                continue;
            }

            // Handle commands
            var command = input.Trim().ToLower();
            if (command == "exit" || command == "quit")
            {
                renderer.RenderGoodbye();
                break;
            }

            if (command == "clear")
            {
                conversationHistory.Clear();
                AnsiConsole.Clear();
                renderer.RenderWelcome();
                AnsiConsole.MarkupLine("[dim]Conversation cleared.[/]");
                AnsiConsole.WriteLine();
                continue;
            }

            if (command == "help")
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[bold]Available commands:[/]");
                AnsiConsole.MarkupLine("  [cyan]exit/quit[/] - Exit the REPL");
                AnsiConsole.MarkupLine("  [cyan]clear[/] - Clear conversation history");
                AnsiConsole.MarkupLine("  [cyan]help[/] - Show this help message");
                AnsiConsole.WriteLine();
                continue;
            }

            // Create agent loop
            var agent = new AgentLoop(llmClient, toolRegistry, options);

            // Execute agent
            try
            {
                await foreach (var evt in agent.ExecuteAsync(conversationHistory, input))
                {
                    renderer.Render(evt);
                }

                AnsiConsole.WriteLine();
            }
            catch (Exception ex)
            {
                AnsiConsole.WriteException(ex);
                AnsiConsole.WriteLine();
            }
        }
    }
}
