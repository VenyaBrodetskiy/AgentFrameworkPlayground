using System.ComponentModel;
using System.Text.Json.Serialization;

namespace AgentFrameworkWorkflows.Models;

[Description("Agent-generated content for a human handoff package: contextual summary and recommended next steps.")]
public sealed class HumanHandoffAgentOutput
{
    [JsonPropertyName("summary")]
    [Description("A concise, contextual summary of the situation for the human agent. Should capture the key issue, customer state, and why escalation is needed.")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("recommended_next_steps")]
    [Description("A list of actionable, prioritized next steps for the human agent. Be specific and consider the context (intent, order IDs, PII handling, etc.).")]
    public List<string> RecommendedNextSteps { get; set; } = [];
}
