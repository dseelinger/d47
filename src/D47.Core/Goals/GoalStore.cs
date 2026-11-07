using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Goals;

/// <summary>The arcs on disk — <c>data/goals.json</c> (Phase 34).</summary>
public sealed class GoalStore(string path, IFileSystem files, ILogger<GoalStore> logger)
{
    /// <summary>The key used where the journals never said who was flying.</summary>
    public const string NoCommander = "";

    private static readonly JsonSerializerOptions Json = StoreJson.OmittingNulls;

    private readonly Lock _gate = new();

    private Dictionary<string, GoalMine> _mines = new(StringComparer.Ordinal);
    private Dictionary<string, IReadOnlyList<string>> _setAside = new(StringComparer.Ordinal);
    private readonly WatchedTextFile _file = new(files, path, logger);

    public event Action? Changed;

    /// <summary>The last walk over this Commander's journals, or null where none has been done.</summary>
    public GoalMine? MineFor(string? frontierId)
    {
        lock (_gate)
        {
            return _mines.GetValueOrDefault(frontierId ?? NoCommander);
        }
    }

    /// <summary>Arc keys this Commander does not want to see.</summary>
    public IReadOnlyList<string> SetAsideBy(string? frontierId)
    {
        lock (_gate)
        {
            return _setAside.GetValueOrDefault(frontierId ?? NoCommander, []);
        }
    }

    public bool Poll() => _file.Poll(
        () =>
        {
            lock (_gate)
            {
                _mines = new Dictionary<string, GoalMine>(StringComparer.Ordinal);
                _setAside = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            }

            Changed?.Invoke();
        },
        text =>
        {
            Reload(text);
            Changed?.Invoke();
            return true;
        });

    /// <summary>
    /// Files one walk's conclusions, replacing whatever that Commander had before — because mining is a
    /// recomputation, and merging would carry forward a count the current journals no longer support.
    /// </summary>
    public void Record(IReadOnlyList<GoalMine> mines)
    {
        ArgumentNullException.ThrowIfNull(mines);

        Poll();

        lock (_gate)
        {
            var next = new Dictionary<string, GoalMine>(_mines, StringComparer.Ordinal);

            foreach (var mine in mines)
            {
                next[mine.FrontierId] = mine;
            }

            _mines = next;
        }

        Save();
        Changed?.Invoke();
    }

    /// <summary>Puts an arc out of sight, or brings it back.</summary>
    public bool SetAside(string? frontierId, string key, bool aside)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        Poll();

        var commander = frontierId ?? NoCommander;
        var wanted = key.Trim();

        lock (_gate)
        {
            var existing = _setAside.GetValueOrDefault(commander, []);
            var already = existing.Contains(wanted, StringComparer.OrdinalIgnoreCase);

            if (already == aside)
            {
                return false;
            }

            _setAside = new Dictionary<string, IReadOnlyList<string>>(_setAside, StringComparer.Ordinal)
            {
                [commander] = aside
                    ? [.. existing, wanted]
                    : [.. existing.Where(entry => !string.Equals(entry, wanted, StringComparison.OrdinalIgnoreCase))],
            };
        }

        Save();
        Changed?.Invoke();
        return true;
    }

    private void Save()
    {
        Document document;

        lock (_gate)
        {
            var commanders = _mines.Keys
                .Concat(_setAside.Keys)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(key => key, StringComparer.Ordinal);

            document = new Document
            {
                Commanders =
                [
                    .. commanders.Select(key =>
                    {
                        var mine = _mines.GetValueOrDefault(key);

                        return new CommanderRecord
                        {
                            FrontierId = key,
                            MinedAt = mine?.MinedAt,
                            Journals = mine?.Journals ?? 0,
                            From = mine?.From,
                            To = mine?.To,
                            Marks = mine is null
                                ? null
                                :
                                [
                                    .. mine.Marks.Select(mark => new MarkRecord
                                    {
                                        Key = mark.Key,
                                        Started = mark.Started,
                                        Have = mark.Have,
                                        AsOf = mark.AsOf,
                                    }),
                                ],
                            SetAside = _setAside.GetValueOrDefault(key, []) is { Count: > 0 } aside
                                ? [.. aside]
                                : null,
                        };
                    }),
                ],
            };
        }

        var text = JsonSerializer.Serialize(document, Json);

        try
        {
            files.WriteText(path, text);

            lock (_gate)
            {
                _file.Seen(text);
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or NotSupportedException)
        {
            logger.LogWarning(ex, "Could not write {Path}", path);
        }
    }

    private void Reload(string text)
    {
        var mines = new Dictionary<string, GoalMine>(StringComparer.Ordinal);
        var aside = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        var refused = 0;

        try
        {
            foreach (var commander in JsonSerializer.Deserialize<Document>(text, Json)?.Commanders ?? [])
            {
                var key = commander.FrontierId ?? NoCommander;

                if (commander.SetAside is { Count: > 0 } hidden)
                {
                    aside[key] =
                    [
                        .. hidden.Where(entry => !string.IsNullOrWhiteSpace(entry)).Select(entry => entry.Trim()),
                    ];
                }

                // A record with no MinedAt gets no mine rather than an empty one, so "have you ever looked"
                // stays answerable.
                if (commander.MinedAt is not { } minedAt)
                {
                    continue;
                }

                mines[key] = new GoalMine
                {
                    FrontierId = key,
                    MinedAt = minedAt,
                    Journals = commander.Journals,
                    From = commander.From,
                    To = commander.To,
                    Marks =
                    [
                        .. (commander.Marks ?? [])
                            .Where(record => !string.IsNullOrWhiteSpace(record.Key))
                            .Select(record => new GoalMark
                            {
                                Key = record.Key!.Trim(),
                                Started = record.Started,
                                Have = record.Have,
                                AsOf = record.AsOf,
                            }),
                    ],
                };
            }
        }
        catch (JsonException ex)
        {
            refused++;
            logger.LogWarning(ex, "Could not parse {Path}", path);
        }

        lock (_gate)
        {
            if (refused == 0 || mines.Count > 0)
            {
                _mines = mines;
                _setAside = aside;
            }

            _file.Seen(text);
        }

        logger.LogInformation(
            "Loaded goal progress for {Commanders} Commander(s) from {Path} ({Problems} refused)",
            _mines.Count,
            path,
            refused);
    }

    private sealed class Document
    {
        public IReadOnlyList<CommanderRecord> Commanders { get; set; } = [];
    }

    private sealed class CommanderRecord
    {
        public string? FrontierId { get; set; }

        public DateTimeOffset? MinedAt { get; set; }

        public int Journals { get; set; }

        public DateTimeOffset? From { get; set; }

        public DateTimeOffset? To { get; set; }

        public IReadOnlyList<MarkRecord>? Marks { get; set; }

        /// <summary>Arc keys the Commander removed.</summary>
        public IReadOnlyList<string>? SetAside { get; set; }
    }

    private sealed class MarkRecord
    {
        public string? Key { get; set; }

        public DateTimeOffset? Started { get; set; }

        public long? Have { get; set; }

        public DateTimeOffset? AsOf { get; set; }
    }
}
