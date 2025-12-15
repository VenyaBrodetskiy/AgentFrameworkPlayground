using System.Reflection;
using Azure;
using Azure.AI.OpenAI;
using AgentFrameworkWorkflows.Executors;
using AgentFrameworkWorkflows.Events;
using AgentFrameworkWorkflows.Models;
using Common;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;

Console.WriteLine($"=== Running: {Assembly.GetEntryAssembly()?.GetName().Name} ===");

// customer support email intake + compliant draft reply.
// - Deterministic executor: preprocess (clean + basic PII detection/masking)
// - Agent 1 (structured output): intake/classification (category/urgency/intent/missing info)
// - Deterministic executor: policy gate (redaction + response mode)
// - Agent 2 (structured output): responder (customer reply + internal notes)

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
    .Build();

var modelName = configuration["ModelName"] ?? throw new ApplicationException("ModelName not found");
var endpoint = configuration["Endpoint"] ?? throw new ApplicationException("Endpoint not found");
var apiKey = configuration["ApiKey"] ?? throw new ApplicationException("ApiKey not found");

var chatClient = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey))
    .GetChatClient(modelName)
    .AsIChatClient();

Workflow workflowForViz = BuildWorkflow(chatClient);
ConsoleUi.WriteSectionTitle("Workflow (Mermaid)", ConsoleColor.DarkCyan);
Console.WriteLine(workflowForViz.ToMermaidString());

var examples = new (string Title, string Email)[]
{
    (
        "Example 1 - Escalate to human (negative + high urgency)",
        """
        From: Noam Cohen <noam.cohen@gmail.com>
        Subject: URGENT: Charged twice - fix today (Order #A1B2C3)

        Hi Support,

        I’m really upset. I was charged twice for order #A1B2C3 and this is urgent.
        Please fix it today. My phone is +972 52-123-4567.

        Thanks,
        Noam
        """
    ),
    (
        "Example 2 - Clarification needed (missing details)",
        """
        From: Yael Levi <yael.levi@gmail.com>
        Subject: Refund request - duplicate charge

        Hi Support,

        I think I was charged twice yesterday, but I’m not sure which order it was.
        Can you help and tell me what info you need?

        Thanks,
        Yael
        """
    ),
    (
        "Example 3 - Refund request created (enough details)",
        """
        From: Eitan Mizrahi <eitan.mizrahi@gmail.com>
        Subject: Duplicate charge for Order #Z9Y8X7 (details included)

        Hi,

        I noticed a duplicate charge, but it’s not urgent.
        Order #Z9Y8X7
        Amounts: ₪499.90 charged twice
        Date: 2025-12-14
        Card: last 4 digits 1234
        Transaction IDs: TX-771122 and TX-771123

        Please review and refund the duplicate charge when you can.

        תודה,
        Eitan
        """
    ),
    (
        "Example 4 - Normal reply (not a refund)",
        """
        From: Dana Barak <dana.barak@gmail.com>
        Subject: Where is my shipment? (Order #H4J5K6)

        Hi Support,

        Can you please tell me the shipping status for order #H4J5K6?
        It’s been a few days and I’m not sure if it shipped.

        Thanks,
        Dana
        """
    ),
};

foreach (var (title, email) in examples)
{
    ConsoleUi.WriteSection(title, email, ConsoleColor.Cyan);

    var workflow = BuildWorkflow(chatClient);

    await using StreamingRun run = await InProcessExecution.StreamAsync(workflow, input: email);
    await foreach (var evt in run.WatchStreamAsync())
    {
        switch (evt)
        {
            case EmailPreprocessedEvent e:
                ConsoleUi.WriteColoredLine($"\n[Preprocess] Subject: {e.Email.Subject ?? "(none)"} | PII: {e.Email.ContainsPii}", ConsoleColor.DarkYellow);
                break;

            case IntakeCompletedEvent e:
                ConsoleUi.WriteColoredLine($"\n[Intake] Category: {e.Context.Intake.Category} | Urgency: {e.Context.Intake.Urgency} | Intent: {e.Context.Intake.Intent}", ConsoleColor.Yellow);
                break;

            case PolicyAppliedEvent e:
                ConsoleUi.WriteColoredLine($"\n[Policy] Mode: {e.Context.Policy.Mode} | SLA: {e.Context.Policy.Sla}", ConsoleColor.Magenta);
                break;

            case ResponseDraftedEvent:
                ConsoleUi.WriteColoredLine("\n[Responder] Draft created.", ConsoleColor.Green);
                break;

            case HumanHandoffPreparedEvent:
                ConsoleUi.WriteColoredLine("\n[Human Prep] Handoff package created.", ConsoleColor.DarkCyan);
                break;

            case RefundRequestCreatedEvent e:
                ConsoleUi.WriteColoredLine($"\n[Refund] Created {e.Request.RefundRequestId}.", ConsoleColor.DarkGreen);
                break;

            case SentToHumanEvent e:
                ConsoleUi.WriteColoredLine($"\n[Human] {e.Data}", ConsoleColor.DarkGray);
                break;

            case WorkflowOutputEvent outputEvent:
                ConsoleUi.WriteSectionTitle("Workflow Output", ConsoleColor.Cyan);
                Console.WriteLine(outputEvent.Data);
                break;

            case WorkflowErrorEvent errorEvent:
                ConsoleUi.WriteColoredLine($"\n=== Workflow Error ===\n{errorEvent}", ConsoleColor.Red);
                break;
        }
    }

    Console.WriteLine("\n------------------------------------------------------------\n");
}

static Workflow BuildWorkflow(IChatClient chatClient)
{
    var preprocess = new PreprocessEmailExecutor(id: "preprocess_email");
    var intake = new EmailIntakeExecutor(id: "intake_agent", chatClient);
    var policyGate = new PolicyGateExecutor(id: "policy_gate");
    var responder = new SupportResponderExecutor(id: "responder_agent", chatClient);
    var humanPrep = new HumanHandoffPrepExecutor(id: "human_prep");
    var refundRequest = new RefundRequestExecutor(id: "refund_request");
    var humanInbox = new HumanInboxExecutor(id: "human_inbox");

    return new WorkflowBuilder(preprocess)
        .AddEdge(preprocess, intake)
        .AddEdge(intake, policyGate)
        // Escalate: negative + high urgency -> prep a human handoff package -> human inbox
        .AddEdge<PolicyContext>(
            source: policyGate,
            target: humanPrep,
            condition: ctx => ctx is not null && ctx.Intake.Sentiment == Sentiment.Negative && ctx.Intake.Urgency == UrgencyLevel.High)
        .AddEdge(humanPrep, humanInbox)
        // Clarification: missing info -> draft questions email (agent)
        .AddEdge<PolicyContext>(
            source: policyGate,
            target: responder,
            condition: ctx => ctx is not null && ctx.Policy.Mode == ResponseMode.AskClarifyingQuestions
                             && !(ctx.Intake.Sentiment == Sentiment.Negative && ctx.Intake.Urgency == UrgencyLevel.High))
        // Refund request: no clarification needed -> create deterministic refund request -> human inbox review
        .AddEdge<PolicyContext>(
            source: policyGate,
            target: refundRequest,
            condition: ctx => ctx is not null
                             && ctx.Policy.Mode == ResponseMode.DraftReply
                             && ctx.Intake.Intent == UserIntent.Refund
                             && !(ctx.Intake.Sentiment == Sentiment.Negative && ctx.Intake.Urgency == UrgencyLevel.High))
        .AddEdge(refundRequest, humanInbox)
        // Default: no clarification needed, not escalated, not refund automation -> draft a normal reply (agent)
        .AddEdge<PolicyContext>(
            source: policyGate,
            target: responder,
            condition: ctx => ctx is not null
                             && ctx.Policy.Mode == ResponseMode.DraftReply
                             && ctx.Intake.Intent != UserIntent.Refund
                             && !(ctx.Intake.Sentiment == Sentiment.Negative && ctx.Intake.Urgency == UrgencyLevel.High))
        // Mark outputs. (WorkflowOutputEvent is emitted from configured output sources.)
        .WithOutputFrom(responder)
        .WithOutputFrom(humanInbox)
        .Build();
}