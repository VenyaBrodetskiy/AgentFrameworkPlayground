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
internal sealed class SupportResponderExecutor : Executor<PolicyContext, FinalSignal>
{
    private readonly AIAgent _agent;
    private readonly AgentThread _thread;
    private readonly string _executorId;

    public SupportResponderExecutor(string id, IChatClient chatClient) : base(id)
    {
        _executorId = id;

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

    public override async ValueTask<FinalSignal> HandleAsync(PolicyContext message, IWorkflowContext context, CancellationToken cancellationToken = default)
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

        var info = new ResponseDraftInfo
        {
            Mode = message.Policy.Mode,
            ClarifyingQuestionsCount = output.ClarifyingQuestions.Count,
            CustomerReplyCharacters = output.CustomerReply?.Length ?? 0,
            InternalNotesCharacters = output.InternalNotes?.Length ?? 0
        };

        await context.AddEventAsync(new ResponseDraftedEvent(info), cancellationToken);
        await context.QueueStateUpdateAsync(SupportRunState.KeyResponderOutput, output, scopeName: SupportRunState.ScopeName);

        return new FinalSignal
        {
            TerminalExecutorId = _executorId,
            Note = message.Policy.Mode == ResponseMode.AskClarifyingQuestions ? "Clarification email drafted" : "Customer reply drafted"
        };
    }
}

