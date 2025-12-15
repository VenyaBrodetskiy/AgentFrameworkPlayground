namespace AgentFrameworkWorkflows.Models;

public sealed class HumanHandoffPackage
{
    public required string Queue { get; init; }

    public required string Summary { get; init; }

    public List<string> RecommendedNextSteps { get; init; } = [];

    public required string Sla { get; init; }

    public required string RedactedEmailText { get; init; }
}

