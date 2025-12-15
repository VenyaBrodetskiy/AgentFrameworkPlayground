namespace AgentFrameworkWorkflows.Models;

public sealed class FollowUpEmailRequest
{
    public required string Subject { get; init; }
    public required string Greeting { get; init; }
    public required string MeetingSummary { get; init; }
    public required List<string> Decisions { get; init; }
    public required List<string> ActionItemsBullets { get; init; }
    public string? NextMeeting { get; init; }
    public required string Closing { get; init; }
}

