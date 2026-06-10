using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Agents.AI;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace AgentFrameworkThreadPersistancy;

/// <summary>
/// A file-based chat history provider that persists chat messages to disk.
/// </summary>
internal sealed class FileChatMessageStore : ChatHistoryProvider
{
    private const string ThreadIdPrefix = "thread";
    private const string StateKey = "file-chat-message-store-thread-id";
    private static readonly JsonSerializerOptions StateJsonSerializerOptions = new()
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    public FileChatMessageStore()
    {
        var storageDirectory = Path.Combine(Environment.CurrentDirectory, "ThreadStorage");
        Directory.CreateDirectory(storageDirectory);
    }

    public override IReadOnlyList<string> StateKeys => [StateKey];

    public async Task<IEnumerable<ChatMessage>> GetMessagesAsync(AgentSession session, CancellationToken cancellationToken = default)
    {
        var messagesFilePath = GetMessagesFilePath(session);
        if (!File.Exists(messagesFilePath))
        {
            return [];
        }

        var json = await File.ReadAllTextAsync(messagesFilePath, cancellationToken);
        return JsonSerializer.Deserialize<List<ChatMessage>>(json) ?? [];
    }

    protected override async ValueTask<IEnumerable<ChatMessage>> ProvideChatHistoryAsync(InvokingContext context, CancellationToken cancellationToken)
    {
        var session = context.Session ?? throw new InvalidOperationException("Agent session was not provided.");
        return await GetMessagesAsync(session, cancellationToken);
    }

    protected override async ValueTask StoreChatHistoryAsync(InvokedContext context, CancellationToken cancellationToken)
    {
        var session = context.Session ?? throw new InvalidOperationException("Agent session was not provided.");
        var messages = (await GetMessagesAsync(session, cancellationToken)).ToList();
        messages.AddRange(context.RequestMessages ?? []);
        messages.AddRange(context.ResponseMessages ?? []);

        await SaveMessagesAsync(session, messages, cancellationToken);
    }

    private async Task SaveMessagesAsync(AgentSession session, IEnumerable<ChatMessage> messages, CancellationToken cancellationToken)
    {
        var messagesFilePath = GetMessagesFilePath(session);
        var json = JsonSerializer.Serialize(messages, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(messagesFilePath, json, cancellationToken);
    }

    private static string GetMessagesFilePath(AgentSession session)
    {
        var threadId = GetOrCreateThreadId(session);

        // Hardcoded storage location in bin folder
        var storageDirectory = Path.Combine(Environment.CurrentDirectory, "ThreadStorage");
        Directory.CreateDirectory(storageDirectory);

        return Path.Combine(storageDirectory, $"{threadId}.json");
    }

    private static string GetOrCreateThreadId(AgentSession session)
    {
        if (session.StateBag.TryGetValue<string>(StateKey, out var threadId, StateJsonSerializerOptions) &&
            !string.IsNullOrWhiteSpace(threadId))
        {
            return SanitizeFileName(threadId);
        }

        threadId = $"{ThreadIdPrefix}-{Guid.NewGuid():N}";
        session.StateBag.SetValue(StateKey, threadId, StateJsonSerializerOptions);
        return threadId;
    }

    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(value.Length);

        foreach (var ch in value)
        {
            if (invalidChars.Contains(ch))
            {
                continue;
            }

            sb.Append(ch);
        }

        var sanitized = sb.ToString().Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? $"{ThreadIdPrefix}-{Guid.NewGuid():N}" : sanitized;
    }
}
