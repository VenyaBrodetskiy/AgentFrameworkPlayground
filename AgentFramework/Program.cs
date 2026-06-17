using Microsoft.Extensions.Configuration;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using Azure;
using Azure.AI.OpenAI;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Common;
using OpenAI.Chat;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

#pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning disable MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning disable MAAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

Console.WriteLine($"=== Running: {Assembly.GetEntryAssembly()?.GetName().Name} ===");

const string AgentId = "local-weather-agent";
const string TelemetrySourceName = AgentId;
const string MicrosoftAgentsAiTelemetrySourceName = "Experimental.Microsoft.Agents.AI";
const string MicrosoftExtensionsAiTelemetrySourceName = "Experimental.Microsoft.Extensions.AI";

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile("appsettings.Development.json", optional: false, reloadOnChange: true)
    .Build();

var modelName = configuration["ModelName"] ?? throw new ApplicationException("ModelName not found");
//var embedding = configuration["EmbeddingModel"] ?? throw new ApplicationException("ModelName not found");
var endpoint = configuration["Endpoint"] ?? throw new ApplicationException("Endpoint not found");
var apiKey = configuration["ApiKey"] ?? throw new ApplicationException("ApiKey not found");
var applicationInsightsConnectionString = configuration["ApplicationInsights:ConnectionString"];
if (string.IsNullOrWhiteSpace(applicationInsightsConnectionString))
{
    applicationInsightsConnectionString =
        configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]
        ?? Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");
}

var telemetryResource = ResourceBuilder
    .CreateDefault()
    .AddService(AgentId)
    .AddAttributes([
        new KeyValuePair<string, object>("gen_ai.agent.id", AgentId),
        new KeyValuePair<string, object>("gen_ai.agent.name", AgentId),
    ]);

var tracerProviderBuilder = Sdk
    .CreateTracerProviderBuilder()
    .SetResourceBuilder(telemetryResource)
    .SetSampler(new AlwaysOnSampler())
    .AddSource(TelemetrySourceName)
    .AddSource(MicrosoftAgentsAiTelemetrySourceName)
    .AddSource(MicrosoftExtensionsAiTelemetrySourceName);

if (string.IsNullOrWhiteSpace(applicationInsightsConnectionString))
{
    Console.WriteLine("APPLICATIONINSIGHTS_CONNECTION_STRING is not set; telemetry export is disabled.");
}
else
{
    tracerProviderBuilder.AddAzureMonitorTraceExporter(options =>
    {
        options.ConnectionString = applicationInsightsConnectionString;
        options.SamplingRatio = 1.0F;
        options.TracesPerSecond = null;
    });

    Console.WriteLine($"Exporting OpenTelemetry traces for '{AgentId}' to Azure Monitor.");
}

using var tracerProvider = tracerProviderBuilder.Build();
using var agentActivitySource = new ActivitySource(TelemetrySourceName);

[Description("Get the weather for a given location.")]
static string GetWeather([Description("The location to get the weather for.")] string location)
    => $"The weather in {location} is cloudy with a high of 15°C.";


var weatherFunction = AIFunctionFactory.Create(GetWeather);

var rawAgent = new AzureOpenAIClient(
        new Uri(endpoint),
        new AzureKeyCredential(apiKey))
    .GetChatClient(modelName)
    .AsAIAgent(new ChatClientAgentOptions
    {
        Id = AgentId,
        Name = AgentId,
        ChatOptions = new ChatOptions
        {
            Instructions = "say 'just a second' before answering question",
            Tools = [weatherFunction],
        },
    });

var agent = new OpenTelemetryAgent(rawAgent, TelemetrySourceName, autoWireChatClient: true)
{
    EnableSensitiveData = true,
};

var thread = await agent.CreateSessionAsync();

do
{
    ConsoleUi.WriteUserPrompt();

    var userInput = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(userInput))
    {
        break;
    }

    using var agentRunActivity = agentActivitySource.StartActivity("agent-run", ActivityKind.Internal);
    agentRunActivity?.SetTag("gen_ai.agent.id", AgentId);
    agentRunActivity?.SetTag("gen_ai.agent.name", AgentId);
    agentRunActivity?.SetTag("gen_ai.operation.name", "execute_agent");

    try
    {
        var streamingResponse =
            agent.RunStreamingAsync(userInput, thread);

        ConsoleUi.WriteAgentPrompt();

        await foreach (var chunk in streamingResponse)
        {
            ConsoleUi.WriteAgentChunk(chunk);
        }
        Console.WriteLine();

        agentRunActivity?.SetStatus(ActivityStatusCode.Ok);
    }
    catch (Exception ex)
    {
        agentRunActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        throw;
    }
    finally
    {
        agentRunActivity?.Stop();
        tracerProvider.ForceFlush(10000);
    }

} while (true);

#pragma warning restore OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning restore MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning restore MAAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
