# Provider Configuration Guide

This guide shows how to configure and use different LLM providers with Open Agent MS.

## Unified Interface: IChatClient

All providers implement `Microsoft.Extensions.AI.IChatClient`, providing a consistent interface:

```csharp
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
}
```

## Cloud Providers

### OpenAI

**Package**: `Microsoft.Extensions.AI.OpenAI` (official)

**Configuration**:
```json
{
  "Providers": {
    "OpenAI": {
      "ApiKey": "sk-...",
      "Model": "gpt-4o",
      "Organization": "org-..." // optional
    }
  }
}
```

**Code**:
```csharp
using Microsoft.Extensions.AI;
using OpenAI;

var client = new OpenAIClient(apiKey)
    .AsChatClient("gpt-4o");
```

**Features**:
- ✅ Streaming
- ✅ Function calling
- ✅ Vision (gpt-4o, gpt-4-turbo)
- ✅ Prompt caching (o1/o3 models)
- ✅ Reasoning models (o1, o3-mini)
- ✅ Structured outputs

**Models**:
- `gpt-4o` - Latest multimodal model
- `gpt-4o-mini` - Fast, cost-effective
- `o1-2024-12-17` - Reasoning with prompt caching
- `o3-mini-2025-01-31` - Latest reasoning model
- `gpt-4-turbo` - Previous generation

---

### Azure OpenAI

**Package**: `Microsoft.Extensions.AI.OpenAI` (official)

**Configuration**:
```json
{
  "Providers": {
    "AzureOpenAI": {
      "Endpoint": "https://your-resource.openai.azure.com/",
      "ApiKey": "...",
      "DeploymentName": "gpt-4o",
      "ApiVersion": "2024-10-21"
    }
  }
}
```

**Code**:
```csharp
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;

var client = new AzureOpenAIClient(
    new Uri(endpoint),
    new AzureKeyCredential(apiKey))
    .AsChatClient(deploymentName);
```

**Features**: Same as OpenAI

---

### Anthropic (Claude)

**Package**: `Anthropic.SDK` v5.8+ (unofficial but feature-complete)

**Configuration**:
```json
{
  "Providers": {
    "Anthropic": {
      "ApiKey": "sk-ant-...",
      "Model": "claude-3-5-sonnet-20241022",
      "CacheType": "FineGrained" // or "AutomaticToolsAndSystem"
    }
  }
}
```

**Code**:
```csharp
using Anthropic.SDK;
// Needs adapter to IChatClient

var anthropicClient = new AnthropicClient(apiKey);

// Prompt caching example
var message = new Message
{
    Model = "claude-3-5-sonnet-20241022",
    MaxTokens = 1024,
    System = new List<SystemMessage>
    {
        new SystemMessage(
            "You are an AI assistant.",
            CacheControlEphemeral.Create())
    },
    Messages = new List<Message> { ... }
};
```

**Features**:
- ✅ Streaming
- ✅ Function calling
- ✅ Vision
- ✅ Prompt caching (all models)
- ✅ Extended thinking
- ❌ Reasoning models (not yet)

**Models**:
- `claude-3-5-sonnet-20241022` - Most capable
- `claude-3-5-haiku-20241022` - Fast, cost-effective
- `claude-3-opus-20240229` - Previous flagship

**Caching Note**: Caches expire after 5 minutes (or 1 hour for extended TTL). Use `CacheControlEphemeral.Create()` on system messages and tool definitions.

---

### Google Gemini

**Package**: `GeminiDotnet.Extensions.AI` (3rd party)

**Configuration**:
```json
{
  "Providers": {
    "Gemini": {
      "ApiKey": "...",
      "Model": "gemini-2.0-flash-exp"
    }
  }
}
```

**Code**:
```csharp
using GeminiDotnet;

var client = new GeminiChatClient(apiKey, modelId);
```

**Features**:
- ✅ Streaming
- ✅ Function calling
- ✅ Vision
- ✅ Reasoning (Gemini 2.0)
- ❌ Prompt caching (not in .NET SDK yet)

**Models**:
- `gemini-2.0-flash-exp` - Latest experimental
- `gemini-1.5-pro` - Production ready
- `gemini-1.5-flash` - Fast inference

---

### AWS Bedrock

**Package**: `AWSSDK.BedrockRuntime` (official)

**Configuration**:
```json
{
  "Providers": {
    "Bedrock": {
      "Region": "us-east-1",
      "Model": "anthropic.claude-3-5-sonnet-20241022-v2:0",
      "AccessKey": "...", // or use IAM role
      "SecretKey": "..."
    }
  }
}
```

**Code**:
```csharp
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
// Needs adapter to IChatClient

var client = new AmazonBedrockRuntimeClient(
    RegionEndpoint.USEast1);

var request = new ConverseRequest
{
    ModelId = "anthropic.claude-3-5-sonnet-20241022-v2:0",
    Messages = messages
};

var response = await client.ConverseAsync(request);
```

**Features**:
- ✅ Streaming (ConverseStream API)
- ✅ Function calling (tool streaming in Claude 4.x)
- ✅ Vision (model-dependent)
- ⚠️ Prompt caching (depends on model)

**Available Models**:
- Anthropic Claude (3.x, 4.x)
- Amazon Titan
- Meta Llama
- Mistral AI
- Cohere Command

---

### OpenRouter

**Package**: `OpenRouter.NET` (3rd party)

**Configuration**:
```json
{
  "Providers": {
    "OpenRouter": {
      "ApiKey": "sk-or-...",
      "Model": "anthropic/claude-3.5-sonnet",
      "SiteUrl": "https://myapp.com", // optional
      "SiteName": "MyApp" // optional
    }
  }
}
```

**Code**:
```csharp
using OpenRouter.NET;
// Needs adapter to IChatClient

var client = new OpenRouterClient(apiKey);
var request = new ChatCompletionRequest
{
    Model = "anthropic/claude-3.5-sonnet",
    Messages = messages
};
```

**Features**:
- ✅ Streaming
- ✅ Function calling
- ✅ Unified access to 100+ models
- ✅ Automatic fallbacks
- ✅ Cost optimization

**Benefits**: Single API for OpenAI, Anthropic, Google, Meta, Mistral, and more

---

## Local Providers

### Ollama

**Package**: `OllamaSharp` v5.4+ (implements IChatClient directly!)

**Configuration**:
```json
{
  "Providers": {
    "Ollama": {
      "Endpoint": "http://localhost:11434",
      "Model": "llama3.1:8b"
    }
  }
}
```

**Code**:
```csharp
using OllamaSharp;
using Microsoft.Extensions.AI;

var client = new OllamaApiClient(
    new Uri("http://localhost:11434"),
    "llama3.1:8b");

// OllamaSharp v4+ implements IChatClient directly!
IChatClient chatClient = client.AsChatClient();
```

**Features**:
- ✅ Streaming
- ⚠️ Function calling (limited, model-dependent)
- ✅ Vision (with LLaVA models)
- ✅ Embeddings
- ❌ Prompt caching

**Popular Models**:
- `llama3.1:8b` - Meta Llama 3.1 8B
- `llama3.1:70b` - Larger, more capable
- `qwen2.5:32b` - Alibaba's multilingual model
- `llava:13b` - Vision model
- `mistral:7b` - Mistral AI

**Setup**:
1. Install Ollama: https://ollama.com/download
2. Pull a model: `ollama pull llama3.1:8b`
3. Verify: `ollama list`

---

### Llama.cpp (LLamaSharp)

**Package**: `LLamaSharp` v0.4+ and `LLama.Backend.Cpu` (or Cuda11/Cuda12)

**Configuration**:
```json
{
  "Providers": {
    "LlamaSharp": {
      "ModelPath": "C:/models/llama-3.1-8b-instruct.Q4_K_M.gguf",
      "ContextSize": 4096,
      "GpuLayers": 32,
      "Seed": 1337
    }
  }
}
```

**Code**:
```csharp
using LLama;
using LLama.Common;
// Needs adapter to IChatClient

var parameters = new ModelParams(modelPath)
{
    ContextSize = 4096,
    GpuLayerCount = 32
};

using var model = LLamaWeights.LoadFromFile(parameters);
using var context = model.CreateContext(parameters);
var executor = new InteractiveExecutor(context);

var session = new ChatSession(executor);
```

**Features**:
- ✅ Streaming
- ⚠️ Function calling (via prompt engineering)
- ✅ Vision (with LLaVA GGUF models)
- ✅ CPU and GPU acceleration
- ❌ Prompt caching

**GPU Backends**:
- `LLama.Backend.Cpu` - CPU only
- `LLama.Backend.Cuda11` - NVIDIA CUDA 11
- `LLama.Backend.Cuda12` - NVIDIA CUDA 12
- `LLama.Backend.Metal` - Apple Silicon (experimental)

**Model Sources**:
- Hugging Face: https://huggingface.co/models?library=gguf
- TheBloke's quantized models
- Recommended format: GGUF (Q4_K_M or Q5_K_M for balance)

---

### Windows ML (ONNX Runtime)

**Package**: `Microsoft.ML.OnnxRuntimeGenAI.DirectML` v0.4+

**Configuration**:
```json
{
  "Providers": {
    "WindowsML": {
      "ModelPath": "C:/models/phi-3-mini-int4-directml",
      "MaxLength": 2048
    }
  }
}
```

**Code**:
```csharp
using Microsoft.ML.OnnxRuntimeGenAI;
// Needs adapter to IChatClient

using var model = new Model(modelPath);
using var tokenizer = new Tokenizer(model);

var generatorParams = new GeneratorParams(model);
generatorParams.SetSearchOption("max_length", 2048);

using var generator = new Generator(model, generatorParams);
```

**Features**:
- ✅ Streaming
- ⚠️ Function calling (via prompt engineering)
- ⚠️ Vision (Phi-3-vision ONNX models)
- ✅ DirectML GPU acceleration (all vendors)
- ❌ Prompt caching

**Supported Models**:
- Phi-3 (Mini, Small, Medium)
- Phi-3-Vision
- Llama 2, Llama 3
- Gemma
- Mistral

**DirectML**: Hardware acceleration across NVIDIA, AMD, Intel GPUs via DirectX 12

**Model Sources**:
- Azure AI Studio model catalog
- Hugging Face with ONNX export
- Pre-optimized DirectML models

---

## Provider Comparison

| Feature | OpenAI | Azure | Anthropic | Gemini | Bedrock | OpenRouter | Ollama | Llama.cpp | Windows ML |
|---------|--------|-------|-----------|--------|---------|------------|--------|-----------|------------|
| **Streaming** | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| **Function Calling** | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ⚠️ | ⚠️ | ⚠️ |
| **Prompt Caching** | ✅ o1+ | ✅ o1+ | ✅ All | ❌ | Varies | Varies | ❌ | ❌ | ❌ |
| **Vision** | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ LLaVA | ✅ LLaVA | ⚠️ |
| **Reasoning Models** | ✅ | ✅ | ❌ | ✅ 2.0 | ❌ | ✅ | ❌ | ❌ | ❌ |
| **Cost** | $$ | $$ | $$ | $ | $$ | $ | Free | Free | Free |
| **Latency** | Low | Low | Low | Low | Low | Med | Med-High | Med-High | Med |
| **Privacy** | Cloud | Cloud | Cloud | Cloud | Cloud | Cloud | Local | Local | Local |

---

## Switching Between Providers

Thanks to `Microsoft.Extensions.AI`, switching providers is seamless:

```csharp
// Configuration-driven provider selection
var providerType = config["OpenAgent:DefaultProvider"];

IChatClient client = providerType switch
{
    "OpenAI" => CreateOpenAIClient(config),
    "Anthropic" => CreateAnthropicClient(config),
    "Ollama" => CreateOllamaClient(config),
    _ => throw new NotSupportedException()
};

// Same agent loop works with any provider!
var agent = new AgentLoop(client, toolRegistry, options);
```

---

## Provider-Specific Optimizations

### Anthropic Prompt Caching

Cache system prompts and tool definitions:

```csharp
var systemPrompt = new SystemMessage(
    "You are an AI assistant with access to file system tools...",
    CacheControlEphemeral.Create() // Cache this!
);
```

### OpenAI o1 Reasoning

Enable extended reasoning:

```csharp
var options = new ChatOptions
{
    ModelId = "o1-2024-12-17",
    MaxOutputTokens = 4096, // Includes reasoning tokens
    Temperature = 1.0 // Fixed for o1
};
```

### Ollama GPU Acceleration

Ensure GPU layers are loaded:

```bash
ollama run llama3.1:8b --gpu-layers 35
```

### LLamaSharp GPU Offloading

```csharp
var parameters = new ModelParams(modelPath)
{
    GpuLayerCount = 35, // Offload 35 layers to GPU
    UseMemorymap = true, // Faster loading
    UseMemoryLock = true // Prevent swapping
};
```

---

## Environment Variables

```bash
# OpenAI
export OPENAI_API_KEY="sk-..."

# Anthropic
export ANTHROPIC_API_KEY="sk-ant-..."

# Azure OpenAI
export AZURE_OPENAI_ENDPOINT="https://..."
export AZURE_OPENAI_API_KEY="..."

# AWS Bedrock (or use IAM role)
export AWS_ACCESS_KEY_ID="..."
export AWS_SECRET_ACCESS_KEY="..."
export AWS_REGION="us-east-1"

# OpenRouter
export OPENROUTER_API_KEY="sk-or-..."
```

---

## Next Steps

- **Cloud**: Start with OpenAI or Anthropic for best features
- **Local**: Try Ollama for easy setup, LLamaSharp for maximum control
- **Cost**: Use OpenRouter for automatic cost optimization
- **Enterprise**: Consider Azure OpenAI for compliance/SLA

For implementation examples, see `samples/BasicChat/` and the main README.
