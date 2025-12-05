using Microsoft.Extensions.Configuration;
using OpenAI;
using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.AI;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile("appsettings.Development.json", optional: false, reloadOnChange: true)
    .Build();

var modelName = configuration["ModelName"] ?? throw new ApplicationException("ModelName not found");
//var embedding = configuration["EmbeddingModel"] ?? throw new ApplicationException("ModelName not found");
//var endpoint = configuration["Endpoint"] ?? throw new ApplicationException("Endpoint not found");
var apiKey = configuration["ApiKey"] ?? throw new ApplicationException("ApiKey not found");

[Description("Get the weather for a given location.")]
static string GetWeather([Description("The location to get the weather for.")] string location)
    => $"The weather in {location} is cloudy with a high of 15°C.";

#pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
var agent = new OpenAIClient(apiKey)
    .GetChatClient(modelName)
    .CreateAIAgent(
        instructions: "always say 'just a second' before answering question", 
        tools: [AIFunctionFactory.Create(GetWeather)],
        name: "myagent");
#pragma warning restore OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

var thread = agent.GetNewThread();

do
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.Write("Me > ");
    Console.ResetColor();

    var userInput = Console.ReadLine();
    if (userInput == "exit")
    {
        break;
    }

    var streamingResponse =
        agent.RunStreamingAsync(userInput!, thread);

    Console.ForegroundColor = ConsoleColor.Green;
    Console.Write("Agent > ");
    Console.ResetColor();

    await foreach (var chunk in streamingResponse)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write(chunk);
        Console.ResetColor();
    }
    Console.WriteLine();

    // to see thread structure
    var serializedThread = thread.Serialize();
    var history = JsonSerializer.Serialize(serializedThread);
} while (true);