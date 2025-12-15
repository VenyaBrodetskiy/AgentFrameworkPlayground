using Microsoft.Agents.AI.Workflows;

namespace AgentFrameworkWorkflows.Events;

internal sealed class ResponseDraftedEvent(string note) : WorkflowEvent(note);

