using Microsoft.Extensions.Configuration;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json.Serialization;
using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;

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

var chatClient = new AzureOpenAIClient(
        new Uri(endpoint),
        new AzureKeyCredential(apiKey))
    .GetChatClient(modelName);

// Sample meeting transcript to extract structured information from
var meetingTranscript = @"
During yesterday's quarterly planning meeting, Sarah Johnson from the Product team 
presented the roadmap for Q1 2024. The meeting started at 2:00 PM and lasted about 90 minutes. 
Key decisions included: launching the new mobile app by February 15th, increasing the marketing 
budget by 25%, and hiring 3 additional engineers for the backend team. Action items assigned were:
1) Mike Chen will prepare the technical specification document by January 20th
2) Lisa Park needs to finalize vendor contracts before month end
3) The design team should complete UI mockups within 2 weeks
The next follow-up meeting is scheduled for January 30th at 10:00 AM.
";

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("\n=== Original Meeting Transcript ===");
Console.ResetColor();
Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine(meetingTranscript);
Console.ResetColor();

// Bad example: the prompt repeats the output keys instead of giving meaningful analysis guidance.
var badPrompt = $"""
Please analyze this meeting transcript and return JSON with exactly these keys:
- date: the date of the meeting
- duration_minutes: meeting length in minutes
- attendees: list of people in the meeting
- decisions: list of decisions made in the meeting
- action_items: list of objects with assignee, task, and due_date

Meeting transcript:
{meetingTranscript}
""";

var badPromptAgent = chatClient.AsAIAgent(name: "BadPromptMeetingAnalyzer");
var badPromptThread = await badPromptAgent.CreateSessionAsync();
var badPromptResponse = await badPromptAgent.RunAsync<MeetingAnalysis>(
    badPrompt,
    badPromptThread);

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("\n=== Example 1: Bad Prompt (Manual Keys In Prompt) ===");
Console.ResetColor();
Console.WriteLine(badPrompt);

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("\n=== Example 1: Structured Output (JSON) ===");
Console.ResetColor();
Console.WriteLine(badPromptResponse.Result.Dump());

// Better example: the schema from MeetingAnalysis tells the model what shape to return.
var schemaOnlyAgent = chatClient.AsAIAgent(name: "SchemaOnlyMeetingAnalyzer");
var schemaOnlyThread = await schemaOnlyAgent.CreateSessionAsync();
var schemaOnlyResponse = await schemaOnlyAgent.RunAsync<MeetingAnalysis>(
    meetingTranscript,
    schemaOnlyThread);

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("\n=== Example 2: Schema Only (Transcript As Input) ===");
Console.ResetColor();
Console.WriteLine("Prompt sent to the model: only the raw meeting transcript.");

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("\n=== Example 2: Structured Output (JSON) ===");
Console.ResetColor();
Console.WriteLine(schemaOnlyResponse.Result.Dump());

#pragma warning restore OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning restore MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

/// <summary>
/// Represents structured information extracted from a meeting transcript.
/// </summary>
[Description("Structured meeting information")]
public class MeetingAnalysis
{
    [JsonPropertyName("date")]
    public string? Date { get; set; }

    [JsonPropertyName("duration_minutes")]
    public int? DurationMinutes { get; set; }

    [JsonPropertyName("attendees")]
    public List<string>? Attendees { get; set; }

    [JsonPropertyName("decisions")]
    public List<string>? Decisions { get; set; }

    [JsonPropertyName("action_items")]
    public List<ActionItem>? ActionItems { get; set; }
}

/// <summary>
/// Represents an action item from the meeting.
/// </summary>
public class ActionItem
{
    [JsonPropertyName("assignee")]
    public string? Assignee { get; set; }

    [JsonPropertyName("task")]
    public string? Task { get; set; }

    [JsonPropertyName("due_date")]
    public string? DueDate { get; set; }
}
