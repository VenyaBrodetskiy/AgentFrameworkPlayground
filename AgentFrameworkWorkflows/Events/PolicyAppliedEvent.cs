using AgentFrameworkWorkflows.Models;
using Microsoft.Agents.AI.Workflows;

namespace AgentFrameworkWorkflows.Events;

internal sealed class PolicyAppliedEvent(PolicyContext context) : WorkflowEvent(context)
{
    public PolicyContext Context { get; } = context;
}

