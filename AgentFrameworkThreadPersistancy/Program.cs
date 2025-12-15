using Microsoft.Extensions.Configuration;
using System.Reflection;
using System.Text.Json;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Common;
using OpenAI;
using AgentFrameworkThreadPersistancy;

#pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning disable MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

Console.WriteLine($"=== Running: {Assembly.GetEntryAssembly()?.GetName().Name} ===");

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile("appsettings.Development.json", optional: false, reloadOnChange: true)
    .Build();

var modelName = configuration["ModelName"] ?? throw new ApplicationException("ModelName not found");
var endpoint = configuration["Endpoint"] ?? throw new ApplicationException("Endpoint not found");
var apiKey = configuration["ApiKey"] ?? throw new ApplicationException("ApiKey not found");

var agent = new AzureOpenAIClient(
        new Uri(endpoint),
        new AzureKeyCredential(apiKey))
    .GetChatClient(modelName)
    .CreateAIAgent(new ChatClientAgentOptions
    {
        Name = "Assistant",
        ChatOptions = new() { Instructions = "You are a helpful assistant." },
        ChatMessageStoreFactory = ctx => new FileChatMessageStore(ctx.SerializedState)
    });

var storageDirectory = Path.Combine(Environment.CurrentDirectory, "ThreadStorage");
var threadStore = new FileThreadStore(storageDirectory);

AgentThread thread;
if (threadStore.Exists)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("\n✓ Found saved thread. Resuming conversation...\n");
    Console.ResetColor();

    // Load and deserialize the thread
    thread = threadStore.Load(serializedThread => agent.DeserializeThread(serializedThread));

    // Display historical messages
    await DisplayHistoricalMessagesAsync(thread);
}
else
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("\n→ No saved thread found. Starting new conversation.\n");
    Console.ResetColor();

    // Create a new thread
    thread = agent.GetNewThread();
}

do
{
    ConsoleUi.WriteUserPrompt();
    var userInput = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(userInput))
        continue;

    // Check for special commands
    if (userInput.Trim().Equals("/clear", StringComparison.OrdinalIgnoreCase) ||
        userInput.Trim().Equals("/reset", StringComparison.OrdinalIgnoreCase))
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n⚠ Clearing thread and starting fresh conversation...\n");
        Console.ResetColor();

        // Delete all thread storage (messages and state)
        storageDirectory = Path.Combine(Environment.CurrentDirectory, "ThreadStorage");
        if (Directory.Exists(storageDirectory))
        {
            foreach (var file in Directory.GetFiles(storageDirectory))
            {
                File.Delete(file);
            }
        }

        // Create new thread
        thread = agent.GetNewThread();
        
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("✓ Thread cleared successfully!\n");
        Console.ResetColor();
        
        continue;
    }

    var streamingResponse = agent.RunStreamingAsync(userInput, thread);

    ConsoleUi.WriteAgentPrompt();

    await foreach (var chunk in streamingResponse)
    {
        ConsoleUi.WriteAgentChunk(chunk);
    }
    Console.WriteLine();

    // Save thread state after each interaction
    threadStore.Save(thread);

} while (true);

static async Task DisplayHistoricalMessagesAsync(AgentThread thread)
{
    var messageStore = thread.GetService<FileChatMessageStore>();
    if (messageStore == null)
        return;

    var messages = await messageStore.GetMessagesAsync();
    
    foreach (var message in messages)
    {
        if (message.Role == Microsoft.Extensions.AI.ChatRole.User)
        {
            ConsoleUi.WriteHistoricalUserPrompt();
            ConsoleUi.WriteHistoricalMessage(message.Text ?? string.Empty);
            Console.WriteLine();
        }
        else if (message.Role == Microsoft.Extensions.AI.ChatRole.Assistant)
        {
            ConsoleUi.WriteHistoricalAgentPrompt();
            ConsoleUi.WriteHistoricalMessage(message.Text ?? string.Empty);
            Console.WriteLine();
        }
    }
    
    Console.WriteLine();
}

#pragma warning restore OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning restore MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
