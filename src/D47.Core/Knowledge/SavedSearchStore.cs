using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Knowledge;

/// <summary>A galaxy search a Commander named: a search tool and its arguments, as given.</summary>
public sealed record SavedSearch(
    string Name, string Tool, IReadOnlyDictionary<string, string> Arguments, DateTimeOffset SavedAt);

/// <summary>Whether a search was saved; when not, <see cref="Message"/> says why in a sentence for the page.</summary>
public sealed record SavedSearchOutcome(bool Saved, string Message);

/// <summary>The galaxy searches named on this machine, shared by every Commander, kept at <c>data/saved-searches.json</c>.</summary>
public sealed class SavedSearchStore(string path, IFileSystem files, ILogger<SavedSearchStore> logger)
{
    public static IReadOnlyList<string> Tools { get; } = ["search_systems", "search_stations", "find_body"];

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly Lock _gate = new();

    private readonly Lock _writeGate = new();

    private Dictionary<string, SavedSearch> _searches = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Raised when a search is saved or forgotten.</summary>
    public event Action? Changed;

    /// <summary>Loads the file. A file of per-Commander records is merged, newest <c>SavedAt</c> per name, and rewritten.</summary>
    public void Load()
    {
        try
        {
            if (files.ReadText(path) is not { } text)
            {
                return;
            }

            var document = JsonSerializer.Deserialize<Document>(text, Json);
            var loaded = new Dictionary<string, SavedSearch>(StringComparer.OrdinalIgnoreCase);
            var records = (document?.Searches ?? []).Concat((document?.Commanders ?? []).SelectMany(commander => commander.Searches ?? []));

            foreach (var record in records)
            {
                if (record is { Name.Length: > 0 } && Tools.Contains(record.Tool))
                {
                    var search = new SavedSearch(
                        record.Name.Trim(),
                        record.Tool,
                        new Dictionary<string, string>(record.Arguments ?? [], StringComparer.Ordinal),
                        record.SavedAt);

                    if (!loaded.TryGetValue(search.Name, out var kept) || search.SavedAt > kept.SavedAt)
                    {
                        loaded[search.Name] = search;
                    }
                }
            }

            lock (_gate)
            {
                _searches = loaded;
            }

            logger.LogInformation("Loaded {Searches} saved search(es)", loaded.Count);

            if (document?.Commanders is { Count: > 0 })
            {
                Write(loaded);
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or NotSupportedException)
        {
            logger.LogWarning(ex, "Could not read {Path}; searches saved before now are lost", path);
        }
    }

    /// <summary>The saved searches, newest first.</summary>
    public IReadOnlyList<SavedSearch> All()
    {
        lock (_gate)
        {
            return [.. _searches.Values.OrderByDescending(search => search.SavedAt)];
        }
    }

    /// <summary>
    /// Saves a search. <paramref name="phrasesTaken"/> is every phrase already routed, including the saved searches.
    /// </summary>
    public SavedSearchOutcome Add(
        string name,
        string tool,
        IReadOnlyDictionary<string, string> arguments,
        DateTimeOffset at,
        IEnumerable<string> phrasesTaken)
    {
        var trimmed = name.Trim();

        if (!Tools.Contains(tool))
        {
            return Refused($"A saved search runs {string.Join(", ", Tools)}, not {tool}.");
        }

        if (trimmed.Length == 0)
        {
            return Refused("Give the search a name.");
        }

        if (QuickSearches.All.Any(quick => string.Equals(quick.Name, trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            return Refused($"\"{trimmed}\" is a quick search already. Choose another name.");
        }

        lock (_writeGate)
        {
            var merged = new Dictionary<string, SavedSearch>(_searches, StringComparer.OrdinalIgnoreCase);

            if (merged.ContainsKey(trimmed))
            {
                return Refused($"You already have a search named \"{trimmed}\".");
            }

            var taken = new HashSet<string>(phrasesTaken, StringComparer.OrdinalIgnoreCase);

            if (SavedSearchPhrases.Of(trimmed).FirstOrDefault(taken.Contains) is { } phrase)
            {
                return Refused($"\"{phrase}\" already means something else. Choose another name.");
            }

            merged[trimmed] = new SavedSearch(
                trimmed, tool, new Dictionary<string, string>(arguments, StringComparer.Ordinal), at);

            if (!Write(merged))
            {
                return Refused("The search could not be written to disk.");
            }
        }

        logger.LogInformation("Saved the {Tool} search \"{Name}\"", tool, trimmed);

        return new SavedSearchOutcome(true, $"Saved \"{trimmed}\".");
    }

    /// <summary>Forgets a search. False when there is no such search.</summary>
    public bool Forget(string name)
    {
        lock (_writeGate)
        {
            if (!_searches.ContainsKey(name.Trim()))
            {
                return false;
            }

            var merged = new Dictionary<string, SavedSearch>(_searches, StringComparer.OrdinalIgnoreCase);

            merged.Remove(name.Trim());

            if (!Write(merged))
            {
                return false;
            }
        }

        logger.LogInformation("Forgot the search \"{Name}\"", name);

        return true;
    }

    private static SavedSearchOutcome Refused(string message) => new(false, message);

    private bool Write(Dictionary<string, SavedSearch> merged)
    {
        var document = new Document
        {
            Searches =
            [
                .. merged.Values.Select(search => new SearchRecord
                {
                    Name = search.Name,
                    Tool = search.Tool,
                    Arguments = new Dictionary<string, string>(search.Arguments),
                    SavedAt = search.SavedAt,
                }),
            ],
        };

        try
        {
            files.WriteText(path, JsonSerializer.Serialize(document, Json));
        }
        catch (Exception ex) when (ex is IOException or JsonException or NotSupportedException)
        {
            logger.LogWarning(ex, "Could not write {Path}", path);
            return false;
        }

        lock (_gate)
        {
            _searches = merged;
        }

        Changed?.Invoke();

        return true;
    }

    private sealed class Document
    {
        public IReadOnlyList<SearchRecord>? Searches { get; set; }

        public IReadOnlyList<CommanderRecord>? Commanders { get; set; }
    }

    private sealed class CommanderRecord
    {
        public IReadOnlyList<SearchRecord>? Searches { get; set; }
    }

    private sealed class SearchRecord
    {
        public string Name { get; set; } = string.Empty;

        public string Tool { get; set; } = string.Empty;

        public Dictionary<string, string>? Arguments { get; set; }

        public DateTimeOffset SavedAt { get; set; }
    }
}

/// <summary>The phrases that run a saved search.</summary>
public static class SavedSearchPhrases
{
    public static IEnumerable<string> Of(string name) => [$"{name} search", $"run the {name} search"];

    /// <summary>Two commands per saved search.</summary>
    public static IEnumerable<DynamicCommand> Commands(SavedSearchStore? store)
    {
        if (store is null)
        {
            return [];
        }

        return store.All().SelectMany(search => Of(search.Name).Select(phrase =>
            new DynamicCommand(phrase, GalaxyCapability.Id, search.Tool, search.Arguments)));
    }
}
