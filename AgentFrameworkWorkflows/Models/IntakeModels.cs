using System.ComponentModel;
using System.Text.Json.Serialization;

namespace AgentFrameworkWorkflows.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TicketCategory
{
    Billing,
    Bug,
    Account,
    Shipping,
    FeatureRequest,
    Other
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UrgencyLevel
{
    Low,
    Normal,
    High
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Sentiment
{
    Negative,
    Neutral,
    Positive
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UserIntent
{
    Refund,
    CancelOrder,
    ReportBug,
    HowTo,
    AccountHelp,
    Other
}

[Description("Structured intake result for a customer support email.")]
public sealed class IntakeResult
{
    [JsonPropertyName("category")]
    public TicketCategory Category { get; set; }

    [JsonPropertyName("urgency")]
    public UrgencyLevel Urgency { get; set; }

    [JsonPropertyName("sentiment")]
    public Sentiment Sentiment { get; set; }

    [JsonPropertyName("intent")]
    public UserIntent Intent { get; set; }

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("missing_information")]
    public List<string> MissingInformation { get; set; } = [];

    [JsonPropertyName("security_issue")]
    public bool SecurityIssue { get; set; }
}

public sealed class IntakeContext
{
    public required EmailDocument Email { get; init; }
    public required IntakeResult Intake { get; init; }
}

