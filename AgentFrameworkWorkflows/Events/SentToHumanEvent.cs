using Microsoft.Agents.AI.Workflows;

namespace AgentFrameworkWorkflows.Events;

internal sealed class SentToHumanEvent(string message) : WorkflowEvent(message);

