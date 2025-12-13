using System.ComponentModel;
using System.Reflection;
using Common;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;

// Agent Framework is currently experimental in SK.
#pragma warning disable SKEXP0001
#pragma warning disable SKEXP0010
#pragma warning disable SKEXP0110

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

var kernelBuilder = Kernel.CreateBuilder()
    .AddAzureOpenAIChatCompletion(modelName, endpoint, apiKey);

var kernel = kernelBuilder.Build();

kernel.Plugins.Add(KernelPluginFactory.CreateFromFunctions(
    "MyToolsPlugin",
    [KernelFunctionFactory.CreateFromMethod(GetWeather)]
));

var agent =
    new ChatCompletionAgent
    {
        Name = "SemanticKernelAgentFramework",
        Instructions = "say 'just a second' before answering question",
        Kernel = kernel,
        Arguments =
            new KernelArguments(
                new AzureOpenAIPromptExecutionSettings { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() }
            )
    };

ChatHistoryAgentThread agentThread = new();

do
{
    Console.WriteLine();
    ConsoleUi.WriteUserPrompt();
    
    var input = Console.ReadLine();

    var message = new ChatMessageContent(AuthorRole.User, input);

    Console.WriteLine();
    ConsoleUi.WriteAgentPrompt();

    await foreach (ChatMessageContent response in agent.InvokeAsync(message, agentThread))
    {
        if (!string.IsNullOrEmpty(response.Content))
        {
            ConsoleUi.WriteAgentChunk(response.Content);
        }
    }

    Console.WriteLine();
} while (true);

#pragma warning restore SKEXP0110
#pragma warning restore SKEXP0010
#pragma warning restore SKEXP0001
