using Microsoft.Extensions.Configuration;
using System.ComponentModel;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Common;
using OpenAI;

#pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning disable MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile("appsettings.Development.json", optional: false, reloadOnChange: true)
    .Build();

var modelName = configuration["ModelName"] ?? throw new ApplicationException("ModelName not found");
//var embedding = configuration["EmbeddingModel"] ?? throw new ApplicationException("ModelName not found");
var endpoint = configuration["Endpoint"] ?? throw new ApplicationException("Endpoint not found");
var apiKey = configuration["ApiKey"] ?? throw new ApplicationException("ApiKey not found");

[Description("Get the weather for a given location.")]
static string GetWeather([Description("The location to get the weather for.")] string location)
    => $"The weather in {location} is cloudy with a high of 15°C.";


var weatherFunction = AIFunctionFactory.Create(GetWeather);

var agent = new AzureOpenAIClient(
        new Uri(endpoint),
        new AzureKeyCredential(apiKey))
    .GetChatClient(modelName)
    .CreateAIAgent(
        instructions: "say 'just a second' before answering question",
        tools: [weatherFunction],
        name: "myagent");

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
