using System.Text.Json;
using Microsoft.Agents.AI;

namespace AgentFrameworkFoundryAgent;

/// <summary>
/// Persists an <see cref="AgentThread"/> to a JSON file and restores it via a supplied deserializer.
/// </summary>
internal sealed class FileThreadStore
{
    private readonly string _threadStatePath;
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    public FileThreadStore(
        string storageDirectory,
        string threadStateFileName = "thread-state.json",
        JsonSerializerOptions? jsonSerializerOptions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(threadStateFileName);

        Directory.CreateDirectory(storageDirectory);
        _threadStatePath = Path.Combine(storageDirectory, threadStateFileName);
        _jsonSerializerOptions = jsonSerializerOptions ?? new JsonSerializerOptions { WriteIndented = true };
    }

    public bool Exists => File.Exists(_threadStatePath);

    public string ThreadStatePath => _threadStatePath;

    public string StorageDirectory => Path.GetDirectoryName(_threadStatePath)!;

    public AgentThread Load(Func<JsonElement, AgentThread> deserializeThread)
    {
        ArgumentNullException.ThrowIfNull(deserializeThread);

        var threadStateJson = File.ReadAllText(_threadStatePath);
        var serializedThread = JsonSerializer.Deserialize<JsonElement>(threadStateJson);
        return deserializeThread(serializedThread);
    }

    public void Save(AgentThread thread)
    {
        ArgumentNullException.ThrowIfNull(thread);

        var serializedThread = thread.Serialize();
        var threadStateJson = JsonSerializer.Serialize(serializedThread, _jsonSerializerOptions);
        File.WriteAllText(_threadStatePath, threadStateJson);
    }

    public void Delete()
    {
        var directory = StorageDirectory;
        if (Directory.Exists(directory))
        {
            foreach (var file in Directory.GetFiles(directory))
            {
                File.Delete(file);
            }
        }
    }
}
