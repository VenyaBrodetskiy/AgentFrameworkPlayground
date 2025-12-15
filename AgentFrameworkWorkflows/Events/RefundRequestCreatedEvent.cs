using AgentFrameworkWorkflows.Models;
using Microsoft.Agents.AI.Workflows;

namespace AgentFrameworkWorkflows.Events;

internal sealed class RefundRequestCreatedEvent(RefundRequest request) : WorkflowEvent(request)
{
    public RefundRequest Request { get; } = request;
}

