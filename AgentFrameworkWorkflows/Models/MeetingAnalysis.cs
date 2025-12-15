using System.ComponentModel;
using System.Text.Json.Serialization;

namespace AgentFrameworkWorkflows.Models;

[Description("Structured meeting information")]
public sealed class MeetingAnalysis
{
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("decisions")]
    public List<string> Decisions { get; set; } = [];

    [JsonPropertyName("action_items")]
    public List<ActionItem> ActionItems { get; set; } = [];

    [JsonPropertyName("next_meeting")]
    public string? NextMeeting { get; set; }
}

public sealed class ActionItem
{
    [JsonPropertyName("assignee")]
    public string Assignee { get; set; } = string.Empty;

    [JsonPropertyName("task")]
    public string Task { get; set; } = string.Empty;

    [JsonPropertyName("due_date")]
    public string? DueDate { get; set; }
}

