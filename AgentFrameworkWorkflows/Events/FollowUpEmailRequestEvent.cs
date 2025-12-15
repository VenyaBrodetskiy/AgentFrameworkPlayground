using AgentFrameworkWorkflows.Models;
using Microsoft.Agents.AI.Workflows;

namespace AgentFrameworkWorkflows.Events;

internal sealed class FollowUpEmailRequestEvent(FollowUpEmailRequest request) : WorkflowEvent(request)
{
    public FollowUpEmailRequest Request { get; } = request;
}

