using System.Text;
using System.Text.Json;
using Microsoft.Agents.AI;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace AgentFrameworkThreadPersistancy;

/// <summary>
/// A file-based implementation of ChatMessageStore that persists chat messages to disk.
/// </summary>
internal sealed class FileChatMessageStore : ChatMessageStore
{
    private const string ThreadIdPrefix = "thread";
    private readonly string _threadId;
    private readonly string _messagesFilePath;

    public FileChatMessageStore(JsonElement serializedStoreState)
    {
        _threadId = ReadOrCreateThreadId(serializedStoreState);

        // Hardcoded storage location in bin folder
        var storageDirectory = Path.Combine(Environment.CurrentDirectory, "ThreadStorage");
        Directory.CreateDirectory(storageDirectory);
        
        _messagesFilePath = Path.Combine(storageDirectory, $"{_threadId}.json");
    }

    public override async Task AddMessagesAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        // Load existing messages
        var allMessages = (await GetMessagesAsync(cancellationToken)).ToList();

        // Add new messages
        allMessages.AddRange(messages);

        // Save all messages to file
        var json = JsonSerializer.Serialize(allMessages, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_messagesFilePath, json, cancellationToken);
    }

    public override async Task<IEnumerable<ChatMessage>> GetMessagesAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_messagesFilePath))
        {
            return [];
        }

        var json = await File.ReadAllTextAsync(_messagesFilePath, cancellationToken);
        return JsonSerializer.Deserialize<List<ChatMessage>>(json) ?? [];
    }

    public override JsonElement Serialize(JsonSerializerOptions? jsonSerializerOptions = null) =>
        JsonSerializer.SerializeToElement(_threadId);

    private static string ReadOrCreateThreadId(JsonElement serializedStoreState)
    {
        var fromState = serializedStoreState.ValueKind == JsonValueKind.String
            ? serializedStoreState.GetString()
            : null;

        if (!string.IsNullOrWhiteSpace(fromState))
        {
            return SanitizeFileName(fromState);
        }

        // New thread: generate a stable id that will be persisted via Serialize() into thread state.
        return $"{ThreadIdPrefix}-{Guid.NewGuid():N}";
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
