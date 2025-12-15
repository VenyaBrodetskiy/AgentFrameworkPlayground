using System.ComponentModel;
using System.Text.Json.Serialization;

namespace AgentFrameworkWorkflows.Models;

[Description("A support agent draft response with customer-facing text and internal notes.")]
public sealed class ResponderOutput
{
    [JsonPropertyName("customer_reply")]
    public string CustomerReply { get; set; } = string.Empty;

    [JsonPropertyName("clarifying_questions")]
    public List<string> ClarifyingQuestions { get; set; } = [];

    [JsonPropertyName("internal_notes")]
    public string InternalNotes { get; set; } = string.Empty;
}

