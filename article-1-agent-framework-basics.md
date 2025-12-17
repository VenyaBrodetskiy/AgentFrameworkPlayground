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

// Run the agent with streaming
var streamingResponse = agent.RunStreamingAsync(userInput, thread);
await foreach (var chunk in streamingResponse)
{
    Console.Write(chunk);
}
```

**What's happening here?**

1. **Function Definition**: We define a simple C# method with `[Description]` attributes. These descriptions are crucial — they're sent to the LLM to help it understand when and how to use the tool.

2. **Tool Registration**: `AIFunctionFactory.Create()` uses reflection to analyze your method's signature and attributes, then generates the tool schema the LLM expects. This schema includes parameter names, types, and descriptions.

3. **Agent Creation**: `CreateAIAgent()` is an extension method that wraps the `ChatClient` and provides agent-specific capabilities. The `instructions` parameter sets the system prompt that guides the agent's behavior.

4. **Thread Management**: `GetNewThread()` creates a conversation context that maintains message history. Each thread is isolated, allowing you to manage multiple conversations independently.

5. **Streaming Execution**: `RunStreamingAsync()` sends the user input to the LLM and streams back responses in chunks. This provides immediate feedback to users instead of waiting for the complete response.

**Under the Hood: Function Calling Flow**

When a user asks "What's the weather in Paris?", here's what happens:

1. The LLM receives the question and the available tool schemas
2. It decides to call `GetWeather` with parameter `location: "Paris"`
3. Agent Framework intercepts this, executes your C# method
4. The result is sent back to the LLM as a "tool message"
5. The LLM incorporates the result into its final response: "Just a second... The weather in Paris is cloudy with a high of 15°C."

This orchestration happens automatically — you just define functions and the framework handles the rest.

**Why This Matters**

Compared to Semantic Kernel's plugin registration ceremony, this is dramatically simpler:

```csharp
// Semantic Kernel approach (more verbose)
var kernel = builder.Build();
kernel.ImportPluginFromFunctions("WeatherPlugin",
    new[] { KernelFunctionFactory.CreateFromMethod(GetWeather) });

// Agent Framework approach (concise)
var weatherFunction = AIFunctionFactory.Create(GetWeather);
var agent = chatClient.CreateAIAgent(tools: [weatherFunction]);
```

You can find the complete implementation in the [AgentFramework project](https://github.com/VenyaBrodetskiy/AgentFrameworkPlayground/tree/main/AgentFramework).

## Example 2: Structured Output

One of the most powerful features in Agent Framework is the ability to extract structured data from unstructured text. Instead of parsing LLM responses as strings, you get type-safe objects.

Here's how to extract structured information from a meeting transcript:

```csharp
// Define your output structure with nested types
[Description("Structured meeting information")]
public class MeetingAnalysis
{
    [JsonPropertyName("date")]
    public string? Date { get; set; }

    [JsonPropertyName("duration_minutes")]
    public int? DurationMinutes { get; set; }

    [JsonPropertyName("attendees")]
    public List<string>? Attendees { get; set; }

    [JsonPropertyName("decisions")]
    public List<string>? Decisions { get; set; }

    [JsonPropertyName("action_items")]
    public List<ActionItem>? ActionItems { get; set; }
}

public class ActionItem
{
    [JsonPropertyName("assignee")]
    public string? Assignee { get; set; }

    [JsonPropertyName("task")]
    public string? Task { get; set; }

    [JsonPropertyName("due_date")]
    public string? DueDate { get; set; }
}

// Create the agent
var agent = chatClient.CreateAIAgent(
    name: "MeetingAnalyzer",
    instructions: "You are an assistant that extracts structured information from meeting transcripts.");

// Run with structured output - notice the generic type parameter
var response = await agent.RunAsync<MeetingAnalysis>(
    $"Please analyze this meeting transcript and extract key information:\n\n{meetingTranscript}",
    thread
);

// Access type-safe properties - no parsing needed!
Console.WriteLine($"Meeting Date: {response.Result.Date}");
Console.WriteLine($"Duration: {response.Result.DurationMinutes} minutes");
Console.WriteLine($"Attendees: {string.Join(", ", response.Result.Attendees)}");

foreach (var item in response.Result.ActionItems)
{
    Console.WriteLine($"- {item.Assignee}: {item.Task} (due: {item.DueDate})");
}
```

**How It Works: JSON Schema Generation**

When you call `RunAsync<MeetingAnalysis>()`, Agent Framework:

1. **Generates a JSON schema** from your C# class using reflection
2. **Sends it to the LLM** as part of the function calling specification
3. **Constrains the response** to match your schema exactly
4. **Deserializes** the JSON response into your strongly-typed object

The `[Description]` attributes on your class help the LLM understand what each field represents, improving extraction accuracy. The `[JsonPropertyName]` attributes control the JSON property names, which is especially useful when working with LLMs that expect specific naming conventions (like snake_case).

**Key Benefits:**

- **Type safety**: Compile-time checking, IntelliSense support, refactoring confidence
- **No manual parsing**: No regex, no JSON.Parse, no string manipulation
- **Automatic validation**: The LLM's response is validated against your schema
- **Nested objects**: Full support for complex hierarchies (see `ActionItem` nested in `MeetingAnalysis`)

**Real-World Use Cases:**

- **Entity extraction**: Pull structured data from documents, emails, or forms
- **Classification**: Convert natural language into enum values or categories
- **Data normalization**: Transform varied input formats into consistent schemas
- **Workflow intake**: Capture structured information from user requests

This pattern is invaluable for building agents that need to extract entities, classify content, or transform unstructured data into structured records.

Check out the full example in the [AgentFrameworkStructuredOutput project](https://github.com/VenyaBrodetskiy/AgentFrameworkPlayground/tree/main/AgentFrameworkStructuredOutput).

## Example 3: Thread Persistence

In production scenarios, you often need to save and resume conversations. Agent Framework makes this straightforward with custom storage providers.

```csharp
// Create custom storage providers
var threadStore = new FileThreadStore(
    storageDirectory: "./threads",
    threadStateFileName: "thread-state.json");

// Save the current conversation thread
threadStore.Save(thread);

// Later, when the user returns
if (threadStore.Exists)
{
    // Deserialize the thread using the agent's deserializer
    var loadedThread = threadStore.Load(thread.Deserialize);

    // Continue the conversation
    var response = await agent.RunAsync(newUserInput, loadedThread);
}
```

**How Thread Serialization Works**

Agent Framework provides built-in serialization methods on `AgentThread`:

```csharp
public void Save(AgentThread thread)
{
    // Serialize thread to JsonElement
    var serializedThread = thread.Serialize();

    // Convert to JSON string
    var threadStateJson = JsonSerializer.Serialize(
        serializedThread,
        new JsonSerializerOptions { WriteIndented = true });

    // Persist to file (or database, Redis, etc.)
    File.WriteAllText(_threadStatePath, threadStateJson);
}

public AgentThread Load(Func<JsonElement, AgentThread> deserializeThread)
{
    // Read JSON from storage
    var threadStateJson = File.ReadAllText(_threadStatePath);

    // Parse as JsonElement
    var serializedThread = JsonSerializer.Deserialize<JsonElement>(threadStateJson);

    // Use the provided deserializer to reconstruct the thread
    return deserializeThread(serializedThread);
}
```

The `thread.Serialize()` and `thread.Deserialize()` methods handle the complexity of converting thread state (including message history, metadata, and agent context) to and from JSON.

**Implementing Custom Storage**

The file-based implementation is just one option. You can easily adapt this pattern for any storage backend:

**Database Storage:**
```csharp
public class DatabaseThreadStore
{
    private readonly DbContext _context;

    public async Task SaveAsync(AgentThread thread, string userId)
    {
        var serialized = thread.Serialize();
        var json = JsonSerializer.Serialize(serialized);

        await _context.ThreadStates.AddAsync(new ThreadState
        {
            UserId = userId,
            ThreadId = thread.Id,
            StateJson = json,
            UpdatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
    }
}
```

**Redis Storage:**
```csharp
public class RedisThreadStore
{
    private readonly IDatabase _redis;

    public async Task SaveAsync(AgentThread thread, TimeSpan expiry)
    {
        var serialized = thread.Serialize();
        var json = JsonSerializer.Serialize(serialized);

        await _redis.StringSetAsync(
            key: $"thread:{thread.Id}",
            value: json,
            expiry: expiry);
    }
}
```

**Why Thread Persistence Matters**

1. **User Experience**: Users can continue conversations across sessions
2. **Cost Optimization**: Avoid re-processing context in every request
3. **Compliance**: Maintain conversation history for audit trails
4. **Analytics**: Track conversation patterns and user interactions

This approach gives you full control over conversation persistence while maintaining a clean API. Whether you store threads in files, databases, or distributed caches, the pattern remains the same.

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

Retrieval-Augmented Generation (RAG) enhances agents with external knowledge. Agent Framework integrates RAG through `TextSearchProvider`, which injects relevant context into agent prompts automatically.

### Setting Up the Vector Store

First, define your document schema using vector store attributes:

```csharp
private sealed class SearchRecord
{
    // Embedding dimension must match your model (e.g., text-embedding-3-large is 3072)
    private const int EmbeddingDimensions = 3072;

    [VectorStoreKey]
    public required string SourceId { get; init; }

    [VectorStoreData]
    public string? SourceName { get; init; }

    [VectorStoreData]
    public string? SourceLink { get; init; }

    [VectorStoreData(IsFullTextIndexed = true)]
    public string? Text { get; init; }

    [VectorStoreVector(EmbeddingDimensions)]
    public ReadOnlyMemory<float> TextEmbedding { get; init; }
}
```

**Attribute Breakdown:**

- `[VectorStoreKey]`: Identifies the unique record identifier
- `[VectorStoreData]`: Marks metadata fields that are stored but not searched
- `[VectorStoreData(IsFullTextIndexed = true)]`: Enables full-text search on this field
- `[VectorStoreVector(dimensions)]`: Marks the embedding vector field

### Creating and Populating the Knowledge Base

```csharp
// Initialize vector store and embedding generator
var vectorStore = new InMemoryVectorStore();
var embeddingGenerator = new AzureOpenAIClient(
        new Uri(endpoint),
        new AzureKeyCredential(apiKey))
    .GetEmbeddingClient(embeddingModel);

// Create collection with automatic embedding generation
var collection = vectorStore.GetCollection<string, SearchRecord>(
    "product-and-policy-info",
    new VectorStoreCollectionDefinition
    {
        EmbeddingGenerator = embeddingGenerator
    });

await collection.EnsureCollectionExistsAsync();

// Add documents with automatic embedding
var records = new List<SearchRecord>();
foreach (var doc in documents)
{
    records.Add(new SearchRecord
    {
        SourceId = doc.SourceId,
        SourceName = doc.SourceName,
        SourceLink = doc.SourceLink,
        Text = doc.Text,
        // Embedding is generated automatically
        TextEmbedding = await embeddingGenerator.GenerateVectorAsync(doc.Text)
    });
}

await collection.UpsertAsync(records);
```

### Integrating RAG with Your Agent

```csharp
// Create a knowledge base wrapper
var knowledgeBase = await RagKnowledgeBase.CreateAsync(
    vectorStore,
    embeddingGenerator);

// Create text search provider
var searchProvider = new TextSearchProvider(
    async (query, ct) =>
    {
        return await knowledgeBase.SearchAsync(query, ct);
    });

// Agent automatically uses RAG when needed
var agent = chatClient.CreateAIAgent(
    instructions: """
        You are a customer support assistant for BrightTrail Gear.
        Use the knowledge base to answer questions accurately.
        If you don't find relevant information, say so clearly.
        """,
    tools: [searchProvider],
    name: "SupportAgent");

// User asks a question
var response = await agent.RunAsync(
    "What's your return policy?",
    thread);
```

**What Happens Under the Hood:**

1. **User query arrives**: "What's your return policy?"
2. **Query embedding**: The question is converted to a vector using the same embedding model
3. **Similarity search**: The vector store finds the top 3 most similar documents using cosine similarity
4. **Context injection**: Retrieved documents are automatically added to the LLM prompt:
   ```
   [System]: You are a customer support assistant...
   [Context from knowledge base]:
   - Returns are accepted within 30 days of delivery...
   [User]: What's your return policy?
   ```
5. **LLM response**: The model answers based on the retrieved context

**Vector Search Mechanics**

The similarity search uses vector distance metrics (typically cosine similarity):

```csharp
public async Task<IEnumerable<TextSearchResult>> SearchAsync(
    string query,
    CancellationToken cancellationToken)
{
    var results = new List<TextSearchResult>();

    // Search returns top N results ranked by similarity
    await foreach (var r in _collection.SearchAsync(
        query,
        topResults: 3,
        options: null,
        cancellationToken: cancellationToken))
    {
        results.Add(new TextSearchResult
        {
            SourceName = r.Record.SourceName,
            SourceLink = r.Record.SourceLink,
            Text = r.Record.Text ?? string.Empty,
            Score = r.Score  // Similarity score (0-1)
        });
    }

    return results;
}
```

**Production Considerations:**

- **Embedding models**: Choose based on your domain (general vs. specialized)
- **Vector dimensions**: Higher dimensions = more precise but slower and more storage
- **Top-K results**: Balance between context richness and token costs
- **Chunking strategy**: Break large documents into searchable chunks
- **Hybrid search**: Combine vector search with keyword/full-text search
- **Vector databases**: Use Azure AI Search, Pinecone, or Qdrant for scale

**Real-World Use Cases:**

- **Customer support**: Product docs, policies, FAQs
- **Internal knowledge**: Company wikis, procedures, tribal knowledge
- **Code assistance**: API documentation, code examples
- **Compliance**: Regulations, legal documents, audit trails

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
