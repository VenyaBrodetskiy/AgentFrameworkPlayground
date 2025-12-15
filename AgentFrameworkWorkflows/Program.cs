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

//Workflow workflowForViz = BuildWorkflow(chatClient);
//ConsoleUi.WriteSectionTitle("Workflow (Mermaid)", ConsoleColor.DarkCyan);
//Console.WriteLine(workflowForViz.ToMermaidString());

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
    var trace = new RunTrace();

    await using StreamingRun run = await InProcessExecution.StreamAsync(workflow, input: email);

    await foreach (var evt in run.WatchStreamAsync())
    {
        switch (evt)
        {
            case EmailPreprocessedEvent e:
                trace.Email = e.Email;
                ConsoleUi.WriteColoredLine(
                    $"\n[Preprocess|Deterministic] Subject='{e.Email.Subject ?? "(none)"}' | PII={e.Email.ContainsPii} | OrderIds={FormatShortList(e.Email.DetectedOrderIds)} | Emails={e.Email.DetectedEmails.Count} | Phones={e.Email.DetectedPhones.Count}",
                    ConsoleColor.DarkYellow);
                Console.WriteLine("  Summary: Normalized email text and extracted identifiers (safe-to-send text prepared).");
                break;

            case IntakeCompletedEvent e:
                trace.IntakeContext = e.Context;
                ConsoleUi.WriteColoredLine(
                    $"\n[Intake|Agent] {e.Context.Intake.Category} | {e.Context.Intake.Urgency} | {e.Context.Intake.Sentiment} | {e.Context.Intake.Intent} | Missing={e.Context.Intake.MissingInformation.Count}",
                    ConsoleColor.Yellow);
                Console.WriteLine($"  Summary: {e.Context.Intake.Summary}");
                if (e.Context.Intake.MissingInformation.Count > 0)
                {
                    Console.WriteLine($"  MissingInfo: {FormatShortList(e.Context.Intake.MissingInformation)}");
                }
                break;

            case PolicyAppliedEvent e:
                trace.PolicyContext = e.Context;
                ConsoleUi.WriteColoredLine(
                    $"\n[Policy|Deterministic] Mode={e.Context.Policy.Mode} | SLA={e.Context.Policy.Sla} | Notes={e.Context.Policy.ComplianceNotes.Count}",
                    ConsoleColor.Magenta);

                var isEscalation = e.Context.Intake.Sentiment == Sentiment.Negative && e.Context.Intake.Urgency == UrgencyLevel.High;
                var isClarification = e.Context.Policy.Mode == ResponseMode.AskClarifyingQuestions && !isEscalation;
                var isRefundRequest = e.Context.Policy.Mode == ResponseMode.DraftReply && e.Context.Intake.Intent == UserIntent.Refund && !isEscalation;
                var isDraftReply = e.Context.Policy.Mode == ResponseMode.DraftReply && e.Context.Intake.Intent != UserIntent.Refund && !isEscalation;

                var route =
                    isEscalation ? "Human escalation (human_prep -> human_inbox)" :
                    isClarification ? "Clarification email (responder_agent)" :
                    isRefundRequest ? "Refund request creation (refund_request -> human_inbox)" :
                    isDraftReply ? "Normal reply (responder_agent)" :
                    "No matching route (check conditions)";

                var reason =
                    isEscalation ? "Sentiment=Negative AND Urgency=High" :
                    isClarification ? "Missing info => AskClarifyingQuestions" :
                    isRefundRequest ? "No missing info + Intent=Refund" :
                    isDraftReply ? "No missing info + Intent!=Refund" :
                    "N/A";

                Console.WriteLine($"  Summary: Selected route → {route} (reason: {reason}).");
                break;

            case ResponseDraftedEvent e:
                trace.ResponseInfo = e.Info;
                ConsoleUi.WriteColoredLine(
                    $"\n[Responder|Agent] Mode={e.Info.Mode} | Questions={e.Info.ClarifyingQuestionsCount} | ReplyChars={e.Info.CustomerReplyCharacters}",
                    ConsoleColor.Green);
                Console.WriteLine($"  Summary: Generated {(e.Info.Mode == ResponseMode.AskClarifyingQuestions ? "clarifying questions" : "a customer reply")}.");
                break;

            case HumanHandoffPreparedEvent e:
                trace.HandoffPackage = e.Package;
                ConsoleUi.WriteColoredLine(
                    $"\n[Human Prep|Deterministic] Queue='{e.Package.Queue}' | SLA={e.Package.Sla} | Summary='{e.Package.Summary}'",
                    ConsoleColor.DarkCyan);
                Console.WriteLine("  Summary: Prepared a concise package for a human to act on.");
                break;

            case RefundRequestCreatedEvent e:
                trace.RefundRequest = e.Request;
                ConsoleUi.WriteColoredLine(
                    $"\n[Refund|Deterministic] Id={e.Request.RefundRequestId} | OrderId={e.Request.OrderId ?? "(unknown)"} | SLA={e.Request.Sla}",
                    ConsoleColor.DarkGreen);
                Console.WriteLine("  Summary: Created a refund-review request payload for human verification.");
                break;

            case SentToHumanEvent e:
                trace.SentToHumanMessages.Add(e.Data?.ToString() ?? string.Empty);
                ConsoleUi.WriteColoredLine($"\n[Human Inbox|Deterministic] {e.Data}", ConsoleColor.DarkGray);
                Console.WriteLine("  Summary: Work item sent to a human queue for review.");
                break;

            case WorkflowOutputEvent outputEvent:
                trace.LastOutputSourceId = outputEvent.SourceId;
                trace.LastOutputData = outputEvent.Data;

                // Try to capture any customer-facing email content from the yielded output.
                if (outputEvent.Is<string>())
                {
                    var outputText = outputEvent.As<string>() ?? string.Empty;
                    trace.LastOutputText = outputText;
                    trace.CustomerEmailDraft ??= TryExtractCustomerEmailFromOutput(outputText);
                }
                else if (outputEvent.Data is not null)
                {
                    trace.LastOutputText = outputEvent.Data.ToString() ?? string.Empty;
                }

                ConsoleUi.WriteSectionTitle("Workflow Output (Summary)", ConsoleColor.Cyan);
                Console.WriteLine(RenderWorkflowSummary(trace));

                if (!string.IsNullOrWhiteSpace(trace.CustomerEmailDraft))
                {
                    ConsoleUi.WriteSectionTitle("Customer Email (Draft)", ConsoleColor.Cyan);
                    Console.WriteLine(trace.CustomerEmailDraft.Trim());
                }
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

static string FormatShortList(IReadOnlyCollection<string> items, int take = 2)
{
    if (items.Count == 0)
    {
        return "(none)";
    }

    var head = items.Take(take).ToList();
    var suffix = items.Count > take ? $" (+{items.Count - take} more)" : "";
    return string.Join(", ", head) + suffix;
}

static string RenderWorkflowSummary(RunTrace trace)
{
    var steps = new List<string> { "preprocess", "intake", "policy" };
    var route = "(unknown)";

    if (trace.PolicyContext is not null)
    {
        var isEscalation = trace.PolicyContext.Intake.Sentiment == Sentiment.Negative
                           && trace.PolicyContext.Intake.Urgency == UrgencyLevel.High;
        var isClarification = trace.PolicyContext.Policy.Mode == ResponseMode.AskClarifyingQuestions && !isEscalation;
        var isRefund = trace.PolicyContext.Policy.Mode == ResponseMode.DraftReply
                       && trace.PolicyContext.Intake.Intent == UserIntent.Refund
                       && !isEscalation;
        var isNormal = trace.PolicyContext.Policy.Mode == ResponseMode.DraftReply
                       && trace.PolicyContext.Intake.Intent != UserIntent.Refund
                       && !isEscalation;

        route =
            isEscalation ? "Human escalation" :
            isClarification ? "Clarification email" :
            isRefund ? "Refund request (human review)" :
            isNormal ? "Normal reply" :
            "(no matching route)";

        if (isEscalation)
        {
            steps.Add("human_prep");
            steps.Add("human_inbox");
        }
        else if (isRefund)
        {
            steps.Add("refund_request");
            steps.Add("human_inbox");
        }
        else
        {
            steps.Add("responder");
        }
    }

    var orderIds = trace.Email?.DetectedOrderIds is null ? "(unknown)" : FormatShortList(trace.Email.DetectedOrderIds);
    var pii = trace.Email?.ContainsPii is null ? "(unknown)" : trace.Email.ContainsPii.ToString();

    var outcome =
        trace.HandoffPackage is not null ? $"Escalated to queue '{trace.HandoffPackage.Queue}' (SLA {trace.HandoffPackage.Sla})." :
        trace.RefundRequest is not null ? $"Refund request {trace.RefundRequest.RefundRequestId} created (SLA {trace.RefundRequest.Sla})." :
        trace.ResponseInfo is not null ? $"Drafted {(trace.ResponseInfo.Mode == ResponseMode.AskClarifyingQuestions ? "clarification email/questions" : "customer reply")}." :
        "Output produced.";

    var outputSource = string.IsNullOrWhiteSpace(trace.LastOutputSourceId) ? "(unknown)" : trace.LastOutputSourceId;

    var handoffNextSteps = trace.HandoffPackage?.RecommendedNextSteps is { Count: > 0 }
        ? FormatShortList(trace.HandoffPackage.RecommendedNextSteps, take: 2)
        : null;

    return
        $"""
        Steps: {string.Join(" -> ", steps)}
        Route: {route}
        Extracted: PII={pii}, OrderIds={orderIds}
        Outcome: {outcome}
        {(handoffNextSteps is null ? "" : $"Handoff next steps: {handoffNextSteps}\n")}
        Output source: {outputSource}
        """;
}

static string? TryExtractCustomerEmailFromOutput(string output)
{
    if (string.IsNullOrWhiteSpace(output))
    {
        return null;
    }

    const string refundMarker = "Customer acknowledgement (deterministic):";
    var refundIdx = output.IndexOf(refundMarker, StringComparison.OrdinalIgnoreCase);
    if (refundIdx >= 0)
    {
        return output[(refundIdx + refundMarker.Length)..].Trim();
    }

    const string replyMarker = "Customer reply:";
    const string nextMarker = "Clarifying questions:";

    var replyIdx = output.IndexOf(replyMarker, StringComparison.OrdinalIgnoreCase);
    if (replyIdx < 0)
    {
        return null;
    }

    var afterReply = output[(replyIdx + replyMarker.Length)..].TrimStart();
    var nextIdx = afterReply.IndexOf(nextMarker, StringComparison.OrdinalIgnoreCase);
    if (nextIdx < 0)
    {
        return afterReply.Trim();
    }

    return afterReply[..nextIdx].Trim();
}

sealed class RunTrace
{
    public EmailDocument? Email { get; set; }
    public IntakeContext? IntakeContext { get; set; }
    public PolicyContext? PolicyContext { get; set; }
    public ResponseDraftInfo? ResponseInfo { get; set; }
    public HumanHandoffPackage? HandoffPackage { get; set; }
    public RefundRequest? RefundRequest { get; set; }

    public List<string> SentToHumanMessages { get; } = [];

    public string? LastOutputSourceId { get; set; }
    public object? LastOutputData { get; set; }
    public string? LastOutputText { get; set; }

    public string? CustomerEmailDraft { get; set; }
}