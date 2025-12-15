using System.Text;
using System.Text.Json;
using AgentFrameworkWorkflows.Events;
using AgentFrameworkWorkflows.Models;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace AgentFrameworkWorkflows.Executors;

/// <summary>
/// Agent 2 (structured output): drafts a customer reply + internal notes, following policy constraints.
/// </summary>
internal sealed class SupportResponderExecutor : Executor<PolicyContext>
{
    private readonly AIAgent _agent;
    private readonly AgentThread _thread;

    public SupportResponderExecutor(string id, IChatClient chatClient) : base(id)
    {
        ChatClientAgentOptions agentOptions = new()
        {
            ChatOptions = new()
            {
                Instructions =
                    """
                    You are a helpful customer support agent.
                    Write concise, friendly responses.
                    Follow policy and security constraints strictly.
                    Return JSON that matches the schema exactly.
                    """,
                ResponseFormat = ChatResponseFormat.ForJsonSchema<ResponderOutput>()
            }
        };

        _agent = new ChatClientAgent(chatClient, agentOptions);
        _thread = _agent.GetNewThread();
    }

    public override async ValueTask HandleAsync(PolicyContext message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var compliance = message.Policy.ComplianceNotes.Count == 0
            ? "(none)"
            : string.Join("\n- ", message.Policy.ComplianceNotes.Prepend(string.Empty)).Trim();

        var missing = message.Intake.MissingInformation.Count == 0
            ? "(none)"
            : string.Join("\n- ", message.Intake.MissingInformation.Prepend(string.Empty)).Trim();

        var prompt =
            $"""
            Draft a support response for this inbound email.

            Context:
            - Category: {message.Intake.Category}
            - Urgency: {message.Intake.Urgency} (SLA target: {message.Policy.Sla})
            - Sentiment: {message.Intake.Sentiment}
            - Intent: {message.Intake.Intent}
            - Mode: {message.Policy.Mode}
            - Summary: {message.Intake.Summary}
            - Missing information:
              {missing}
            - Compliance notes:
              {compliance}

            Inbound email (PII-masked):
            {message.Policy.RedactedEmailText}

            Output requirements:
            - customer_reply: a ready-to-send email reply (no markdown)
            - clarifying_questions: list (empty if Mode is DraftReply)
            - internal_notes: short bullet-style notes for the support agent
            """;

        var response = await _agent.RunAsync(prompt, _thread, cancellationToken: cancellationToken);

        var output = JsonSerializer.Deserialize<ResponderOutput>(response.Text)
            ?? throw new InvalidOperationException("Failed to deserialize ResponderOutput.");

        await context.AddEventAsync(new ResponseDraftedEvent("ok"), cancellationToken);

        var rendered = RenderForConsole(output);
        await context.YieldOutputAsync(rendered, cancellationToken);
    }

    private static string RenderForConsole(ResponderOutput output)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Customer reply:");
        sb.AppendLine(output.CustomerReply.Trim());
        sb.AppendLine();

        sb.AppendLine("Clarifying questions:");
        if (output.ClarifyingQuestions.Count == 0)
        {
            sb.AppendLine("- (none)");
        }
        else
        {
            foreach (var q in output.ClarifyingQuestions)
            {
                sb.AppendLine($"- {q}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("Internal notes:");
        sb.AppendLine(output.InternalNotes.Trim());

        return sb.ToString();
    }
}

