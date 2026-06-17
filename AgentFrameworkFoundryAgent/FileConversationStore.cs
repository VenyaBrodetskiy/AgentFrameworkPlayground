using System.Text.Json;

namespace AgentFrameworkFoundryAgent;

/// <summary>
/// Persists the Foundry conversation ID that lets the console app resume cloud-hosted history.
/// </summary>
internal sealed class FileConversationStore
{
    private static readonly JsonSerializerOptions s_jsonSerializerOptions = new() { WriteIndented = true };

    private readonly string _statePath;

    public FileConversationStore(
        string storageDirectory,
        string stateFileName = "thread-state.json")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(stateFileName);

        Directory.CreateDirectory(storageDirectory);
        _statePath = Path.Combine(storageDirectory, stateFileName);
    }

    public bool Exists => File.Exists(_statePath);

    public bool TryLoadConversationId(out string conversationId)
    {
        conversationId = string.Empty;

        if (!Exists)
            return false;

        using var threadState = JsonDocument.Parse(File.ReadAllText(_statePath));
        if (threadState.RootElement.TryGetProperty("conversationId", out var conversationIdElement) &&
            conversationIdElement.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(conversationIdElement.GetString()))
        {
            conversationId = conversationIdElement.GetString()!;
            return true;
        }

        return false;
    }

    public void Save(string conversationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);

        var conversationStateJson = JsonSerializer.Serialize(new { conversationId }, s_jsonSerializerOptions);
        File.WriteAllText(_statePath, conversationStateJson);
    }

    public void Delete()
    {
        if (File.Exists(_statePath))
        {
            File.Delete(_statePath);
        }
    }
}
