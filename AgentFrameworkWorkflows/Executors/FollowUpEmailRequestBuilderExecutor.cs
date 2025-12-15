using AgentFrameworkWorkflows.Events;
using AgentFrameworkWorkflows.Models;
using Microsoft.Agents.AI.Workflows;

namespace AgentFrameworkWorkflows.Executors;

/// <summary>
/// Deterministic: Converts structured analysis into a structured email request.
/// </summary>
internal sealed class FollowUpEmailRequestBuilderExecutor(string id)
    : Executor<MeetingAnalysis, FollowUpEmailRequest>(id)
{
    public override async ValueTask<FollowUpEmailRequest> HandleAsync(MeetingAnalysis message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var actionBullets = message.ActionItems.Count == 0
            ? ["- No action items captured."]
            : message.ActionItems.Select(ai =>
            {
                var due = string.IsNullOrWhiteSpace(ai.DueDate) ? "" : $" (Due: {ai.DueDate})";
                return $"- {ai.Assignee}: {ai.Task}{due}";
            }).ToList();

        var subject = "Follow-up: Q1 planning meeting";
        if (!string.IsNullOrWhiteSpace(message.NextMeeting))
        {
            subject += $" (Next: {message.NextMeeting})";
        }

        var request = new FollowUpEmailRequest
        {
            Subject = subject,
            Greeting = "Hi team,",
            MeetingSummary = message.Summary,
            Decisions = message.Decisions,
            ActionItemsBullets = actionBullets,
            NextMeeting = message.NextMeeting,
            Closing = "Thanks,\n"
        };

        await context.AddEventAsync(new FollowUpEmailRequestEvent(request), cancellationToken);
        return request;
    }
}

