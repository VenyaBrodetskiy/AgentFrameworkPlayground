using AgentFrameworkWorkflows.Models;
using Microsoft.Agents.AI.Workflows;

namespace AgentFrameworkWorkflows.Events;

internal sealed class IntakeCompletedEvent(IntakeContext context) : WorkflowEvent(context)
{
    public IntakeContext Context { get; } = context;
}

