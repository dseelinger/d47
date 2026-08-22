using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Adventures;

/// <summary>Why a record of the file was refused, naming the record and the problem.</summary>
public sealed record AdventureProblem(string Where, string Reason);

/// <summary>
/// The adventures on disk — <c>data/adventures.json</c> (list.md Phase 47).
/// <para>
/// <see cref="Goals.GoalStore"/>'s shape with a different payload, for the reason that file gives:
/// written through <see cref="AtomicFile"/>, polled by comparing <b>content</b>, keyed per Commander
/// with the key inside the document, hand-editable, and a record that cannot be read back reported
/// rather than dropped. The one thing this file holds that no other store does is a
/// <see cref="Adventure.IsDraft"/>: a generated adventure waiting for the Commander's yes lives here
/// beside the ones under way, told apart by its source and the absence of a stamp, so the boundary
/// is inspectable by reading <c>data/</c>. Nothing model-callable writes this file.
/// </para>
/// </summary>
public sealed class AdventureStore(string path, ILogger<AdventureStore> logger)
{
    /// <summary>The key used where the journals never said who was flying.</summary>
    public const string NoCommander = "";

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly Lock _gate = new();

    private Dictionary<string, IReadOnlyList<Adventure>> _byCommander = new(StringComparer.Ordinal);
    private IReadOnlyList<AdventureProblem> _problems = [];
    private string? _seen;

    public string Path => path;

    public event Action? Changed;

    public IReadOnlyList<AdventureProblem> Problems
    {
        get
        {
            lock (_gate)
            {
                return _problems;
            }
        }
    }

    /// <summary>This Commander's adventures, in the order they were written.</summary>
    public IReadOnlyList<Adventure> For(string? frontierId)
    {
        lock (_gate)
        {
            return _byCommander.GetValueOrDefault(frontierId ?? NoCommander, []);
        }
    }

    public Adventure? Find(string? frontierId, string key) =>
        For(frontierId).FirstOrDefault(adventure => string.Equals(adventure.Key, key, StringComparison.OrdinalIgnoreCase));

    /// <summary>Every Commander with anything on file. The catch-up walks them all at once.</summary>
    public IReadOnlyList<string> Commanders
    {
        get
        {
            lock (_gate)
            {
                return [.. _byCommander.Keys];
            }
        }
    }

    public bool Poll()
    {
        string text;

        try
        {
            if (!File.Exists(path))
            {
                if (_seen is null)
                {
                    return false;
                }

                lock (_gate)
                {
                    _byCommander = new Dictionary<string, IReadOnlyList<Adventure>>(StringComparer.Ordinal);
                    _problems = [];
                    _seen = null;
                }

                Changed?.Invoke();
                return true;
            }

            using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

            using var reader = new StreamReader(stream);
            text = reader.ReadToEnd();
        }
        catch (IOException ex)
        {
            logger.LogDebug(ex, "Could not read {Path}", path);
            return false;
        }

        if (string.Equals(text, _seen, StringComparison.Ordinal))
        {
            return false;
        }

        Reload(text);
        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// Writes one adventure, adding it or replacing the one with its key. Returns null when it was
    /// stored, or why it was refused — the same sentences the file loader would have used.
    /// </summary>
    public string? Save(string? frontierId, Adventure adventure)
    {
        ArgumentNullException.ThrowIfNull(adventure);

        if (AdventureValidation.Problems(adventure) is { Count: > 0 } problems)
        {
            return string.Join(" ", problems);
        }

        Poll();

        var commander = frontierId ?? NoCommander;

        lock (_gate)
        {
            var existing = _byCommander.GetValueOrDefault(commander, []);
            var replacing = existing.Any(other => string.Equals(other.Key, adventure.Key, StringComparison.OrdinalIgnoreCase));

            if (!replacing && existing.Count >= AdventureLimits.MaxAdventures)
            {
                return $"You already have {AdventureLimits.MaxAdventures} adventures, which is the most I hold.";
            }

            _byCommander = new Dictionary<string, IReadOnlyList<Adventure>>(_byCommander, StringComparer.Ordinal)
            {
                [commander] = replacing
                    ?
                    [
                        .. existing.Select(other =>
                            string.Equals(other.Key, adventure.Key, StringComparison.OrdinalIgnoreCase) ? adventure : other),
                    ]
                    : [.. existing, adventure],
            };
        }

        Write();
        Changed?.Invoke();
        return null;
    }

    /// <summary>Deletes one outright. Returns whether there was one to delete.</summary>
    public bool Remove(string? frontierId, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        Poll();

        var commander = frontierId ?? NoCommander;

        lock (_gate)
        {
            var existing = _byCommander.GetValueOrDefault(commander, []);
            var kept = existing
                .Where(adventure => !string.Equals(adventure.Key, key.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (kept.Count == existing.Count)
            {
                return false;
            }

            _byCommander = new Dictionary<string, IReadOnlyList<Adventure>>(_byCommander, StringComparer.Ordinal)
            {
                [commander] = kept,
            };
        }

        Write();
        Changed?.Invoke();
        return true;
    }

    private void Write()
    {
        Document document;

        lock (_gate)
        {
            document = new Document
            {
                Commanders =
                [
                    .. _byCommander
                        .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                        .Where(pair => pair.Value.Count > 0)
                        .Select(pair => new CommanderRecord
                        {
                            FrontierId = pair.Key,
                            Adventures = [.. pair.Value.Select(ToRecord)],
                        }),
                ],
            };
        }

        var text = JsonSerializer.Serialize(document, Json);

        try
        {
            AtomicFile.WriteAllText(path, text);

            lock (_gate)
            {
                _seen = text;
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or NotSupportedException)
        {
            logger.LogWarning(ex, "Could not write {Path}", path);
        }
    }

    private void Reload(string text)
    {
        var loaded = new Dictionary<string, IReadOnlyList<Adventure>>(StringComparer.Ordinal);
        var problems = new List<AdventureProblem>();

        try
        {
            foreach (var commander in JsonSerializer.Deserialize<Document>(text, Json)?.Commanders ?? [])
            {
                var key = commander.FrontierId ?? NoCommander;
                var kept = new List<Adventure>();

                foreach (var record in commander.Adventures ?? [])
                {
                    var adventure = FromRecord(record, problems);

                    if (adventure is null)
                    {
                        continue;
                    }

                    if (kept.Any(other => string.Equals(other.Key, adventure.Key, StringComparison.OrdinalIgnoreCase)))
                    {
                        problems.Add(new AdventureProblem(adventure.Key, "a second adventure with the same key"));
                        continue;
                    }

                    kept.Add(adventure);
                }

                loaded[key] = kept;
            }
        }
        catch (JsonException ex)
        {
            problems.Add(new AdventureProblem(System.IO.Path.GetFileName(path), ex.Message));
            logger.LogWarning(ex, "Could not parse {Path}", path);
        }

        lock (_gate)
        {
            if (problems.Count == 0 || loaded.Count > 0)
            {
                _byCommander = loaded;
            }

            _problems = problems;
            _seen = text;
        }

        logger.LogInformation(
            "Loaded adventures for {Commanders} Commander(s) from {Path} ({Problems} refused)",
            loaded.Count,
            path,
            problems.Count);
    }

    private static Adventure? FromRecord(AdventureRecord record, List<AdventureProblem> problems)
    {
        var where = record.Name ?? record.Key ?? "(an adventure)";

        if (string.IsNullOrWhiteSpace(record.Key) || string.IsNullOrWhiteSpace(record.Name))
        {
            problems.Add(new AdventureProblem(where, "no key or no name"));
            return null;
        }

        var beats = new List<AdventureBeat>();

        foreach (var (beat, index) in (record.Beats ?? []).Select((beat, index) => (beat, index)))
        {
            if (!AdventureValidation.TryKind(beat.Trigger?.Kind, out var kind))
            {
                problems.Add(new AdventureProblem(
                    where,
                    $"beat {index + 1} names a trigger \"{beat.Trigger?.Kind ?? string.Empty}\"; the five are "
                    + string.Join(", ", AdventureValidation.Kinds)));
                return null;
            }

            beats.Add(new AdventureBeat
            {
                Title = beat.Title?.Trim() ?? string.Empty,
                Function = beat.Function?.Trim(),
                Line = beat.Line?.Trim() ?? string.Empty,
                Trigger = new AdventureTrigger
                {
                    Kind = kind,
                    SystemAddress = beat.Trigger!.SystemAddress,
                    MarketId = beat.Trigger.MarketId,
                    BodyId = beat.Trigger.BodyId,
                    Career = Careers.Match(beat.Trigger.Career) ?? beat.Trigger.Career?.Trim(),
                    Rank = beat.Trigger.Rank,
                    System = beat.Trigger.System?.Trim(),
                    Station = beat.Trigger.Station?.Trim(),
                    Body = beat.Trigger.Body?.Trim(),
                },
            });
        }

        var adventure = new Adventure
        {
            Key = record.Key.Trim(),
            Name = record.Name.Trim(),
            Source = Enum.TryParse<AdventureSource>(record.Source, ignoreCase: true, out var source) && Enum.IsDefined(source)
                ? source
                : AdventureSource.Commander,
            Written = record.Written,
            WrittenBy = record.WrittenBy?.Trim(),
            Spine = record.Spine is { } spine
                ? new AdventureSpine
                {
                    Premise = spine.Premise?.Trim(),
                    Want = spine.Want?.Trim(),
                    Stake = spine.Stake?.Trim(),
                    Turn = spine.Turn?.Trim(),
                    Ending = spine.Ending?.Trim(),
                }
                : null,
            Opening = record.Opening?.Trim(),
            Beats = beats,
            AcceptedAt = record.AcceptedAt,
            AbandonedAt = record.AbandonedAt,
            Previous = record.Previous is { } previous ? FromRecord(previous, problems) : null,
        };

        var refusals = AdventureValidation.Problems(adventure);

        if (refusals.Count > 0)
        {
            problems.Add(new AdventureProblem(where, string.Join(" ", refusals)));
            return null;
        }

        return adventure;
    }

    private static AdventureRecord ToRecord(Adventure adventure) => new()
    {
        Key = adventure.Key,
        Name = adventure.Name,
        Source = adventure.Source.ToString().ToLowerInvariant(),
        Written = adventure.Written,
        WrittenBy = adventure.WrittenBy,
        Spine = adventure.Spine is { IsEmpty: false } spine
            ? new SpineRecord
            {
                Premise = spine.Premise,
                Want = spine.Want,
                Stake = spine.Stake,
                Turn = spine.Turn,
                Ending = spine.Ending,
            }
            : null,
        Opening = adventure.Opening,
        Beats =
        [
            .. adventure.Beats.Select(beat => new BeatRecord
            {
                Title = beat.Title,
                Function = beat.Function,
                Trigger = new TriggerRecord
                {
                    Kind = beat.Trigger.Kind.ToString().ToLowerInvariant(),
                    SystemAddress = beat.Trigger.SystemAddress,
                    MarketId = beat.Trigger.MarketId,
                    BodyId = beat.Trigger.BodyId,
                    Career = beat.Trigger.Career,
                    Rank = beat.Trigger.Rank,
                    System = beat.Trigger.System,
                    Station = beat.Trigger.Station,
                    Body = beat.Trigger.Body,
                },
                Line = beat.Line,
            }),
        ],
        AcceptedAt = adventure.AcceptedAt,
        AbandonedAt = adventure.AbandonedAt,
        Previous = adventure.Previous is { } previous ? ToRecord(previous) : null,
    };

    private sealed class Document
    {
        public IReadOnlyList<CommanderRecord> Commanders { get; set; } = [];
    }

    private sealed class CommanderRecord
    {
        public string? FrontierId { get; set; }

        public IReadOnlyList<AdventureRecord>? Adventures { get; set; }
    }

    private sealed class AdventureRecord
    {
        public string? Key { get; set; }

        public string? Name { get; set; }

        public string? Source { get; set; }

        public DateTimeOffset? Written { get; set; }

        public string? WrittenBy { get; set; }

        public SpineRecord? Spine { get; set; }

        public string? Opening { get; set; }

        public IReadOnlyList<BeatRecord>? Beats { get; set; }

        public DateTimeOffset? AcceptedAt { get; set; }

        public DateTimeOffset? AbandonedAt { get; set; }

        public AdventureRecord? Previous { get; set; }
    }

    private sealed class SpineRecord
    {
        public string? Premise { get; set; }

        public string? Want { get; set; }

        public string? Stake { get; set; }

        public string? Turn { get; set; }

        public string? Ending { get; set; }
    }

    private sealed class BeatRecord
    {
        public string? Title { get; set; }

        public string? Function { get; set; }

        public TriggerRecord? Trigger { get; set; }

        public string? Line { get; set; }
    }

    private sealed class TriggerRecord
    {
        public string? Kind { get; set; }

        public long? SystemAddress { get; set; }

        public long? MarketId { get; set; }

        public int? BodyId { get; set; }

        public string? Career { get; set; }

        public int? Rank { get; set; }

        public string? System { get; set; }

        public string? Station { get; set; }

        public string? Body { get; set; }
    }
}
