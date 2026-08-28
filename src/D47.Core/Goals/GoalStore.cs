using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Goals;

/// <summary>Why a line of the file was refused, naming the line and the problem.</summary>
public sealed record GoalProblem(string Where, string Reason);

/// <summary>
/// The arcs on disk — <c>data/goals.json</c> (Phase 34).
/// <para>
/// A mined report per Commander, plus what they authored and what they set aside, because it is
/// the same kind of
/// thing and a second shape would be a second set of bugs: written through <see cref="AtomicFile"/>,
/// polled by comparing <b>content</b> rather than a last-write time, keyed per Commander with the
/// key <b>inside</b> the document, hand-editable, and a line that cannot be read back reported
/// rather than dropped.
/// </para>
/// <para>
/// <b>Three things live here and only two of them are d47's.</b> The mined marks are a
/// recomputation and are replaced wholesale by the next run. The arcs a Commander wrote and the
/// arcs they set aside are <em>theirs</em>, are stored apart, and mining never touches either — the
/// same split every store here makes between what was mined and what was decided, for the same
/// reason: a
/// recomputation that erased a person's decision would resurrect exactly the thing they had already
/// refused.
/// </para>
/// </summary>
public sealed class GoalStore(string path, ILogger<GoalStore> logger)
{
    /// <summary>The key used where the journals never said who was flying.</summary>
    public const string NoCommander = "";

    /// <summary>Bounds how many arcs a person can invent. A page, not a novel.</summary>
    public const int MaxAuthored = 40;

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

    private Dictionary<string, GoalMine> _mines = new(StringComparer.Ordinal);
    private Dictionary<string, IReadOnlyList<GoalArc>> _authored = new(StringComparer.Ordinal);
    private Dictionary<string, IReadOnlyList<string>> _setAside = new(StringComparer.Ordinal);
    private IReadOnlyList<GoalProblem> _problems = [];
    private string? _seen;

    public string Path => path;

    public event Action? Changed;

    public IReadOnlyList<GoalProblem> Problems
    {
        get
        {
            lock (_gate)
            {
                return _problems;
            }
        }
    }

    /// <summary>The last walk over this Commander's journals, or null where none has been done.</summary>
    public GoalMine? MineFor(string? frontierId)
    {
        lock (_gate)
        {
            return _mines.GetValueOrDefault(frontierId ?? NoCommander);
        }
    }

    /// <summary>The arcs this Commander invented, in the order they wrote them.</summary>
    public IReadOnlyList<GoalArc> AuthoredBy(string? frontierId)
    {
        lock (_gate)
        {
            return _authored.GetValueOrDefault(frontierId ?? NoCommander, []);
        }
    }

    /// <summary>Arc keys this Commander does not want to see. Survives a re-mine.</summary>
    public IReadOnlyList<string> SetAsideBy(string? frontierId)
    {
        lock (_gate)
        {
            return _setAside.GetValueOrDefault(frontierId ?? NoCommander, []);
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
                    _mines = new Dictionary<string, GoalMine>(StringComparer.Ordinal);
                    _authored = new Dictionary<string, IReadOnlyList<GoalArc>>(StringComparer.Ordinal);
                    _setAside = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
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
    /// Files one walk's conclusions, replacing whatever that Commander had before — because mining
    /// is a recomputation, and merging would carry forward a count the current journals no longer
    /// support.
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

    /// <summary>Writes a goal the Commander invented. Returns null when it was refused, with why.</summary>
    public string? Author(string? frontierId, GoalArc arc)
    {
        ArgumentNullException.ThrowIfNull(arc);

        Poll();

        var commander = frontierId ?? NoCommander;

        lock (_gate)
        {
            var existing = _authored.GetValueOrDefault(commander, []);

            if (existing.Count >= MaxAuthored)
            {
                return $"You already have {MaxAuthored} of your own goals, which is the most I hold.";
            }

            if (GoalCatalogue.Find(arc.Key) is not null ||
                existing.Any(other => string.Equals(other.Key, arc.Key, StringComparison.OrdinalIgnoreCase)))
            {
                return $"There is already a goal keyed {arc.Key}.";
            }

            _authored = new Dictionary<string, IReadOnlyList<GoalArc>>(_authored, StringComparer.Ordinal)
            {
                [commander] = [.. existing, arc],
            };
        }

        Save();
        Changed?.Invoke();
        return null;
    }

    /// <summary>Replaces one authored arc — the only way its <see cref="GoalArc.Finished"/> moves.</summary>
    public bool Replace(string? frontierId, GoalArc arc)
    {
        ArgumentNullException.ThrowIfNull(arc);

        Poll();

        var commander = frontierId ?? NoCommander;

        lock (_gate)
        {
            var existing = _authored.GetValueOrDefault(commander, []);

            if (!existing.Any(other => string.Equals(other.Key, arc.Key, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            _authored = new Dictionary<string, IReadOnlyList<GoalArc>>(_authored, StringComparer.Ordinal)
            {
                [commander] =
                [
                    .. existing.Select(other =>
                        string.Equals(other.Key, arc.Key, StringComparison.OrdinalIgnoreCase) ? arc : other),
                ],
            };
        }

        Save();
        Changed?.Invoke();
        return true;
    }

    /// <summary>Drops an authored arc outright. A built-in one is set aside instead, never deleted.</summary>
    public bool Forget(string? frontierId, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        Poll();

        var commander = frontierId ?? NoCommander;

        lock (_gate)
        {
            var existing = _authored.GetValueOrDefault(commander, []);
            var kept = existing
                .Where(arc => !string.Equals(arc.Key, key.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (kept.Count == existing.Count)
            {
                return false;
            }

            _authored = new Dictionary<string, IReadOnlyList<GoalArc>>(_authored, StringComparer.Ordinal)
            {
                [commander] = kept,
            };
        }

        Save();
        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// Puts an arc out of sight, or brings it back. Returns whether anything moved.
    /// <para>
    /// Stored apart from everything mined and never touched by a run, so the Commander who set
    /// aside the Mercenary arc in March does not find it back on the page in April.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// Throws away everything mined, for every Commander, and reports how many marks went. The
    /// arcs a person wrote and the ones they set aside stay, for the reason the class remark gives.
    /// </summary>
    public int Empty()
    {
        Poll();

        int removed;

        lock (_gate)
        {
            removed = _mines.Sum(pair => pair.Value.Marks.Count);

            if (_mines.Count == 0 && _problems.Count == 0)
            {
                return 0;
            }

            _mines = new Dictionary<string, GoalMine>(StringComparer.Ordinal);
            _problems = [];
        }

        Save();
        Changed?.Invoke();
        return removed;
    }

    private void Save()
    {
        Document document;

        lock (_gate)
        {
            var commanders = _mines.Keys
                .Concat(_authored.Keys)
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
                            Goals = _authored.GetValueOrDefault(key, []) is { Count: > 0 } authored
                                ?
                                [
                                    .. authored.Select(arc => new GoalRecord
                                    {
                                        Key = arc.Key,
                                        Name = arc.Name,
                                        Done = arc.Done,
                                        Written = arc.Written,
                                        Finished = arc.Finished ? true : null,
                                        FinishedAt = arc.FinishedAt,
                                    }),
                                ]
                                : null,
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
        var mines = new Dictionary<string, GoalMine>(StringComparer.Ordinal);
        var authored = new Dictionary<string, IReadOnlyList<GoalArc>>(StringComparer.Ordinal);
        var aside = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        var problems = new List<GoalProblem>();

        try
        {
            foreach (var commander in JsonSerializer.Deserialize<Document>(text, Json)?.Commanders ?? [])
            {
                var key = commander.FrontierId ?? NoCommander;

                if (commander.Goals is { Count: > 0 } goals)
                {
                    var kept = new List<GoalArc>();

                    foreach (var record in goals)
                    {
                        if (string.IsNullOrWhiteSpace(record.Key) || string.IsNullOrWhiteSpace(record.Name))
                        {
                            problems.Add(new GoalProblem(record.Key ?? "(a goal)", "no key or no name"));
                            continue;
                        }

                        kept.Add(new GoalArc
                        {
                            Key = record.Key.Trim(),
                            Name = record.Name.Trim(),

                            // A hand-written goal with no definition of done still reads back. It
                            // costs the line its edge rather than its meaning, and refusing it
                            // would lose somebody's ambition to a missing field.
                            Done = string.IsNullOrWhiteSpace(record.Done)
                                ? "Yours to call finished."
                                : record.Done.Trim(),
                            Kind = GoalKind.Authored,
                            Written = record.Written,
                            Finished = record.Finished ?? false,
                            FinishedAt = record.FinishedAt,
                        });
                    }

                    authored[key] = kept;
                }

                if (commander.SetAside is { Count: > 0 } hidden)
                {
                    aside[key] =
                    [
                        .. hidden.Where(entry => !string.IsNullOrWhiteSpace(entry)).Select(entry => entry.Trim()),
                    ];
                }

                // A record carrying only a person's own goals is a real state — nothing has been
                // mined yet — and it gets no mine rather than an empty one, so "have you ever
                // looked" stays answerable.
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
            problems.Add(new GoalProblem(System.IO.Path.GetFileName(path), ex.Message));
            logger.LogWarning(ex, "Could not parse {Path}", path);
        }

        lock (_gate)
        {
            if (problems.Count == 0 || mines.Count > 0 || authored.Count > 0)
            {
                _mines = mines;
                _authored = authored;
                _setAside = aside;
            }

            _problems = problems;
            _seen = text;
        }

        logger.LogInformation(
            "Loaded goal progress for {Commanders} Commander(s) from {Path} ({Problems} refused)",
            _mines.Count,
            path,
            problems.Count);
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

        /// <summary>The Commander's own arcs. Written by a person, never by a mining run.</summary>
        public IReadOnlyList<GoalRecord>? Goals { get; set; }

        /// <summary>Read first and rewritten untouched — the other half of what a person authored.</summary>
        public IReadOnlyList<string>? SetAside { get; set; }
    }

    private sealed class MarkRecord
    {
        public string? Key { get; set; }

        public DateTimeOffset? Started { get; set; }

        public long? Have { get; set; }

        public DateTimeOffset? AsOf { get; set; }
    }

    private sealed class GoalRecord
    {
        public string? Key { get; set; }

        public string? Name { get; set; }

        public string? Done { get; set; }

        public DateTimeOffset? Written { get; set; }

        public bool? Finished { get; set; }

        public DateTimeOffset? FinishedAt { get; set; }
    }
}
