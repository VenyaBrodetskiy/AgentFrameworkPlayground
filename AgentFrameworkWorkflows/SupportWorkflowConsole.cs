using AgentFrameworkWorkflows.Models;

namespace AgentFrameworkWorkflows;

internal static class SupportWorkflowConsole
{
    internal static string FormatShortList(IReadOnlyCollection<string> items, int take = 2)
    {
        if (items.Count == 0)
        {
            return "(none)";
        }

        var head = items.Take(take).ToList();
        var suffix = items.Count > take ? $" (+{items.Count - take} more)" : "";
        return string.Join(", ", head) + suffix;
    }

    internal static (string Route, string Reason) ExplainPolicyRoute(PolicyContext ctx)
    {
        if (ctx.Intake.Sentiment == Sentiment.Negative && ctx.Intake.Urgency == UrgencyLevel.High)
        {
            return ("Human escalation (human_prep -> human_inbox)", "Sentiment=Negative AND Urgency=High");
        }

        if (ctx.Policy.Mode == ResponseMode.AskClarifyingQuestions)
        {
            return ("Clarification email (responder_agent)", "Missing info => AskClarifyingQuestions");
        }

        if (ctx.Policy.Mode == ResponseMode.DraftReply && ctx.Intake.Intent == UserIntent.Refund)
        {
            return ("Refund request creation (refund_request -> human_inbox)", "No missing info + Intent=Refund");
        }

        if (ctx.Policy.Mode == ResponseMode.DraftReply)
        {
            return ("Normal reply (responder_agent)", "No missing info + Intent!=Refund");
        }

        return ("No matching route (check conditions)", "N/A");
    }
}
