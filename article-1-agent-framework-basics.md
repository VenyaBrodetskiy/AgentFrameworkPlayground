# Getting Started with Microsoft Agent Framework

If you've been building AI agents with Semantic Kernel or LangChain, you know the landscape. You wire up prompts, configure plugins, manage conversation state, and piece together integrations. It works, but sometimes it feels like you're fighting the framework as much as building with it.

Microsoft Agent Framework changes that equation. Think of it as the next evolution — a fresh start that learns from both Semantic Kernel and AutoGen, distilling their best ideas into something cleaner and more intuitive.

In this guide, we'll explore the fundamentals of Agent Framework through practical examples. You'll learn how to build agents that can call functions, extract structured data, persist conversations, integrate with Azure AI Foundry, and leverage RAG — all with less boilerplate and more clarity than before.

## What is Agent Framework?

Agent Framework is Microsoft's development kit for building AI agents and multi-agent workflows. Currently in public preview, it's positioned as the next generation of both Semantic Kernel and AutoGen — essentially, think of it as "SK v2."

The framework focuses on two core capabilities:

1. **AI Agents**: Individual agents that use LLMs to process inputs, call tools, and generate responses
2. **Workflows**: Graph-based orchestration that connects multiple agents and functions to perform complex, multi-step tasks

Microsoft built Agent Framework to address the pain points developers encountered with earlier frameworks: simplified APIs, better performance, unified interfaces across providers, and a more intuitive developer experience.

You can learn more in the [official Microsoft Agent Framework documentation](https://learn.microsoft.com/en-us/dotnet/ai/agent-framework-overview).

## Prerequisites

Before diving in, make sure you have:

- **.NET 10.0 SDK** or later
- **Azure OpenAI** account with:
  - API endpoint
  - API key
  - Model deployment (e.g., `gpt-4`, `gpt-4o`)
  - Embedding model deployment (for RAG examples)

Most projects require an `appsettings.json` or `appsettings.Development.json` file with your Azure OpenAI configuration:

```json
{
  "ModelName": "your-model-deployment-name",
  "Endpoint": "https://your-resource.openai.azure.com/",
  "ApiKey": "your-api-key",
  "EmbeddingModel": "your-embedding-model-name"
}
```

⚠️ **Note**: Agent Framework is currently in public preview. APIs may evolve in future releases.

## Core Concepts

Before we jump into examples, let's establish the foundational concepts:

**AIAgent**: The core abstraction representing an AI agent. It encapsulates the LLM, instructions, and tools the agent can use.

**Tools/Functions**: Actions your agent can perform — from simple C# methods to complex API calls. Agent Framework uses `AIFunctionFactory` to convert methods into tools the LLM can call. For a deep dive on function calling, check out [OpenAI's function calling guide](https://platform.openai.com/docs/guides/function-calling).

**AgentThread**: Manages conversation context and history. Think of it as the stateful container for your agent's interactions with users.

With these building blocks in mind, let's see them in action.

## Example 1: Simple Agent with Function Calling

Let's start with the fundamentals: creating an agent that can call tools.

```csharp
// Define a function the agent can call
[Description("Get the weather for a given location.")]
static string GetWeather([Description("The location to get the weather for.")] string location)
    => $"The weather in {location} is cloudy with a high of 15°C.";

// Convert the function to a tool
var weatherFunction = AIFunctionFactory.Create(GetWeather);

// Create the agent
var agent = new AzureOpenAIClient(
        new Uri(endpoint),
        new AzureKeyCredential(apiKey))
    .GetChatClient(modelName)
    .CreateAIAgent(
        instructions: "say 'just a second' before answering question",
        tools: [weatherFunction],
        name: "myagent");

// Create a thread for conversation
var thread = agent.GetNewThread();

// Run the agent
var streamingResponse = agent.RunStreamingAsync(userInput, thread);
await foreach (var chunk in streamingResponse)
{
    Console.Write(chunk);
}
```

**What's happening here?**

1. We define a simple C# method `GetWeather` with description attributes
2. `AIFunctionFactory.Create()` converts it into a tool the LLM can discover and invoke
3. `CreateAIAgent()` sets up our agent with instructions and available tools
4. The agent automatically decides when to call the weather function based on user queries

This pattern is remarkably clean compared to earlier frameworks. No kernel configuration, no plugin registration ceremony — just your function, your agent, and you're running.

You can find the complete implementation in the [AgentFramework project](https://github.com/VenyaBrodetskiy/AgentFrameworkPlayground/tree/main/AgentFramework).

## Example 2: Structured Output

One of the most powerful features in Agent Framework is the ability to extract structured data from unstructured text. Instead of parsing LLM responses as strings, you get type-safe objects.

Here's how to extract structured information from a meeting transcript:

```csharp
// Define your output structure
public class MeetingInfo
{
    [Description("The title of the meeting")]
    public string Title { get; set; }

    [Description("List of participants")]
    public List<string> Participants { get; set; }

    [Description("Key action items")]
    public List<string> ActionItems { get; set; }
}

// Run the agent with structured output
var result = await agent.RunAsync<MeetingInfo>(
    "Analyze this transcript: ...",
    thread
);

// Access type-safe properties
Console.WriteLine($"Meeting: {result.Title}");
foreach (var item in result.ActionItems)
{
    Console.WriteLine($"- {item}");
}
```

**Key benefits:**

- Type-safe responses — no string parsing required
- JSON schema is automatically generated from your C# classes
- Description attributes guide the LLM on what to extract

This pattern is invaluable for building agents that need to extract entities, classify content, or transform unstructured data into structured records.

Check out the full example in the [AgentFrameworkStructuredOutput project](https://github.com/VenyaBrodetskiy/AgentFrameworkPlayground/tree/main/AgentFrameworkStructuredOutput).

## Example 3: Thread Persistence

In production scenarios, you often need to save and resume conversations. Agent Framework makes this straightforward with custom storage providers.

```csharp
// Create custom stores
var threadStore = new FileThreadStore("./threads");
var messageStore = new FileChatMessageStore("./messages");

// Save a thread
await threadStore.SaveThreadAsync(thread);

// Later, resume the conversation
var loadedThread = await threadStore.LoadThreadAsync(threadId);
var messages = await messageStore.LoadMessagesAsync(threadId);

// Continue where you left off
var response = await agent.RunAsync(newUserInput, loadedThread);
```

The example implementation uses file-based storage, but you can easily adapt the pattern for databases, Redis, or any other persistence layer. The key interfaces to implement are:

- `IThreadStore`: Manages thread metadata
- `IChatMessageStore`: Handles message history

This approach gives you full control over conversation persistence while maintaining a clean API.

Explore the complete implementation in the [AgentFrameworkThreadPersistancy project](https://github.com/VenyaBrodetskiy/AgentFrameworkPlayground/tree/main/AgentFrameworkThreadPersistancy).

## Example 4: Azure AI Foundry Integration

Azure AI Foundry provides managed agent infrastructure in the cloud. Instead of managing agent lifecycle yourself, you can leverage Foundry's persistent agents.

```csharp
// Connect to Azure AI Foundry
var credential = new AzureCliCredential();
var client = new PersistentAgentsClient(
    new Uri(projectEndpoint),
    credential
);

// Create or retrieve a managed agent
var agent = await client.CreateAgentAsync(
    model: modelName,
    instructions: "You are a helpful assistant",
    tools: tools
);

// Use the agent like any other
var thread = await client.CreateThreadAsync();
var response = await client.RunAsync(agent.Id, thread.Id, userMessage);
```

**Why use Foundry?**

- **Cloud-native persistence**: Agents and threads are automatically stored
- **Lifecycle management**: No need to handle agent state yourself
- **Azure integration**: Seamless authentication and monitoring
- **Scale**: Leverage Azure's infrastructure for production workloads

For teams building production systems, Foundry removes significant operational overhead.

See the working example in the [AgentFrameworkFoundryAgent project](https://github.com/VenyaBrodetskiy/AgentFrameworkPlayground/tree/main/AgentFrameworkFoundryAgent).

## Example 5: RAG with Vector Search

Retrieval-Augmented Generation (RAG) enhances agents with external knowledge. Agent Framework integrates RAG through `TextSearchProvider`, which injects relevant context into agent prompts.

```csharp
// Create an in-memory vector store
var vectorStore = new InMemoryVectorStore();
var collection = vectorStore.GetCollection<string, Document>("docs");

// Generate embeddings and store documents
var embeddingGenerator = new AzureOpenAIClient(...)
    .GetEmbeddingClient(embeddingModel);

foreach (var doc in documents)
{
    var embedding = await embeddingGenerator.GenerateEmbeddingAsync(doc.Text);
    await collection.UpsertAsync(new Document
    {
        Id = doc.Id,
        Text = doc.Text,
        Embedding = embedding.Vector
    });
}

// Create a search provider
var searchProvider = new TextSearchProvider(collection, embeddingGenerator);

// Agent automatically retrieves relevant context
var agent = chatClient.CreateAIAgent(
    instructions: "Answer questions using the provided context",
    tools: [searchProvider]
);
```

**How it works:**

1. Documents are converted to embeddings and stored in a vector database
2. When a user asks a question, the agent searches for relevant documents
3. Retrieved context is injected into the prompt
4. The LLM answers based on both its training and the retrieved knowledge

This pattern is essential for building agents that need to reference specific documentation, databases, or knowledge bases. For more on vector embeddings and similarity search, see [Azure OpenAI embeddings documentation](https://learn.microsoft.com/en-us/azure/ai-services/openai/concepts/understand-embeddings).

Dive into the full RAG implementation in the [AgentFrameworkRag project](https://github.com/VenyaBrodetskiy/AgentFrameworkPlayground/tree/main/AgentFrameworkRag).

## Bonus: Python Support

Agent Framework isn't just for C# developers. Microsoft provides Python support, enabling cross-language agent development.

```python
from azure.ai.agents import AIAgent
from azure.ai.openai import AzureOpenAIClient

# Create agent
client = AzureOpenAIClient(endpoint=endpoint, credential=credential)
agent = client.create_agent(
    model=model_name,
    instructions="You are a helpful assistant",
    tools=[weather_tool]
)

# Run the agent
thread = agent.create_thread()
response = agent.run(user_input, thread)
```

The Python API mirrors the C# design, making it easy to work across languages or migrate existing Python projects to Agent Framework.

Check out the Python example in the [AgentFrameworkPython directory](https://github.com/VenyaBrodetskiy/AgentFrameworkPlayground/tree/main/AgentFrameworkPython).

## What's Next?

We've covered the fundamentals — individual agents with various capabilities. But what happens when you need to orchestrate multiple agents and deterministic logic into complex workflows?

That's where Agent Framework's workflow system comes in. Workflows let you build graph-based processes that combine LLM agents with business rules, conditional routing, and shared state management.

In the next article, ["Building Multi-Step Workflows with Agent Framework"](./article-2-agent-framework-workflows.md), we'll explore how to build a real-world customer support email triage system that automatically classifies emails, applies business policies, and routes to either automated responses or human escalation.

## In Conclusion

Microsoft Agent Framework represents a significant step forward in AI agent development. By learning from Semantic Kernel and AutoGen, it delivers a cleaner, more intuitive API that accelerates development without sacrificing power.

Whether you're building simple chatbots or complex multi-agent systems, Agent Framework provides the building blocks you need — and as the successor to SK and AutoGen, it's the future direction for Microsoft's AI agent ecosystem.

🔗 **Explore the complete demo repository**: [AgentFrameworkPlayground on GitHub](https://github.com/VenyaBrodetskiy/AgentFrameworkPlayground)

🔍 **Continue the journey**: Read the next article on [Building Multi-Step Workflows with Agent Framework](./article-2-agent-framework-workflows.md)

🤝 **Your feedback is invaluable!** Feel free to drop comments, ask questions, or share your insights and optimizations. Every contribution helps to enhance our collective knowledge and build a resourceful developer community.

Happy Coding! 🚀
