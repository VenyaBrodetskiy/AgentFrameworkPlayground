using System.ComponentModel;
using System.Reflection;
using Common;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;

#pragma warning disable SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

Console.WriteLine($"=== Running: {Assembly.GetEntryAssembly()?.GetName().Name} ===");

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile("appsettings.Development.json", optional: false, reloadOnChange: true)
    .Build();

var modelName = configuration["ModelName"] ?? throw new ApplicationException("ModelName not found");
var endpoint = configuration["Endpoint"] ?? throw new ApplicationException("Endpoint not found");
var apiKey = configuration["ApiKey"] ?? throw new ApplicationException("ApiKey not found");

[Description("Get the weather for a given location.")]
static string GetWeather([Description("The location to get the weather for.")] string location)
    => $"The weather in {location} is cloudy with a high of 15°C.";

var builder = Kernel.CreateBuilder()
    .AddAzureOpenAIChatCompletion(modelName, endpoint, apiKey);

var kernel = builder.Build();

kernel.Plugins.Add(KernelPluginFactory.CreateFromFunctions(
    "MyToolsPlugin",
    [ KernelFunctionFactory.CreateFromMethod(GetWeather) ]
    ));

var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

AzureOpenAIPromptExecutionSettings openAiPromptExecutionSettings = new()
{
    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
};

var history = new ChatHistory();
history.AddSystemMessage("say 'just a second' before answering question");

do
{
    ConsoleUi.WriteUserPrompt();

    var userInput = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(userInput) || userInput.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase))
    {
        break;
    }

    history.AddUserMessage(userInput);

    var streamingResponse = chatCompletionService.GetStreamingChatMessageContentsAsync(
        history,
        openAiPromptExecutionSettings,
        kernel);

    ConsoleUi.WriteAgentPrompt();

    var fullResponse = "";
    await foreach (var chunk in streamingResponse)
    {
        if (!string.IsNullOrEmpty(chunk.Content))
        {
            ConsoleUi.WriteAgentChunk(chunk.Content);
            fullResponse += chunk.Content;
        }
    }
    Console.WriteLine();

    if (!string.IsNullOrWhiteSpace(fullResponse))
    {
        history.AddMessage(AuthorRole.Assistant, fullResponse);
    }
} while (true);

#pragma warning restore SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
