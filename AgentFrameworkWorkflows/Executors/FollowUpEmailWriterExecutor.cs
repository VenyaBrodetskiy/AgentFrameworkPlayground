using AgentFrameworkWorkflows.Events;
using AgentFrameworkWorkflows.Models;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace AgentFrameworkWorkflows.Executors;

/// <summary>
/// Agent 2: Drafts a follow-up email based on the request.
/// </summary>
internal sealed class FollowUpEmailWriterExecutor : Executor<FollowUpEmailRequest>
{
    private readonly AIAgent _agent;
    private readonly AgentThread _thread;

    public FollowUpEmailWriterExecutor(string id, IChatClient chatClient) : base(id)
    {
        ChatClientAgentOptions agentOptions = new()
        {
            ChatOptions = new()
            {
                Instructions =
                    """
                    You write clear, professional follow-up emails after meetings.
                    Keep it concise, friendly, and action-oriented.
                    """,
            }
        };

        _agent = new ChatClientAgent(chatClient, agentOptions);
        _thread = _agent.GetNewThread();
    }

    public override async ValueTask HandleAsync(FollowUpEmailRequest message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var prompt =
            $"""
            Draft an email using exactly this structure:

            Subject: <subject>

            <greeting>

            Summary:
            <meeting summary>

            Decisions:
            <bullets>

            Action items:
            <bullets>

            Next meeting:
            <one line, or 'TBD' if missing>

            <closing>

            Use these inputs:
            Subject: {message.Subject}
            Greeting: {message.Greeting}
            MeetingSummary: {message.MeetingSummary}
            Decisions: {string.Join(" | ", message.Decisions)}
            ActionItems: {string.Join(" | ", message.ActionItemsBullets)}
            NextMeeting: {(string.IsNullOrWhiteSpace(message.NextMeeting) ? "TBD" : message.NextMeeting)}
            Closing: {message.Closing}
            """;

        var result = await _agent.RunAsync(prompt, _thread, cancellationToken: cancellationToken);

        await context.AddEventAsync(new EmailDraftedEvent("ok"), cancellationToken);
        await context.YieldOutputAsync(result.Text, cancellationToken);
    }
}

