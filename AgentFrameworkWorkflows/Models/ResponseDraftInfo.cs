namespace AgentFrameworkWorkflows.Models;

public sealed class ResponseDraftInfo
{
    public required ResponseMode Mode { get; init; }
    public required int ClarifyingQuestionsCount { get; init; }
    public required int CustomerReplyCharacters { get; init; }
    public required int InternalNotesCharacters { get; init; }
}

