using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Knowledge;

/// <summary>One answered search, kept so it opens as it was answered.</summary>
/// <param name="Tool"><c>describe_system</c>, <c>search_systems</c>, <c>search_stations</c>, <c>search_outfitting</c> or <c>find_body</c>.</param>
/// <param name="SystemAddress">Set for <c>describe_system</c> only.</param>
/// <param name="Result">
/// A <see cref="GalaxySearchResult"/>, <see cref="StationSearchResult"/> or <see cref="BodySearchResult"/> by
/// <paramref name="Tool"/>; null for <c>describe_system</c>.
/// </param>
public sealed record SearchHistoryEntry(
    string Id,
    string Tool,
    IReadOnlyDictionary<string, string> Arguments,
    string? Reference,
    long? SystemAddress,
    object? Result,
    DateTimeOffset AskedAt)
{
    public static SearchHistoryEntry Of(
        string tool,
        IReadOnlyDictionary<string, string> arguments,
        string? reference,
        long? systemAddress,
        object? result,
        DateTimeOffset askedAt) =>
        new(Guid.NewGuid().ToString("N"), tool, arguments, reference, systemAddress, result, askedAt);
}

/// <summary>The last <see cref="Capacity"/> searches on this machine, shared by every Commander, kept at <c>data/search-history.json</c>.</summary>
public sealed class SearchHistory(string path, IFileSystem files, ILogger<SearchHistory> logger)
{
    public const int Capacity = 25;

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly Lock _gate = new();

    private List<SearchHistoryEntry> _entries = [];

    /// <summary>Raised when an entry is added.</summary>
    public event Action? Changed;

    public void Load()
    {
        try
        {
            if (files.ReadText(path) is not { } text)
            {
                return;
            }

            var document = JsonSerializer.Deserialize<Document>(text, Json);
            var dropped = 0;
            var read = new List<SearchHistoryEntry>();
            var records = (document?.Entries ?? []).Concat((document?.Commanders ?? []).SelectMany(commander => commander.Entries ?? []));

            foreach (var record in records)
            {
                if (Read(record) is { } entry)
                {
                    read.Add(entry);
                }
                else
                {
                    dropped++;
                }
            }

            var loaded = Newest(read);

            lock (_gate)
            {
                _entries = loaded;
            }

            if (document?.Commanders is { Count: > 0 })
            {
                Write(loaded);
            }

            if (dropped > 0)
            {
                logger.LogWarning("Dropped {Count} unreadable search history entries from {Path}", dropped, path);
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or NotSupportedException)
        {
            logger.LogWarning(ex, "Could not read {Path}; the search history is empty", path);
        }
    }

    /// <summary>The searches, newest first.</summary>
    public IReadOnlyList<SearchHistoryEntry> All()
    {
        lock (_gate)
        {
            return [.. _entries];
        }
    }

    /// <summary>
    /// Keeps <paramref name="entry"/> first, dropping an earlier entry with the same tool and arguments and the
    /// oldest beyond <see cref="Capacity"/>.
    /// </summary>
    public void Add(SearchHistoryEntry entry)
    {
        lock (_gate)
        {
            var entries = new List<SearchHistoryEntry>(_entries);

            entries.RemoveAll(kept => Same(kept, entry));
            entries.Insert(0, entry);

            if (entries.Count > Capacity)
            {
                entries.RemoveRange(Capacity, entries.Count - Capacity);
            }

            if (!Write(entries))
            {
                return;
            }

            _entries = entries;
        }

        Changed?.Invoke();
    }

    private static List<SearchHistoryEntry> Newest(IEnumerable<SearchHistoryEntry> entries) =>
        [.. entries.DistinctBy(entry => entry.Id).OrderByDescending(entry => entry.AskedAt).Take(Capacity)];

    private static bool Same(SearchHistoryEntry a, SearchHistoryEntry b) =>
        a.Tool == b.Tool
        && a.SystemAddress == b.SystemAddress
        && a.Arguments.Count == b.Arguments.Count
        && a.Arguments.All(pair => b.Arguments.TryGetValue(pair.Key, out var value) && value == pair.Value);

    private static SearchHistoryEntry? Read(EntryRecord record)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(record.Id) || record.Arguments is null)
            {
                return null;
            }

            object? result = record.Result is { ValueKind: not (JsonValueKind.Null or JsonValueKind.Undefined) } element
                ? record.Tool switch
                {
                    "search_systems" => element.Deserialize<GalaxySearchResult>(Json),
                    "search_stations" or "search_outfitting" => element.Deserialize<StationSearchResult>(Json),
                    "find_body" => element.Deserialize<BodySearchResult>(Json),
                    _ => null,
                }
                : null;

            if (record.Tool == "describe_system" ? record.SystemAddress is null : result is null)
            {
                return null;
            }

            return new SearchHistoryEntry(
                record.Id, record.Tool, record.Arguments, record.Reference, record.SystemAddress, result, record.AskedAt);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private bool Write(List<SearchHistoryEntry> entries)
    {
        try
        {
            var document = new Document
            {
                Entries =
                [
                    .. entries.Select(entry => new EntryRecord
                    {
                        Id = entry.Id,
                        Tool = entry.Tool,
                        Arguments = new Dictionary<string, string>(entry.Arguments),
                        Reference = entry.Reference,
                        SystemAddress = entry.SystemAddress,
                        Result = ToElement(entry),
                        AskedAt = entry.AskedAt,
                    }),
                ],
            };

            files.WriteText(path, JsonSerializer.Serialize(document, Json));

            return true;
        }
        catch (Exception ex) when (ex is IOException or JsonException or NotSupportedException or InvalidCastException)
        {
            logger.LogWarning(ex, "Could not write {Path}", path);

            return false;
        }
    }

    private static JsonElement? ToElement(SearchHistoryEntry entry) => entry.Tool switch
    {
        "search_systems" => JsonSerializer.SerializeToElement((GalaxySearchResult)entry.Result!, Json),
        "search_stations" or "search_outfitting" => JsonSerializer.SerializeToElement((StationSearchResult)entry.Result!, Json),
        "find_body" => JsonSerializer.SerializeToElement((BodySearchResult)entry.Result!, Json),
        _ => null,
    };

    private sealed class Document
    {
        public IReadOnlyList<EntryRecord>? Entries { get; set; }

        public IReadOnlyList<CommanderRecord>? Commanders { get; set; }
    }

    private sealed class CommanderRecord
    {
        public IReadOnlyList<EntryRecord>? Entries { get; set; }
    }

    private sealed class EntryRecord
    {
        public string Id { get; set; } = string.Empty;

        public string Tool { get; set; } = string.Empty;

        public Dictionary<string, string>? Arguments { get; set; }

        public string? Reference { get; set; }

        public long? SystemAddress { get; set; }

        public JsonElement? Result { get; set; }

        public DateTimeOffset AskedAt { get; set; }
    }
}
