using AgentFrameworkFoundryAgent;
using Azure.AI.Agents.Persistent;
using Azure.Identity;
using Common;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using System.Reflection;

#pragma warning disable MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

Console.WriteLine($"=== Running: {Assembly.GetEntryAssembly()?.GetName().Name} ===");

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile("appsettings.Development.json", optional: false, reloadOnChange: true)
    .Build();

var modelName = configuration["ModelName"] ?? throw new ApplicationException("ModelName not found");
var projectEndpoint = configuration["ProjectEndpoint"] ?? throw new ApplicationException("Endpoint not found");

PersistentAgentsClient aiProjectClient = new(projectEndpoint, new AzureCliCredential());

ChatClientAgent agent;
const string agentId = "asst_V9o17V41ZiFYrUYVd4qLAY3K";
try
{
    agent = await aiProjectClient.GetAIAgentAsync(agentId);
}
catch (Exception e)
{
    agent = await aiProjectClient.CreateAIAgentAsync(
        model: modelName,
        instructions: "say 'just a second' before answering question",
        name: "myagent");
}

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
    await DisplayHistoricalMessagesAsync(aiProjectClient, thread);
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

    // Check for clear/reset commands
    if (userInput.Trim().Equals("/clear", StringComparison.OrdinalIgnoreCase) ||
        userInput.Trim().Equals("/reset", StringComparison.OrdinalIgnoreCase))
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n⚠ Clearing thread and starting fresh conversation...\n");
        Console.ResetColor();

        // Delete local thread storage
        threadStore.Delete();

        // Create new thread
        thread = agent.GetNewThread();

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("✓ Thread cleared successfully!\n");
        Console.ResetColor();

        continue;
    }

    var streamingResponse =
        agent.RunStreamingAsync(userInput, thread);

    ConsoleUi.WriteAgentPrompt();

    await foreach (var chunk in streamingResponse)
    {
        ConsoleUi.WriteAgentChunk(chunk);
    }
    Console.WriteLine();

    // Save thread state after each interaction
    threadStore.Save(thread);

} while (true);

static async Task DisplayHistoricalMessagesAsync(PersistentAgentsClient client, AgentThread thread)
{
    try
    {
        // Get the thread ID from the serialized state
        var serializedThread = thread.Serialize();
        if (serializedThread.TryGetProperty("conversationId", out var threadIdElement))
        {
            var threadId = threadIdElement.GetString();
            if (!string.IsNullOrEmpty(threadId))
            {
                var messages = client.Messages.GetMessagesAsync(
                    threadId: threadId,
                    order: ListSortOrder.Ascending);

                await foreach (var message in messages)
                {
                    var role = message.Role.ToString();
                    var content = string.Join("", message.ContentItems
                        .OfType<MessageTextContent>()
                        .Select(c => c.Text));

                    if (string.IsNullOrEmpty(content))
                        continue;

                    if (role.Equals("user", StringComparison.OrdinalIgnoreCase))
                    {
                        ConsoleUi.WriteHistoricalUserPrompt();
                        ConsoleUi.WriteHistoricalMessage(content);
                        Console.WriteLine();
                    }
                    else if (role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
                    {
                        ConsoleUi.WriteHistoricalAgentPrompt();
                        ConsoleUi.WriteHistoricalMessage(content);
                        Console.WriteLine();
                    }
                }

                Console.WriteLine();
            }
        }
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine($"Note: Could not load historical messages: {ex.Message}");
        Console.ResetColor();
    }
}

#pragma warning restore MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
