using Microsoft.Extensions.Configuration;
using OpenAI;
using System.ComponentModel;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Common;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile("appsettings.Development.json", optional: false, reloadOnChange: true)
    .Build();

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource("agent-telemetry-source")
    .AddConsoleExporter()
    .Build();

var modelName = configuration["ModelName"] ?? throw new ApplicationException("ModelName not found");
//var embedding = configuration["EmbeddingModel"] ?? throw new ApplicationException("ModelName not found");
var endpoint = configuration["Endpoint"] ?? throw new ApplicationException("Endpoint not found");
var apiKey = configuration["ApiKey"] ?? throw new ApplicationException("ApiKey not found");

[Description("Get the weather for a given location.")]
static string GetWeather([Description("The location to get the weather for.")] string location)
    => $"The weather in {location} is cloudy with a high of 15°C.";

#pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning disable MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

AIFunction weatherFunction = AIFunctionFactory.Create(GetWeather);
//AIFunction approvalRequiredWeatherFunction = new ApprovalRequiredAIFunction(weatherFunction);

var agent = new AzureOpenAIClient(
        new Uri(endpoint),
        new AzureKeyCredential(apiKey))
    .GetChatClient(modelName)
    .CreateAIAgent(
        instructions: "say 'just a second' before answering question",
        tools: [weatherFunction],
        name: "myagent")
    .AsBuilder()
    .UseOpenTelemetry(sourceName: "agent-telemetry-source")
    .Build();
#pragma warning restore OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning restore MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

var thread = agent.GetNewThread();

do
{
    ConsoleUi.WriteUserPrompt();

    var userInput = Console.ReadLine();
    if (userInput == "exit")
    {
        break;
    }

    var streamingResponse =
        agent.RunStreamingAsync(userInput!, thread);

    ConsoleUi.WriteAgentPrompt();

    await foreach (var chunk in streamingResponse)
    {
        ConsoleUi.WriteAgentChunk(chunk);
    }
    Console.WriteLine();

    // to see thread structure
    var serializedThread = thread.Serialize();
    //var history = JsonSerializer.Serialize(serializedThread);
} while (true);