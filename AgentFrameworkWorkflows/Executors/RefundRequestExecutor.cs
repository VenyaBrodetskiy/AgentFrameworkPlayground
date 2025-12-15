using AgentFrameworkWorkflows.Events;
using AgentFrameworkWorkflows.Models;
using Microsoft.Agents.AI.Workflows;

namespace AgentFrameworkWorkflows.Executors;

/// <summary>
/// Deterministic: builds a refund request payload when we have enough information.
/// This simulates "automation" (creating a request) without calling external systems.
/// </summary>
internal sealed class RefundRequestExecutor(string id) : Executor<PolicyContext, RefundRequest>(id)
{
    public override async ValueTask<RefundRequest> HandleAsync(PolicyContext message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var orderId = message.Email.DetectedOrderIds.FirstOrDefault();
        var requestId = $"RR-{Guid.NewGuid():N}"[..12].ToUpperInvariant();

        var customer = message.Email.From ?? "Customer";

        var customerReply =
            $"""
            Hello {customer},

            Thanks for reaching out. We’ve opened a refund review request ({requestId}) regarding the duplicate charge{(string.IsNullOrWhiteSpace(orderId) ? "" : $" for order {orderId}")}.
            Our team will verify the transaction details and follow up with the next steps.

            Best regards,
            Support Team
            """;

        var request = new RefundRequest
        {
            RefundRequestId = requestId,
            OrderId = orderId,
            Customer = customer,
            Reason = message.Intake.Summary,
            Sla = message.Policy.Sla,
            CustomerReply = customerReply
        };

        await context.AddEventAsync(new RefundRequestCreatedEvent(request), cancellationToken);
        return request;
    }
}

