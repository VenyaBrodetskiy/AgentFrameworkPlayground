using AgentFrameworkWorkflows.Models;
using Microsoft.Agents.AI.Workflows;

namespace AgentFrameworkWorkflows.Events;

internal sealed class MeetingAnalysisEvent(MeetingAnalysis analysis) : WorkflowEvent(analysis)
{
    public MeetingAnalysis Analysis { get; } = analysis;
}

