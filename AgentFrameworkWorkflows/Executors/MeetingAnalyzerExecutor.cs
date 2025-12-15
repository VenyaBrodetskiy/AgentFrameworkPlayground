using System.Text.Json;
using AgentFrameworkWorkflows.Events;
using AgentFrameworkWorkflows.Models;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace AgentFrameworkWorkflows.Executors;

/// <summary>
/// Agent 1: Extracts structured meeting info from raw transcript.
/// </summary>
internal sealed class MeetingAnalyzerExecutor : Executor<string, MeetingAnalysis>
{
    private readonly AIAgent _agent;
    private readonly AgentThread _thread;

    public MeetingAnalyzerExecutor(string id, IChatClient chatClient) : base(id)
    {
        ChatClientAgentOptions agentOptions = new()
        {
            ChatOptions = new()
            {
                Instructions =
                    """
                    You extract structured information from meeting transcripts.
                    Return concise, accurate JSON that matches the schema exactly.
                    If a field is unknown, use an empty string or empty array.
                    """,
                ResponseFormat = ChatResponseFormat.ForJsonSchema<MeetingAnalysis>()
            }
        };

        _agent = new ChatClientAgent(chatClient, agentOptions);
        _thread = _agent.GetNewThread();
    }

    public override async ValueTask<MeetingAnalysis> HandleAsync(string message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var prompt =
            """
            Analyze the following meeting transcript and extract:
            - summary (1-2 sentences)
            - decisions (bullet list)
            - action_items (assignee, task, due_date if present)
            - next_meeting (string if present)

            Transcript:
            """ + message;

        var result = await _agent.RunAsync(prompt, _thread, cancellationToken: cancellationToken);

        var analysis = JsonSerializer.Deserialize<MeetingAnalysis>(result.Text)
            ?? throw new InvalidOperationException("Failed to deserialize MeetingAnalysis.");

        await context.AddEventAsync(new MeetingAnalysisEvent(analysis), cancellationToken);
        return analysis;
    }
}

