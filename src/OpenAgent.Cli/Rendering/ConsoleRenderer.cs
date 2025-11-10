using OpenAgent.Core;
using Spectre.Console;

namespace OpenAgent.Cli.Rendering;

/// <summary>
/// Renders agent events to the console using Spectre.Console.
/// </summary>
public class ConsoleRenderer
{
    private bool _isFirstToken = true;
    private readonly Dictionary<string, ProgressTask> _activeTools = new();

    public void Render(AgentEvent evt)
    {
        switch (evt)
        {
            case AgentEvent.GatheringContext:
                // Silent - just internal state
                break;

            case AgentEvent.CallingModel:
                AnsiConsole.Write(new Markup("[bold cyan]Agent:[/] "));
                _isFirstToken = true;
                break;

            case AgentEvent.ModelResponse(var text, var isPartial):
                if (_isFirstToken)
                {
                    _isFirstToken = false;
                }
                AnsiConsole.Write(text);
                if (!isPartial)
                {
                    AnsiConsole.WriteLine();
                }
                break;

            case AgentEvent.ThinkingUpdate(var thinking):
                AnsiConsole.MarkupLine($"[dim]Thinking: {thinking.EscapeMarkup()}[/]");
                break;

            case AgentEvent.ExecutingTool(var name, var args):
                AnsiConsole.MarkupLine($"[yellow]→ Using tool: {name.EscapeMarkup()}[/]");
                break;

            case AgentEvent.ToolResult(var name, var result):
                var resultStr = result?.ToString() ?? "null";
                if (resultStr.Length > 100)
                {
                    resultStr = resultStr.Substring(0, 97) + "...";
                }
                AnsiConsole.MarkupLine($"[green]✓ Tool {name.EscapeMarkup()} completed[/]");
                break;

            case AgentEvent.ToolDenied(var name, var reason):
                AnsiConsole.MarkupLine($"[red]✗ Tool {name.EscapeMarkup()} denied: {reason.EscapeMarkup()}[/]");
                break;

            case AgentEvent.Completed(var finalMsg):
                AnsiConsole.WriteLine();
                break;

            case AgentEvent.MaxTurnsReached(var turns):
                AnsiConsole.MarkupLine($"[yellow]⚠ Maximum turns reached ({turns})[/]");
                break;

            case AgentEvent.Error(var ex, var msg):
                AnsiConsole.MarkupLine($"[red]✗ Error: {msg.EscapeMarkup()}[/]");
                if (ex != null)
                {
                    AnsiConsole.WriteException(ex);
                }
                break;
        }
    }

    public void RenderWelcome()
    {
        var rule = new Rule("[bold cyan]Open Agent MS - CLI[/]");
        rule.Style = Style.Parse("cyan");
        AnsiConsole.Write(rule);
        AnsiConsole.WriteLine();

        var panel = new Panel(
            "[yellow]Type your message and press Enter to chat with the agent.[/]\n" +
            "[dim]Type 'exit' or 'quit' to leave, 'clear' to reset conversation.[/]")
        {
            Border = BoxBorder.Rounded,
            BorderStyle = Style.Parse("dim")
        };
        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }

    public void RenderGoodbye()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[cyan]Goodbye! 👋[/]");
    }
}
