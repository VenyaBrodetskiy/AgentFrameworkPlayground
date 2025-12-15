namespace AgentFrameworkWorkflows.Models;

/// <summary>
/// A small message used to funnel all branches into a single final summarizer.
/// </summary>
internal sealed class FinalSignal
{
    public required string TerminalExecutorId { get; init; }
    public string? Note { get; init; }
}
