namespace AgentFrameworkWorkflows.Models;

public sealed class EmailDocument
{
    public required string OriginalText { get; init; }
    public required string CleanText { get; init; }

    /// <summary>
    /// A best-effort PII-masked version of <see cref="CleanText"/> that is safer to send to an LLM.
    /// </summary>
    public required string ModelSafeText { get; init; }

    public string? From { get; init; }
    public string? Subject { get; init; }

    public bool ContainsPii { get; init; }

    public List<string> DetectedEmails { get; init; } = [];
    public List<string> DetectedPhones { get; init; } = [];
    public List<string> DetectedOrderIds { get; init; } = [];
}

