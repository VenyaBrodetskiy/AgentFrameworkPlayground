using AgentFrameworkWorkflows.Events;
using AgentFrameworkWorkflows.Models;
using Microsoft.Agents.AI.Workflows;

namespace AgentFrameworkWorkflows.Executors;

/// <summary>
/// Deterministic: simulates handing work to a human (support agent / finance reviewer).
/// Demonstrates a single executor handling multiple input message types.
/// </summary>
internal sealed class HumanInboxExecutor(string id) : Executor(id)
{
    protected override RouteBuilder ConfigureRoutes(RouteBuilder routeBuilder) =>
        routeBuilder
            .AddHandler<HumanHandoffPackage, FinalSignal>(HandleHandoffAsync)
            .AddHandler<RefundRequest, FinalSignal>(HandleRefundAsync);

    private async ValueTask<FinalSignal> HandleHandoffAsync(HumanHandoffPackage handoff, IWorkflowContext context)
    {
        await context.AddEventAsync(new SentToHumanEvent($"Sent to human queue '{handoff.Queue}' (SLA {handoff.Sla})."));

        var output =
            $"""
            Escalated to human support.

            Queue: {handoff.Queue}
            SLA: {handoff.Sla}
            Summary: {handoff.Summary}

            Suggested next steps:
            - {string.Join("\n- ", handoff.RecommendedNextSteps)}
            """;

        await context.QueueStateUpdateAsync(SupportRunState.KeyHumanInboxOutput, output, scopeName: SupportRunState.ScopeName);

        return new FinalSignal
        {
            TerminalExecutorId = Id,
            Note = "Sent escalation package to human inbox"
        };
    }

    private async ValueTask<FinalSignal> HandleRefundAsync(RefundRequest refund, IWorkflowContext context)
    {
        await context.AddEventAsync(new SentToHumanEvent($"Refund request {refund.RefundRequestId} sent to human reviewer (SLA {refund.Sla})."));

        var output =
            $"""
            Refund request created and sent for human review.

            RefundRequestId: {refund.RefundRequestId}
            OrderId: {refund.OrderId ?? "(unknown)"}
            SLA: {refund.Sla}

            Customer acknowledgement (deterministic):
            {refund.CustomerReply}
            """;

        await context.QueueStateUpdateAsync(SupportRunState.KeyHumanInboxOutput, output, scopeName: SupportRunState.ScopeName);

        return new FinalSignal
        {
            TerminalExecutorId = Id,
            Note = "Sent refund request to human inbox"
        };
    }
}

