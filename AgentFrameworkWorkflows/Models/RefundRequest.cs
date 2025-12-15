namespace AgentFrameworkWorkflows.Models;

public sealed class RefundRequest
{
    public required string RefundRequestId { get; init; }
    public string? OrderId { get; init; }
    public string? Customer { get; init; }
    public required string Reason { get; init; }
    public required string Sla { get; init; }

    /// <summary>
    /// Deterministic customer-facing acknowledgement email.
    /// </summary>
    public required string CustomerReply { get; init; }
}

