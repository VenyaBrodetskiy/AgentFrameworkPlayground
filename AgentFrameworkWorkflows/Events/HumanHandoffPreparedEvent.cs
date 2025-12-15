using AgentFrameworkWorkflows.Models;
using Microsoft.Agents.AI.Workflows;

namespace AgentFrameworkWorkflows.Events;

internal sealed class HumanHandoffPreparedEvent(HumanHandoffPackage package) : WorkflowEvent(package)
{
    public HumanHandoffPackage Package { get; } = package;
}

