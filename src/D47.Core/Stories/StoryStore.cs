using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Adventures;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Stories;

/// <summary>The stories each Commander picked — <c>data/story.json</c>. At most one per Commander is current.</summary>
public sealed class StoryStore
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly string? _path;
    private readonly ILogger? _logger;
    private readonly Lock _gate = new();
    private Dictionary<string, IReadOnlyList<Story>> _byCommander;

    private StoryStore(string? path, ILogger? logger, Dictionary<string, IReadOnlyList<Story>> byCommander)
    {
        _path = path;
        _logger = logger;
        _byCommander = byCommander;
    }

    /// <summary>Held in memory only.</summary>
    public static StoryStore InMemory() => new(null, null, new(StringComparer.Ordinal));

    public static StoryStore Open(string path, ILogger<StoryStore> logger) => new(path, logger, Read(path, logger));

    public event Action? Changed;

    /// <summary>This Commander's stories, in the order they were first picked.</summary>
    public IReadOnlyList<Story> For(string? frontierId)
    {
        lock (_gate)
        {
            return _byCommander.GetValueOrDefault(frontierId ?? AdventureStore.NoCommander, []);
        }
    }

    /// <summary>The running or paused story, or null.</summary>
    public Story? Current(string? frontierId) => For(frontierId).FirstOrDefault(story => story.IsCurrent);

    public Story? Find(string? frontierId, string id) =>
        For(frontierId).FirstOrDefault(story => string.Equals(story.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>The running or paused stories of every Commander.</summary>
    public IReadOnlyList<Story> AllCurrent()
    {
        lock (_gate)
        {
            return [.. _byCommander.Values.SelectMany(stories => stories).Where(story => story.IsCurrent)];
        }
    }

    /// <summary>Adds the story or replaces the one with its id.</summary>
    public void Save(string? frontierId, Story story)
    {
        ArgumentNullException.ThrowIfNull(story);

        var commander = frontierId ?? AdventureStore.NoCommander;

        lock (_gate)
        {
            var existing = _byCommander.GetValueOrDefault(commander, []);
            var replacing = existing.Any(other => string.Equals(other.Id, story.Id, StringComparison.OrdinalIgnoreCase));

            _byCommander = new Dictionary<string, IReadOnlyList<Story>>(_byCommander, StringComparer.Ordinal)
            {
                [commander] = replacing
                    ? [.. existing.Select(other => string.Equals(other.Id, story.Id, StringComparison.OrdinalIgnoreCase) ? story : other)]
                    : [.. existing, story],
            };

            Write();
        }

        Changed?.Invoke();
    }

    /// <summary>Replaces the story with <paramref name="id"/> by what <paramref name="change"/> makes of it, under the store's lock.</summary>
    public void Update(string? frontierId, string id, Func<Story, Story> change)
    {
        ArgumentNullException.ThrowIfNull(change);

        var commander = frontierId ?? AdventureStore.NoCommander;

        lock (_gate)
        {
            var existing = _byCommander.GetValueOrDefault(commander, []);

            if (existing.FirstOrDefault(story => string.Equals(story.Id, id, StringComparison.OrdinalIgnoreCase)) is not { } before)
            {
                return;
            }

            var after = change(before);

            if (after == before)
            {
                return;
            }

            _byCommander = new Dictionary<string, IReadOnlyList<Story>>(_byCommander, StringComparer.Ordinal)
            {
                [commander] = [.. existing.Select(story => ReferenceEquals(story, before) ? after : story)],
            };

            Write();
        }

        Changed?.Invoke();
    }

    private void Write()
    {
        if (_path is null)
        {
            return;
        }

        var document = new Document
        {
            Commanders =
            [
                .. _byCommander
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Where(pair => pair.Value.Count > 0)
                    .Select(pair => new CommanderRecord { FrontierId = pair.Key, Stories = pair.Value }),
            ],
        };

        try
        {
            AtomicFile.WriteAllText(_path, JsonSerializer.Serialize(document, Json));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger?.LogError(ex, "Could not write {Path}", _path);
        }
    }

    private static Dictionary<string, IReadOnlyList<Story>> Read(string path, ILogger logger)
    {
        var loaded = new Dictionary<string, IReadOnlyList<Story>>(StringComparer.Ordinal);

        if (!File.Exists(path))
        {
            return loaded;
        }

        try
        {
            foreach (var commander in JsonSerializer.Deserialize<Document>(File.ReadAllText(path), Json)?.Commanders ?? [])
            {
                loaded[commander.FrontierId ?? AdventureStore.NoCommander] = commander.Stories ?? [];
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            logger.LogError(ex, "Could not read {Path}; no story is running", path);
        }

        return loaded;
    }

    private sealed class Document
    {
        public IReadOnlyList<CommanderRecord> Commanders { get; set; } = [];
    }

    private sealed class CommanderRecord
    {
        public string? FrontierId { get; set; }

        public IReadOnlyList<Story>? Stories { get; set; }
    }
}
