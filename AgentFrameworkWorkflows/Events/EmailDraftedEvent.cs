using Microsoft.Agents.AI.Workflows;

namespace AgentFrameworkWorkflows.Events;

internal sealed class EmailDraftedEvent(string note) : WorkflowEvent(note);

