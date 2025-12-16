# Agent Framework Playground

A collection of C# projects demonstrating various capabilities of the **Microsoft Agent Framework** (preview). This repository serves as a learning resource and playground for exploring AI agent patterns, workflows, structured output, RAG, and more.

## Overview

This repository contains multiple independent projects, each showcasing different aspects of the Microsoft Agent Framework:

- **Basic agent creation** with function calling
- **Structured output** extraction from unstructured text
- **Thread persistence** for conversational continuity
- **RAG (Retrieval-Augmented Generation)** with vector search
- **Workflows** for orchestrating multi-step agent processes
- **Foundry integration** for managed agents
- **Semantic Kernel** integration examples

## Projects

### 1. AgentFramework (`1-AgentFramework.csproj`)
**Basic agent with function calling**

Demonstrates:
- Creating a simple AI agent using `ChatClient.CreateAIAgent()`
- Function calling with `AIFunctionFactory.Create()`
- Streaming responses
- Basic agent-thread interaction

**Key concepts:**
- `AIAgent` and `AgentThread`
- Tool/function integration
- Streaming responses

---

### 2. AgentFrameworkStructuredOutput (`2-AgentFrameworkStructuredOutput.csproj`)
**Structured output extraction**

Demonstrates:
- Extracting structured data from unstructured text (meeting transcripts)
- Using `RunAsync<T>()` for type-safe structured output
- JSON schema generation from C# classes

**Key concepts:**
- Structured output with `Description` attributes
- Type-safe LLM responses
- JSON schema generation

---

### 3. AgentFrameworkThreadPersistancy (`3-AgentFrameworkThreadPersistancy.csproj`)
**Conversation persistence**

Demonstrates:
- Saving and loading agent threads
- Resuming conversations across sessions
- Custom thread and message storage (`FileThreadStore`, `FileChatMessageStore`)

**Key concepts:**
- Thread serialization/deserialization
- Conversation continuity
- Custom storage providers

---

### 4. AgentFrameworkFoundryAgent (`4-AgentFrameworkFoundryAgent.csproj`)
**Azure AI Foundry integration**

Demonstrates:
- Using Azure AI Foundry's managed agents
- `PersistentAgentsClient` for agent lifecycle management
- Creating and retrieving agents from Foundry

**Key concepts:**
- Managed agents in Azure AI Foundry
- Agent persistence in the cloud
- Azure authentication (`AzureCliCredential`)

---

### 5. AgentFrameworkRag (`5-AgentFrameworkRag.csproj`)
**Retrieval-Augmented Generation**

Demonstrates:
- Vector store integration (In-Memory)
- Embedding generation with Azure OpenAI
- `TextSearchProvider` for on-demand RAG
- Knowledge base creation and search

**Key concepts:**
- Vector embeddings and similarity search
- RAG pattern with `TextSearchProvider`
- Context injection into agent prompts

---

### 6. AgentFrameworkWorkflows (`6-AgentFrameworkWorkflows.csproj`)
**Multi-step workflow orchestration**

Demonstrates:
- Building complex workflows with multiple executors
- Combining LLM agents with deterministic logic
- Conditional routing based on context
- Shared state management
- Custom events for observability

**Workflow example:** Customer support email triage and response
- **Preprocess** (deterministic): Clean text, detect/mask PII, extract identifiers
- **Intake** (LLM agent): Classify category, urgency, sentiment, intent
- **Policy** (deterministic): Apply business rules, determine response mode
- **Route** (conditional): Escalate, refund automation, or draft reply
- **Final Summary** (deterministic): Consolidate output from shared state

**Key concepts:**
- `WorkflowBuilder` and `Executor<TInput, TOutput>`
- Conditional edges (`AddEdge<T>(..., condition: ...)`)
- Shared state (`QueueStateUpdateAsync`, `ReadStateAsync`)
- Custom events for tracing
- Hybrid executors (agent + deterministic)

See [`AgentFrameworkWorkflows/workflow.md`](AgentFrameworkWorkflows/workflow.md) for a visual diagram.

---

### SemanticKernel & SemanticKernelAgentFramework
**Semantic Kernel integration examples**

Demonstrates integration patterns between Semantic Kernel and Agent Framework.

---

## Prerequisites

- **.NET 10.0** SDK or later
- **Azure OpenAI** account with:
  - API endpoint
  - API key
  - Model deployment (e.g., `gpt-4`, `gpt-35-turbo`)
  - Embedding model deployment (for RAG project)

## Configuration

Most projects require an `appsettings.json` or `appsettings.Development.json` file with:

```json
{
  "ModelName": "your-model-deployment-name",
  "Endpoint": "https://your-resource.openai.azure.com/",
  "ApiKey": "your-api-key",
  "EmbeddingModel": "your-embedding-model-name"  // For RAG project
}
```

Some projects (Foundry) may require additional configuration:
```json
{
  "ProjectEndpoint": "https://your-project.cognitiveservices.azure.com/"
}
```

## Running Projects

Each project is a standalone console application. Run from the project directory:

```bash
cd AgentFramework
dotnet run
```

Or run from the solution root:

```bash
dotnet run --project AgentFramework/1-AgentFramework.csproj
```

## Project Structure

```
AgentFrameworkPlayground/
├── AgentFramework/              # Basic agent example
├── AgentFrameworkStructuredOutput/  # Structured output
├── AgentFrameworkThreadPersistancy/ # Thread persistence
├── AgentFrameworkFoundryAgent/     # Foundry integration
├── AgentFrameworkRag/              # RAG example
├── AgentFrameworkWorkflows/         # Workflow orchestration
│   ├── Executors/                   # Workflow executors
│   ├── Models/                      # Data models
│   ├── Events/                      # Custom events
│   └── workflow.md                  # Workflow diagram
├── Common/                          # Shared utilities
├── SemanticKernel/                 # SK examples
└── SemanticKernelAgentFramework/  # SK + Agent Framework
```

## Key Concepts Demonstrated

### Agents
- **AIAgent**: Core agent abstraction
- **ChatClientAgent**: Chat-based agent implementation
- **AgentThread**: Conversation context management

### Workflows
- **Executor**: Individual processing units in a workflow
- **WorkflowBuilder**: Fluent API for building workflow graphs
- **Conditional Edges**: Dynamic routing based on message content
- **Shared State**: Cross-executor data sharing
- **Events**: Observability and tracing

### Patterns
- **Hybrid Executors**: Combining LLM agents with deterministic logic
- **Structured Output**: Type-safe LLM responses
- **RAG**: Context injection via vector search
- **Persistence**: Thread and message storage

## Resources

- [Microsoft Agent Framework Documentation](https://learn.microsoft.com/en-us/agent-framework/)
- [Agent Framework Workflows Guide](https://learn.microsoft.com/en-us/agent-framework/user-guide/workflows/)
- [Azure AI Foundry](https://learn.microsoft.com/en-us/azure/ai-foundry/)

## Notes

⚠️ **Preview Software**: The Microsoft Agent Framework is currently in preview. APIs may change in future releases.

All projects include `#pragma warning disable` directives for preview API warnings.

## License

This is a learning/playground repository. Check individual project licenses if applicable.
