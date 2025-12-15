using Microsoft.Extensions.Configuration;
using System.Reflection;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Data;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.InMemory;
using Microsoft.Extensions.AI;
using Common;
using OpenAI;

#pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning disable MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

Console.WriteLine($"=== Running: {Assembly.GetEntryAssembly()?.GetName().Name} ===");

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile("appsettings.Development.json", optional: false, reloadOnChange: true)
    .Build();

var modelName = configuration["ModelName"] ?? throw new ApplicationException("ModelName not found");
var embeddingModel = configuration["EmbeddingModel"] ?? throw new ApplicationException("EmbeddingModel not found");
var endpoint = configuration["Endpoint"] ?? throw new ApplicationException("Endpoint not found");
var apiKey = configuration["ApiKey"] ?? throw new ApplicationException("ApiKey not found");

var azureOpenAiClient = new AzureOpenAIClient(
    new Uri(endpoint),
    new AzureKeyCredential(apiKey));

// Create an In-Memory vector store that uses the Azure OpenAI embedding model to generate embeddings.
var embeddingGenerator = azureOpenAiClient.GetEmbeddingClient(embeddingModel).AsIEmbeddingGenerator();
VectorStore vectorStore = new InMemoryVectorStore(new InMemoryVectorStoreOptions
{
    EmbeddingGenerator = embeddingGenerator
});

var knowledgeBase = await RagKnowledgeBase.CreateAsync(vectorStore, embeddingGenerator);

// Adapter function used by TextSearchProvider to retrieve context.
Func<string, CancellationToken, Task<IEnumerable<TextSearchProvider.TextSearchResult>>> searchAdapter = knowledgeBase.SearchAsync;

TextSearchProviderOptions textSearchOptions = new()
{
    SearchTime = TextSearchProviderOptions.TextSearchBehavior.OnDemandFunctionCalling,
    RecentMessageMemoryLimit = 6, // Include some recent user/assistant messages when constructing the search query.
};

var agent = azureOpenAiClient
    .GetChatClient(modelName)
    .CreateAIAgent(new ChatClientAgentOptions
    {
        Name = "myagent",
        ChatOptions = new ChatOptions
        {
            Instructions = "Say 'just a second' before answering."
        },
        AIContextProviderFactory = ctx => new TextSearchProvider(searchAdapter, ctx.SerializedState, ctx.JsonSerializerOptions, textSearchOptions)
    });

var thread = agent.GetNewThread();

do
{
    ConsoleUi.WriteUserPrompt();

    var userInput = Console.ReadLine();

    var streamingResponse =
        agent.RunStreamingAsync(userInput!, thread);

    ConsoleUi.WriteAgentPrompt();

    await foreach (var chunk in streamingResponse)
    {
        ConsoleUi.WriteAgentChunk(chunk);
    }
    Console.WriteLine();

} while (true);

#pragma warning restore OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning restore MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
