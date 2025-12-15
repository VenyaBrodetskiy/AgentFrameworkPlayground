using System.Text.Json;
using AgentFrameworkWorkflows.Events;
using AgentFrameworkWorkflows.Models;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace AgentFrameworkWorkflows.Executors;

/// <summary>
/// Agent 1 (structured output): performs intake/classification for an inbound email.
/// </summary>
internal sealed class EmailIntakeExecutor : Executor<EmailDocument, IntakeContext>
{
    private readonly AIAgent _agent;
    private readonly AgentThread _thread;

    public EmailIntakeExecutor(string id, IChatClient chatClient) : base(id)
    {
        ChatClientAgentOptions agentOptions = new()
        {
            ChatOptions = new()
            {
                Instructions =
                    """
                    You are a customer support intake assistant.
                    Return JSON that matches the schema exactly.
                    Be concise and do not invent missing facts.
                    If the email already contains enough details to proceed, set missing_information to an empty list.
                    """,
                ResponseFormat = ChatResponseFormat.ForJsonSchema<IntakeResult>()
            }
        };

        _agent = new ChatClientAgent(chatClient, agentOptions);
        _thread = _agent.GetNewThread();
    }

    public override async ValueTask<IntakeContext> HandleAsync(EmailDocument message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var prompt =
            $"""
            Classify this inbound customer support email.

            Subject: {message.Subject ?? "(none)"}
            From: {message.From ?? "(unknown)"}

            Email:
            {message.ModelSafeText}
            """;

        var result = await _agent.RunAsync(prompt, _thread, cancellationToken: cancellationToken);

        var intake = JsonSerializer.Deserialize<IntakeResult>(result.Text)
            ?? throw new InvalidOperationException("Failed to deserialize IntakeResult.");

        var intakeContext = new IntakeContext { Email = message, Intake = intake };
        await context.AddEventAsync(new IntakeCompletedEvent(intakeContext), cancellationToken);

        return intakeContext;
    }
}

