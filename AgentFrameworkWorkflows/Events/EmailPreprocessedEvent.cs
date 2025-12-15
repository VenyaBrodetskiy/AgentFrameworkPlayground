using AgentFrameworkWorkflows.Models;
using Microsoft.Agents.AI.Workflows;

namespace AgentFrameworkWorkflows.Events;

internal sealed class EmailPreprocessedEvent(EmailDocument email) : WorkflowEvent(email)
{
    public EmailDocument Email { get; } = email;
}

