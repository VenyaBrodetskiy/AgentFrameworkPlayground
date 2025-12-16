using System.Reflection;
using AgentFrameworkWorkflows;
using AgentFrameworkWorkflows.Events;
using AgentFrameworkWorkflows.Executors;
using AgentFrameworkWorkflows.Models;
using Azure;
using Azure.AI.OpenAI;
using Common;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

Console.WriteLine($"=== Running: {Assembly.GetEntryAssembly()?.GetName().Name} ===");

// Set up OpenTelemetry tracing for workflow visualization
var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("AgentFrameworkWorkflows"))
    .AddSource("Microsoft.Agents.AI.*")
    .SetSampler(new AlwaysOnSampler())
    .AddOtlpExporter(options =>
    {
        options.Endpoint = new Uri("http://localhost:4319");
        options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
    })
    .Build();

// Customer support email workflow (concise):
// 1) Preprocess (deterministic): clean text + detect/mask basic PII + extract identifiers (order id, email, phone).
// 2) Intake (agent): classify category/urgency/sentiment/intent + list missing info.
// 3) Policy (deterministic): decide response mode + SLA + compliance notes + select route.
// 4) Route (conditional):
//    - Escalate: human_prep -> human_inbox
//    - Refund automation: refund_request -> human_inbox
//    - Otherwise: responder (agent) drafts email
// 5) Final summary (deterministic): reads shared state and prints final flow + email (if any).

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

static Workflow BuildWorkflow(IChatClient chatClient)
{
    var preprocess = new PreprocessEmailExecutor(id: "preprocess_email");
    var intake = new EmailIntakeExecutor(id: "intake_agent", chatClient);
    var policyGate = new PolicyGateExecutor(id: "policy_gate");
    var responder = new SupportResponderExecutor(id: "responder_agent", chatClient);
    var humanPrep = new HumanHandoffPrepExecutor(id: "human_prep", chatClient);
    var refundRequest = new RefundRequestExecutor(id: "refund_request");
    var humanInbox = new HumanInboxExecutor(id: "human_inbox");
    var finalSummary = new FinalSummaryExecutor(id: "final_summary");

    return new WorkflowBuilder(preprocess)
        .AddEdge(preprocess, intake)
        .AddEdge(intake, policyGate)
        // Escalate: negative + high urgency -> prep a human handoff package -> human inbox
        .AddEdge<PolicyContext>(
            source: policyGate,
            target: humanPrep,
            condition: ctx => ctx is not null
                              && ctx.Intake.Sentiment == Sentiment.Negative
                              && ctx.Intake.Urgency == UrgencyLevel.High)
        .AddEdge(humanPrep, humanInbox)
        .AddEdge(humanInbox, finalSummary)
        // Clarification: missing info -> draft questions email (agent)
        .AddEdge<PolicyContext>(
            source: policyGate,
            target: responder,
            condition: ctx => ctx is not null
                              && ctx.Policy.Mode == ResponseMode.AskClarifyingQuestions
                              && !(ctx.Intake.Sentiment == Sentiment.Negative && ctx.Intake.Urgency == UrgencyLevel.High))
        .AddEdge(responder, finalSummary)
        // Refund request: no clarification needed -> create refund request -> human inbox review
        .AddEdge<PolicyContext>(
            source: policyGate,
            target: refundRequest,
            condition: ctx => ctx is not null
                              && ctx.Policy.Mode == ResponseMode.DraftReply
                              && ctx.Intake.Intent == UserIntent.Refund
                              && !(ctx.Intake.Sentiment == Sentiment.Negative && ctx.Intake.Urgency == UrgencyLevel.High))
        .AddEdge(refundRequest, humanInbox)
        // Default: normal reply -> responder
        .AddEdge<PolicyContext>(
            source: policyGate,
            target: responder,
            condition: ctx => ctx is not null
                              && ctx.Policy.Mode == ResponseMode.DraftReply
                              && ctx.Intake.Intent != UserIntent.Refund
                              && !(ctx.Intake.Sentiment == Sentiment.Negative && ctx.Intake.Urgency == UrgencyLevel.High))
        // Only final_summary yields output
        .WithOutputFrom(finalSummary)
        .Build();
}

foreach (var (title, email) in SampleEmails.Examples)
{
    ConsoleUi.WriteSection(title, email);

    var workflow = BuildWorkflow(chatClient);

    ConsoleUi.WriteSectionTitle("Workflow log (reasoning)", ConsoleColor.DarkGray);

    await using StreamingRun run = await InProcessExecution.StreamAsync(workflow, input: email);

    await foreach (var evt in run.WatchStreamAsync())
    {
        switch (evt)
        {
            case EmailPreprocessedEvent e:
                ConsoleUi.WriteColoredLine(
                    $"\n[Preprocess|Deterministic] Subject='{e.Email.Subject ?? "(none)"}' | PII={e.Email.ContainsPii} | OrderIds={SupportWorkflowConsole.FormatShortList(e.Email.DetectedOrderIds)} | Emails={e.Email.DetectedEmails.Count} | Phones={e.Email.DetectedPhones.Count}",
                    ConsoleColor.DarkYellow);
                Console.WriteLine("  Summary: Normalized email text and extracted identifiers (safe-to-send text prepared).");
                break;

            case IntakeCompletedEvent e:
                ConsoleUi.WriteColoredLine(
                    $"\n[Intake|Agent] {e.Context.Intake.Category} | {e.Context.Intake.Urgency} | {e.Context.Intake.Sentiment} | {e.Context.Intake.Intent} | Missing={e.Context.Intake.MissingInformation.Count}",
                    ConsoleColor.Yellow);
                Console.WriteLine($"  Summary: {e.Context.Intake.Summary}");
                if (e.Context.Intake.MissingInformation.Count > 0)
                {
                    Console.WriteLine($"  MissingInfo: {SupportWorkflowConsole.FormatShortList(e.Context.Intake.MissingInformation)}");
                }
                break;

            case PolicyAppliedEvent e:
                ConsoleUi.WriteColoredLine(
                    $"\n[Policy|Deterministic] Mode={e.Context.Policy.Mode} | SLA={e.Context.Policy.Sla} | Notes={e.Context.Policy.ComplianceNotes.Count}",
                    ConsoleColor.Magenta);

                var (route, reason) = SupportWorkflowConsole.ExplainPolicyRoute(e.Context);
                Console.WriteLine($"  Summary: Selected route → {route} (reason: {reason}).");
                break;

            case ResponseDraftedEvent e:
                ConsoleUi.WriteColoredLine(
                    $"\n[Responder|Agent] Mode={e.Info.Mode} | Questions={e.Info.ClarifyingQuestionsCount} | ReplyChars={e.Info.CustomerReplyCharacters}",
                    ConsoleColor.Green);
                Console.WriteLine($"  Summary: Generated {(e.Info.Mode == ResponseMode.AskClarifyingQuestions ? "clarifying questions" : "a customer reply")}.");
                break;

            case HumanHandoffPreparedEvent e:
                ConsoleUi.WriteColoredLine(
                    $"\n[Human Prep|Hybrid] Queue='{e.Package.Queue}' | SLA={e.Package.Sla} | Steps={e.Package.RecommendedNextSteps.Count}",
                    ConsoleColor.DarkCyan);
                Console.WriteLine($"  Summary: {e.Package.Summary}");
                Console.WriteLine($"  Next Steps: {SupportWorkflowConsole.FormatShortList(e.Package.RecommendedNextSteps)}");
                break;

            case RefundRequestCreatedEvent e:
                ConsoleUi.WriteColoredLine(
                    $"\n[Refund|Deterministic] Id={e.Request.RefundRequestId} | OrderId={e.Request.OrderId ?? "(unknown)"} | SLA={e.Request.Sla}",
                    ConsoleColor.DarkGreen);
                Console.WriteLine("  Summary: Created a refund-review request payload for human verification.");
                break;

            case SentToHumanEvent e:
                ConsoleUi.WriteColoredLine($"\n[Human Inbox|Deterministic] {e.Data}", ConsoleColor.DarkGray);
                Console.WriteLine("  Summary: Work item sent to a human queue for review.");
                break;

            case WorkflowOutputEvent outputEvent:
                ConsoleUi.WriteSectionTitle("Workflow Output");
                Console.WriteLine(outputEvent.Is<string>() ? outputEvent.As<string>() : outputEvent.Data);
                break;

            case WorkflowErrorEvent errorEvent:
                ConsoleUi.WriteColoredLine($"\n=== Workflow Error ===\n{errorEvent}", ConsoleColor.Red);
                break;
        }
    }

    Console.WriteLine("\n------------------------------------------------------------\n");
}

// Flush and dispose tracer to ensure all spans are exported
tracerProvider.Dispose();

//Workflow workflowForViz = buildWorkflow();
//ConsoleUi.WriteSectionTitle("Workflow (Mermaid)", ConsoleColor.DarkCyan);
//Console.WriteLine(workflowForViz.ToMermaidString());