using System.Reflection;
using Azure;
using Azure.AI.OpenAI;
using AgentFrameworkWorkflows.Executors;
using AgentFrameworkWorkflows.Events;
using Common;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;

Console.WriteLine($"=== Running: {Assembly.GetEntryAssembly()?.GetName().Name} ===");

// This sample is intentionally "simple and stupid":
// - Agent 1 extracts a structured analysis from a meeting transcript.
// - A deterministic executor turns that analysis into a structured "email request".
// - Agent 2 drafts a follow-up email based on that request.

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
    .Build();

var modelName = configuration["ModelName"] ?? throw new ApplicationException("ModelName not found");
//var embedding = configuration["EmbeddingModel"] ?? throw new ApplicationException("ModelName not found");
var endpoint = configuration["Endpoint"] ?? throw new ApplicationException("Endpoint not found");
var apiKey = configuration["ApiKey"] ?? throw new ApplicationException("ApiKey not found");

var chatClient = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey))
    .GetChatClient(modelName)
    .AsIChatClient();

var analyzer = new MeetingAnalyzerExecutor(id: "meeting_analyzer", chatClient);
var requestBuilder = new FollowUpEmailRequestBuilderExecutor(id: "request_builder");
var writer = new FollowUpEmailWriterExecutor(id: "email_writer", chatClient);

var workflow = new WorkflowBuilder(analyzer)
    .AddEdge(analyzer, requestBuilder)
    .AddEdge(requestBuilder, writer)
    .WithOutputFrom(writer)
    .Build();

Console.ForegroundColor = ConsoleColor.DarkCyan;
Console.WriteLine("\n=== Workflow (Mermaid) ===");
Console.ResetColor();
Console.WriteLine(workflow.ToMermaidString());

var meetingTranscript = """
                        During yesterday's quarterly planning meeting, Sarah (Product) reviewed the Q1 roadmap.
                        Key decisions: ship the new mobile app by Feb 15, increase marketing budget by 25%.
                        Action items:
                        1) Mike will draft the technical spec by Jan 20
                        2) Lisa will finalize vendor contracts before month end
                        Next follow-up meeting: Jan 30 at 10:00 AM.
                        """;

ConsoleUi.WriteSection("Input Transcript", meetingTranscript, ConsoleColor.Cyan);

await using StreamingRun run = await InProcessExecution.StreamAsync(workflow, input: meetingTranscript);
await foreach (var evt in run.WatchStreamAsync())
{
    switch (evt)
    {
        case MeetingAnalysisEvent e:
            ConsoleUi.WriteColoredLine($"\n[Analyzer] Summary: {e.Analysis.Summary}", ConsoleColor.Yellow);
            break;

        case FollowUpEmailRequestEvent e:
            ConsoleUi.WriteColoredLine($"\n[Deterministic] Subject: {e.Request.Subject}", ConsoleColor.DarkYellow);
            break;

        case EmailDraftedEvent:
            ConsoleUi.WriteColoredLine("\n[Writer] Draft created.", ConsoleColor.Green);
            break;

        case WorkflowOutputEvent outputEvent:
            ConsoleUi.WriteSectionTitle("Workflow Output", ConsoleColor.Cyan);
            Console.WriteLine(outputEvent.Data);
            break;

        case WorkflowErrorEvent errorEvent:
            ConsoleUi.WriteColoredLine($"\n=== Workflow Error ===\n{errorEvent}", ConsoleColor.Red);
            break;
    }
}