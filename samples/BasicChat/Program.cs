using Microsoft.Extensions.AI;
using OpenAgent.Core;

namespace BasicChat;

/// <summary>
/// Basic chat example demonstrating the agent loop with OpenAI.
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Open Agent MS - Basic Chat Example ===\n");

        // Get API key from environment or prompt user
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            Console.Write("Enter your OpenAI API key: ");
            apiKey = Console.ReadLine();
        }

        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("Error: API key is required.");
            return;
        }

        // TODO: Replace with actual OpenAgent implementation once available
        Console.WriteLine("Creating OpenAI chat client...");

        // This is a placeholder - will be replaced with actual OpenAgent.Core implementation
        // var chatClient = ProviderFactory.Create(
        //     ProviderType.OpenAI,
        //     new ProviderOptions { ApiKey = apiKey, Model = "gpt-4o" });
        //
        // var agent = new AgentLoop(
        //     chatClient,
        //     toolRegistry,
        //     new AgentOptions { MaxTurns = 20, Temperature = 0.7f });
        //
        // await foreach (var evt in agent.ExecuteAsync("Hello! Can you help me?"))
        // {
        //     switch (evt)
        //     {
        //         case AgentEvent.ModelResponse(var msg):
        //             Console.WriteLine($"Agent: {msg}");
        //             break;
        //         case AgentEvent.ExecutingTool(var name):
        //             Console.WriteLine($"[Using tool: {name}]");
        //             break;
        //     }
        // }

        Console.WriteLine("\nNote: Full implementation coming soon!");
        Console.WriteLine("This project structure is ready for development.");
        Console.WriteLine("\nNext steps:");
        Console.WriteLine("1. Implement OpenAgent.Core.AgentLoop");
        Console.WriteLine("2. Implement OpenAgent.Providers.ProviderFactory");
        Console.WriteLine("3. Add built-in tools to OpenAgent.Tools");
        Console.WriteLine("4. Build and run this sample!");
    }
}
