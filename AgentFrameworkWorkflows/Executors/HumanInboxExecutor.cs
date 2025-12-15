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
            .AddHandler<HumanHandoffPackage>(this.HandleAsync)
            .AddHandler<RefundRequest>(this.HandleAsync);

    public async ValueTask HandleAsync(HumanHandoffPackage message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        await context.AddEventAsync(new SentToHumanEvent($"Sent to human queue '{message.Queue}' (SLA {message.Sla})."), cancellationToken);

        await context.YieldOutputAsync(
            $"""
            Escalated to human support.

            Queue: {message.Queue}
            SLA: {message.Sla}
            Summary: {message.Summary}

            Suggested next steps:
            - {string.Join("\n- ", message.RecommendedNextSteps)}
            """,
            cancellationToken);
    }

    public async ValueTask HandleAsync(RefundRequest message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        await context.AddEventAsync(new SentToHumanEvent($"Refund request {message.RefundRequestId} sent to human reviewer (SLA {message.Sla})."), cancellationToken);

        await context.YieldOutputAsync(
            $"""
            Refund request created and sent for human review.

            RefundRequestId: {message.RefundRequestId}
            OrderId: {message.OrderId ?? "(unknown)"}
            SLA: {message.Sla}

            Customer acknowledgement (deterministic):
            {message.CustomerReply}
            """,
            cancellationToken);
    }
}

