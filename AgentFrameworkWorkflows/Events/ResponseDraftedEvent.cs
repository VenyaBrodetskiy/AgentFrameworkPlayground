using AgentFrameworkWorkflows.Models;
using Microsoft.Agents.AI.Workflows;

namespace AgentFrameworkWorkflows.Events;

internal sealed class ResponseDraftedEvent(ResponseDraftInfo info) : WorkflowEvent(info)
{
    public ResponseDraftInfo Info { get; } = info;
}

