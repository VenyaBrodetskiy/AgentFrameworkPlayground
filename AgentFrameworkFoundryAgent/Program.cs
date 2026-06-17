using Microsoft.Extensions.Configuration;
using System.ClientModel;
using System.Reflection;
using System.Text.Json;
using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.Identity;
using Common;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Foundry;
using AgentFrameworkFoundryAgent;

#pragma warning disable MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

Console.WriteLine($"=== Running: {Assembly.GetEntryAssembly()?.GetName().Name} ===");

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile("appsettings.Development.json", optional: false, reloadOnChange: true)
    .Build();

var modelName = configuration["ModelName"] ?? throw new ApplicationException("ModelName not found");
var projectEndpoint = configuration["ProjectEndpoint"] ?? throw new ApplicationException("Endpoint not found");
var agentName = configuration["AgentName"] ?? "pirate-prompt-agent";
const string agentInstructions = "Answer like a friendly pirate. Keep responses helpful, concise, and clear.";

AIProjectClient aiProjectClient = new(new Uri(projectEndpoint), new AzureCliCredential());

ProjectsAgentRecord agentRecord = await GetOrCreateAgentAsync(aiProjectClient, agentName, modelName, agentInstructions);
FoundryAgent agent = aiProjectClient.AsAIAgent(agentRecord);

var storageDirectory = Path.Combine(Environment.CurrentDirectory, "ThreadStorage");
var conversationStore = new FileConversationStore(storageDirectory);

AgentSession thread;
string conversationId;
if (conversationStore.TryLoadConversationId(out conversationId) &&
    conversationId.StartsWith("conv_", StringComparison.Ordinal))
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("\n✓ Found saved Foundry conversation. Resuming conversation...\n");
    Console.ResetColor();

    thread = await agent.CreateSessionAsync(conversationId);
}
else
{
    if (conversationStore.Exists)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\nFound saved local or legacy thread state. Starting a fresh Foundry conversation...\n");
        Console.ResetColor();

        conversationStore.Delete();
    }

    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("\n→ No saved Foundry conversation found. Starting new conversation.\n");
    Console.ResetColor();

    thread = await agent.CreateConversationSessionAsync();
    conversationId = await GetConversationIdAsync(agent, thread);
    conversationStore.Save(conversationId);
}

Console.WriteLine($"[foundry-conversation] {conversationId}\n");

do
{
    ConsoleUi.WriteUserPrompt();

    var userInput = Console.ReadLine();

    if (userInput is null)
        break;

    if (string.IsNullOrWhiteSpace(userInput))
        continue;

    if (userInput.Trim().Equals("/clear", StringComparison.OrdinalIgnoreCase) ||
        userInput.Trim().Equals("/reset", StringComparison.OrdinalIgnoreCase))
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n⚠ Clearing local conversation pointer and starting a fresh Foundry conversation...\n");
        Console.ResetColor();

        conversationStore.Delete();

        thread = await agent.CreateConversationSessionAsync();
        conversationId = await GetConversationIdAsync(agent, thread);
        conversationStore.Save(conversationId);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"✓ New Foundry conversation started: {conversationId}\n");
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

} while (true);

static async Task<ProjectsAgentRecord> GetOrCreateAgentAsync(
    AIProjectClient aiProjectClient,
    string agentName,
    string modelName,
    string instructions)
{
    try
    {
        return await aiProjectClient.AgentAdministrationClient.GetAgentAsync(agentName);
    }
    catch (ClientResultException ex) when (ex.Status == 404)
    {
        ProjectsAgentDefinition agentDefinition = new DeclarativeAgentDefinition(modelName)
        {
            Instructions = instructions,
        };

        ProjectsAgentVersion agentVersion = await aiProjectClient.AgentAdministrationClient.CreateAgentVersionAsync(
            agentName: agentName,
            options: new(agentDefinition));

        return await aiProjectClient.AgentAdministrationClient.GetAgentAsync(agentVersion.Name);
    }
}

static async Task<string> GetConversationIdAsync(FoundryAgent agent, AgentSession thread)
{
    JsonElement serializedThread = await agent.SerializeSessionAsync(thread);
    if (serializedThread.TryGetProperty("conversationId", out var conversationIdElement) &&
        conversationIdElement.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(conversationIdElement.GetString()))
    {
        return conversationIdElement.GetString()!;
    }

    throw new InvalidOperationException("The Foundry conversation session did not include a conversationId.");
}

#pragma warning restore MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
