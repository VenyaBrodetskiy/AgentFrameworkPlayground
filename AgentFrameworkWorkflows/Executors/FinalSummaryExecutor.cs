using System.Text;
using AgentFrameworkWorkflows.Models;
using Microsoft.Agents.AI.Workflows;

namespace AgentFrameworkWorkflows.Executors;

/// <summary>
/// Deterministic: reads shared state to produce a single, consolidated output.
/// This keeps Program.cs simple and makes the workflow graph "self-reporting".
/// </summary>
internal sealed class FinalSummaryExecutor(string id) : Executor<FinalSignal>(id)
{
    public override async ValueTask HandleAsync(FinalSignal message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var email = await context.ReadStateAsync<EmailDocument>(SupportRunState.KeyEmail, scopeName: SupportRunState.ScopeName);
        var intake = await context.ReadStateAsync<IntakeContext>(SupportRunState.KeyIntake, scopeName: SupportRunState.ScopeName);
        var policy = await context.ReadStateAsync<PolicyContext>(SupportRunState.KeyPolicy, scopeName: SupportRunState.ScopeName);
        var route = await context.ReadStateAsync<string>(SupportRunState.KeySelectedRoute, scopeName: SupportRunState.ScopeName);

        var responderOutput = await context.ReadStateAsync<ResponderOutput>(SupportRunState.KeyResponderOutput, scopeName: SupportRunState.ScopeName);
        var refundRequest = await context.ReadStateAsync<RefundRequest>(SupportRunState.KeyRefundRequest, scopeName: SupportRunState.ScopeName);
        var handoff = await context.ReadStateAsync<HumanHandoffPackage>(SupportRunState.KeyHumanHandoff, scopeName: SupportRunState.ScopeName);

        var customerEmail =
            !string.IsNullOrWhiteSpace(refundRequest?.CustomerReply) ? refundRequest!.CustomerReply :
            !string.IsNullOrWhiteSpace(responderOutput?.CustomerReply) ? responderOutput!.CustomerReply :
            null;

        var sb = new StringBuilder();

        sb.AppendLine("Workflow summary:");
        sb.AppendLine($"- Flow: preprocess -> intake -> policy -> {message.TerminalExecutorId} -> final_summary");
        sb.AppendLine($"- Route: {route ?? "(unknown)"}");
        sb.AppendLine($"- Final step: {message.TerminalExecutorId}{(string.IsNullOrWhiteSpace(message.Note) ? "" : $" ({message.Note})")}");

        if (policy?.Policy?.Sla is not null)
        {
            sb.AppendLine($"- SLA: {policy.Policy.Sla}");
        }

        sb.AppendLine();
        sb.AppendLine("Email to customer:");

        sb.AppendLine(string.IsNullOrWhiteSpace(customerEmail) ? "(none)" : customerEmail.Trim());

        await context.YieldOutputAsync(sb.ToString(), cancellationToken);
    }

    private static string FormatShortList(IReadOnlyCollection<string> items, int take = 2)
    {
        if (items.Count == 0)
        {
            return "(none)";
        }

        var head = items.Take(take).ToList();
        var suffix = items.Count > take ? $" (+{items.Count - take} more)" : "";
        return string.Join(", ", head) + suffix;
    }
}
