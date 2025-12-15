using System.Text.Json.Serialization;

namespace AgentFrameworkWorkflows.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ResponseMode
{
    DraftReply,
    AskClarifyingQuestions
}

public sealed class PolicyDecision
{
    public required ResponseMode Mode { get; init; }
    public required string RedactedEmailText { get; init; }
    public required string Sla { get; init; }
    public List<string> ComplianceNotes { get; init; } = [];
}

public sealed class PolicyContext
{
    public required EmailDocument Email { get; init; }
    public required IntakeResult Intake { get; init; }
    public required PolicyDecision Policy { get; init; }
}

