# Open Agent MS

**Windows C# implementation of an Open Agent using an imperative agent loop paradigm with unified multi-provider LLM abstraction.**

## Overview

Open Agent MS is a .NET implementation of an agentic AI system inspired by [Open-Agent.io](https://open-agent.io/), designed specifically for Windows with C# while maintaining maximum flexibility for LLM providers and deployment scenarios.

### Key Design Principles

- **Imperative Agent Loop**: Uses the "gather → act → verify → repeat" pattern (like Claude Agent SDK)
- **Frontend-Agnostic Core**: Business logic isolated from UI, enabling CLI, WinUI3, REST API, and MCP frontends
- **Multi-Provider Support**: Seamlessly switch between cloud (OpenAI, Anthropic, Gemini, AWS Bedrock) and local (Ollama, Llama.cpp, Windows ML) models
- **No Workflow Graphs**: Pure imperative control loop, NOT Microsoft Agent Framework's graph-based orchestration

## Quick Start

### Prerequisites

- .NET 9.0 SDK
- Anthropic API key (or other provider credentials)

### Installation

1. Clone the repository:
```bash
git clone https://github.com/witt3rd/open-agent-ms.git
cd open-agent-ms
```

2. Set your Anthropic API key:

**Option 1 - User Secrets (Recommended for development):**
```bash
cd src/OpenAgent.Cli
dotnet user-secrets init
dotnet user-secrets set "Anthropic:ApiKey" "sk-ant-your-key-here"
```

**Option 2 - Environment Variable:**
```bash
# Windows (PowerShell)
$env:ANTHROPIC_API_KEY="sk-ant-your-key-here"

# Windows (CMD)
set ANTHROPIC_API_KEY=sk-ant-your-key-here

# Linux/Mac
export ANTHROPIC_API_KEY="sk-ant-your-key-here"
```

**Option 3 - appsettings.json (Not recommended - don't commit!):**
```json
{
  "Anthropic": {
    "ApiKey": "sk-ant-your-key-here",
    "Model": "claude-sonnet-4-20250514"
  }
}
```

3. Run the CLI:
```bash
cd src/OpenAgent.Cli
dotnet run
```

## Usage

### CLI REPL

Start an interactive chat session:

```bash
dotnet run --project src/OpenAgent.Cli
```

**Commands:**
- Type your message and press Enter to chat
- `help` - Show available commands
- `clear` - Clear conversation history
- `exit` or `quit` - Exit the REPL

**Example Session:**
```
> You: What's 15 + 27?

Agent: → Using tool: calculator
        ✓ Tool calculator completed
        15 + 27 = 42

> You: What time is it in UTC?

Agent: → Using tool: get_current_time
        ✓ Tool get_current_time completed
        Current time (UTC): 2025-01-10 15:30:45
```

### Available Tools

The agent currently has access to:

- **calculator** - Basic arithmetic (add, subtract, multiply, divide)
- **get_current_time** - Get current date/time with timezone support

## Architecture

```
┌──────────── FRONTENDS ────────────┐
│  CLI  │  WinUI3  │  API  │  MCP  │
└────────────────┬──────────────────┘
                 │
         ┌───────▼───────┐
         │ OpenAgent.Core │  Frontend-agnostic
         │  (Agent Loop)  │  IAsyncEnumerable<AgentEvent>
         └───────┬───────┘
                 │
    ┌────────────▼────────────┐
    │  OpenAgent.Providers    │  Multi-provider abstraction
    │  (ILlmClient)           │
    └────────────┬────────────┘
                 │
    ┌────────────▼────────────┐
    │  Cloud: OpenAI, Claude  │
    │  Local: Ollama, Llama   │
    └─────────────────────────┘
```

### Components

- **OpenAgent.Core** - Imperative agent loop, hooks, tool registry (frontend-agnostic)
- **OpenAgent.Providers** - LLM provider adapters (Anthropic, OpenAI, Ollama, etc.)
- **OpenAgent.Tools** - Built-in tools (calculator, time, file operations)
- **OpenAgent.Cli** - Console REPL interface

### Event-Driven Communication

The core emits `AgentEvent` types that frontends consume:

```csharp
await foreach (var evt in agent.ExecuteAsync(userPrompt))
{
    switch (evt)
    {
        case AgentEvent.ModelResponse(var text):
            // Display agent's text response
        case AgentEvent.ExecutingTool(var name, var args):
            // Show tool execution indicator
        case AgentEvent.ToolResult(var name, var result):
            // Display tool completion
        case AgentEvent.Completed(var finalMsg):
            // Finalize UI
    }
}
```

## Documentation

- **[ARCHITECTURE.md](ARCHITECTURE.md)** - Comprehensive architecture design and implementation guide
- **[docs/PROVIDERS.md](docs/PROVIDERS.md)** - Provider configuration and setup (OpenAI, Anthropic, Gemini, Ollama, etc.)
- **[docs/FRONTENDS.md](docs/FRONTENDS.md)** - Building frontends (CLI, WinUI3, REST API, MCP)

## Supported Providers

### Cloud Providers
- ✅ **Anthropic Claude** (Sonnet 4.5, Haiku 4.5, Opus 4) - Implemented
- ⏳ OpenAI (GPT-4o, o1, o3-mini)
- ⏳ Azure OpenAI
- ⏳ Google Gemini (2.0 Flash, 1.5 Pro)
- ⏳ AWS Bedrock
- ⏳ OpenRouter (unified access to 100+ models)

### Local Providers
- ⏳ Ollama (Llama 3.1, Qwen, Mistral)
- ⏳ Llama.cpp (LLamaSharp)
- ⏳ Windows ML (ONNX Runtime with DirectML)

## Current Status

**✅ Implemented:**
- Core agent loop with imperative control flow
- Anthropic Claude provider (Sonnet 4.5)
- Tool system with calculator and time tools
- Hook system for pre/post tool execution
- CLI REPL with Spectre.Console

**🚧 In Progress:**
- Additional tool implementations (file, web, code)
- OpenAI provider adapter
- Session management for stateful frontends

**📋 Planned:**
- WinUI3 desktop application
- REST API with Server-Sent Events
- MCP Server for tool integration
- Local model support (Ollama, LLamaSharp)
- Advanced caching strategies
- Multi-agent orchestration

## Development

### Building from Source

```bash
# Build all projects
dotnet build OpenAgentMS.sln

# Run tests
dotnet test

# Run CLI
dotnet run --project src/OpenAgent.Cli
```

### Project Structure

```
src/
├── OpenAgent.Core/          # ✅ Core agent loop (frontend-agnostic)
├── OpenAgent.Providers/     # ✅ LLM provider adapters
├── OpenAgent.Tools/         # ✅ Built-in tools
├── OpenAgent.Cli/           # ✅ Console REPL
├── OpenAgent.WinUI/         # ⏳ WinUI3 desktop app
├── OpenAgent.Api/           # ⏳ REST API
└── OpenAgent.Mcp/           # ⏳ MCP server
```

## Contributing

Contributions welcome! Please see implementation phases in [ARCHITECTURE.md](ARCHITECTURE.md).

## License

[Add license information]

## Acknowledgments

- Inspired by [Open-Agent.io](https://open-agent.io/)
- Uses [Anthropic SDK for .NET](https://github.com/tghamm/Anthropic.SDK)
- Built with [Spectre.Console](https://spectreconsole.net/)

---

**Note**: This is an early implementation. The architecture prioritizes clean separation of concerns and extensibility over feature completeness.
