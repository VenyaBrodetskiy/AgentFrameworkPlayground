using AgentFrameworkWorkflows.Events;
using AgentFrameworkWorkflows.Models;
using Microsoft.Agents.AI.Workflows;

namespace AgentFrameworkWorkflows.Executors;

/// <summary>
/// Deterministic: applies simple policy rules (redaction + response mode + SLA).
/// </summary>
internal sealed class PolicyGateExecutor(string id) : Executor<IntakeContext, PolicyContext>(id)
{
    public override async ValueTask<PolicyContext> HandleAsync(IntakeContext message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var missingInfo = message.Intake.MissingInformation ?? [];
        var mode = missingInfo.Count > 0 ? ResponseMode.AskClarifyingQuestions : ResponseMode.DraftReply;

        var sla = message.Intake.Urgency switch
        {
            UrgencyLevel.High => "4h",
            UrgencyLevel.Normal => "24h",
            _ => "72h"
        };

        var complianceNotes = new List<string>();

        if (message.Email.ContainsPii)
        {
            complianceNotes.Add("PII detected in original email. Use only redacted content in replies.");
        }

        if (message.Intake.Intent is UserIntent.Refund or UserIntent.CancelOrder)
        {
            complianceNotes.Add("Do not promise a refund/cancellation. Confirm policy and ask for required order details.");
        }

        if (message.Intake.SecurityIssue)
        {
            complianceNotes.Add("Potential security issue. Avoid requesting sensitive data; escalate to security process if needed.");
        }

        // For this sample we simply use the pre-masked text as the "redacted" version.
        var policy = new PolicyDecision
        {
            Mode = mode,
            RedactedEmailText = message.Email.ModelSafeText,
            Sla = sla,
            ComplianceNotes = complianceNotes
        };

        var policyContext = new PolicyContext
        {
            Email = message.Email,
            Intake = message.Intake,
            Policy = policy
        };

        await context.AddEventAsync(new PolicyAppliedEvent(policyContext), cancellationToken);
        await context.QueueStateUpdateAsync(SupportRunState.KeyPolicy, policyContext, scopeName: SupportRunState.ScopeName);

        var isEscalation = policyContext.Intake.Sentiment == Sentiment.Negative && policyContext.Intake.Urgency == UrgencyLevel.High;
        var isClarification = policyContext.Policy.Mode == ResponseMode.AskClarifyingQuestions && !isEscalation;
        var isRefund = policyContext.Policy.Mode == ResponseMode.DraftReply && policyContext.Intake.Intent == UserIntent.Refund && !isEscalation;
        var isNormal = policyContext.Policy.Mode == ResponseMode.DraftReply && policyContext.Intake.Intent != UserIntent.Refund && !isEscalation;

        var route =
            isEscalation ? "Human escalation" :
            isClarification ? "Clarification email" :
            isRefund ? "Refund request (human review)" :
            isNormal ? "Normal reply" :
            "(no matching route)";

        await context.QueueStateUpdateAsync(SupportRunState.KeySelectedRoute, route, scopeName: SupportRunState.ScopeName);
        return policyContext;
    }
}

