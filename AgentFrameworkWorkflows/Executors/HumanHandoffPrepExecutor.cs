using System.Text.Json;
using AgentFrameworkWorkflows.Events;
using AgentFrameworkWorkflows.Models;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace AgentFrameworkWorkflows.Executors;

/// <summary>
/// Hybrid (Agent + Deterministic): Uses an LLM agent to generate contextual summary and recommendations,
/// while deterministically assigning queue, SLA, and redacted email text.
/// </summary>
internal sealed class HumanHandoffPrepExecutor : Executor<PolicyContext, HumanHandoffPackage>
{
    private readonly AIAgent _agent;
    private readonly AgentThread _thread;

    public HumanHandoffPrepExecutor(string id, IChatClient chatClient) : base(id)
    {
        ChatClientAgentOptions agentOptions = new()
        {
            ChatOptions = new()
            {
                Instructions =
                    """
                    You are a support operations assistant preparing handoff packages for human agents.
                    Generate a concise, contextual summary and actionable next steps based on the escalation context.
                    Be specific and consider all relevant details (intent, order IDs, PII handling, urgency, sentiment).
                    Return JSON that matches the schema exactly.
                    """,
                ResponseFormat = ChatResponseFormat.ForJsonSchema<HumanHandoffAgentOutput>()
            }
        };

        _agent = new ChatClientAgent(chatClient, agentOptions);
        _thread = _agent.GetNewThread();
    }

    public override async ValueTask<HumanHandoffPackage> HandleAsync(PolicyContext message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        // Agent-generated: summary and recommended next steps
        var orderInfo = message.Email.DetectedOrderIds.Count > 0
            ? $"Order ID(s): {string.Join(", ", message.Email.DetectedOrderIds)}"
            : "Order ID: not detected";

        var piiNote = message.Email.ContainsPii
            ? "⚠️ PII detected in original email; use redacted content only."
            : "No PII detected.";

        var prompt =
            $"""
            Prepare a handoff package for a human support agent.

            Context:
            - Category: {message.Intake.Category}
            - Intent: {message.Intake.Intent}
            - Urgency: {message.Intake.Urgency}
            - Sentiment: {message.Intake.Sentiment}
            - Intake Summary: {message.Intake.Summary}
            - {orderInfo}
            - PII Status: {piiNote}
            - SLA Target: {message.Policy.Sla}
            - Compliance Notes: {(message.Policy.ComplianceNotes.Count > 0 ? string.Join("; ", message.Policy.ComplianceNotes) : "none")}

            Customer Email (redacted):
            {message.Policy.RedactedEmailText}

            Generate:
            1. A concise summary that captures the key issue, customer state, and why escalation is needed.
            2. Specific, prioritized next steps that consider the context (e.g., verify refunds before promising, check order status, etc.).
            """;

        var response = await _agent.RunAsync(prompt, _thread, cancellationToken: cancellationToken);

        var agentOutput = JsonSerializer.Deserialize<HumanHandoffAgentOutput>(response.Text)
            ?? throw new InvalidOperationException("Failed to deserialize HumanHandoffAgentOutput.");

        // Deterministic: queue assignment, SLA, redacted text
        var package = new HumanHandoffPackage
        {
            Queue = "Support - Priority", // Could be made smarter based on category/intent
            Summary = agentOutput.Summary,
            RecommendedNextSteps = agentOutput.RecommendedNextSteps,
            Sla = message.Policy.Sla,
            RedactedEmailText = message.Policy.RedactedEmailText
        };

        await context.AddEventAsync(new HumanHandoffPreparedEvent(package), cancellationToken);
        await context.QueueStateUpdateAsync(SupportRunState.KeyHumanHandoff, package, scopeName: SupportRunState.ScopeName);
        return package;
    }
}

