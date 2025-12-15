using AgentFrameworkWorkflows.Events;
using AgentFrameworkWorkflows.Models;
using Microsoft.Agents.AI.Workflows;

namespace AgentFrameworkWorkflows.Executors;

/// <summary>
/// Deterministic: prepares a concise handoff package for a human support agent.
/// </summary>
internal sealed class HumanHandoffPrepExecutor(string id) : Executor<PolicyContext, HumanHandoffPackage>(id)
{
    public override async ValueTask<HumanHandoffPackage> HandleAsync(PolicyContext message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var steps = new List<string>
        {
            $"Review intake summary: {message.Intake.Summary}",
            $"Respect SLA target: {message.Policy.Sla}",
        };

        if (message.Intake.Intent is UserIntent.Refund)
        {
            steps.Add("Verify duplicate charge in payment system; do not promise a refund before verification.");
        }

        if (message.Email.DetectedOrderIds.Count > 0)
        {
            steps.Add($"Order ID(s) detected: {string.Join(", ", message.Email.DetectedOrderIds)}");
        }
        else
        {
            steps.Add("Order ID not detected; request it if needed.");
        }

        if (message.Email.ContainsPii)
        {
            steps.Add("PII present in original email; use redacted content only.");
        }

        var package = new HumanHandoffPackage
        {
            Queue = "Support - Priority",
            Summary = $"{message.Intake.Category} / {message.Intake.Intent} ({message.Intake.Urgency}, {message.Intake.Sentiment})",
            RecommendedNextSteps = steps,
            Sla = message.Policy.Sla,
            RedactedEmailText = message.Policy.RedactedEmailText
        };

        await context.AddEventAsync(new HumanHandoffPreparedEvent(package), cancellationToken);
        await context.QueueStateUpdateAsync(SupportRunState.KeyHumanHandoff, package, scopeName: SupportRunState.ScopeName);
        return package;
    }
}

