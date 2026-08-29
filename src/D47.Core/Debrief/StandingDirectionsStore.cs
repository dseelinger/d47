using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Debrief;

/// <summary>
/// The standing-directions file, and the only thing the debrief pass may write
/// (<a href="https://github.com/dseelinger/d47/issues/162">#162</a>).
/// <para>
/// <b>Built on <see cref="Memory.MemoryStore"/>'s shape on purpose.</b> Same two halves: polled by
/// content so a hand edit is live without a restart, and keyed per Commander <b>inside</b> the
/// document, because a Frontier id comes out of the journal and turning untrusted input into a
/// filename buys a path-traversal surface for an organisational convenience. A second shape would
/// be a second set of bugs, and this file holds the Commander's own words exactly as that one does.
/// </para>
/// <para>
/// <b>The fence is checked twice, and the second check is the one that matters.</b> Construction
/// refuses a path that is not the standing-directions file, which catches the obvious mistake; and
/// <see cref="Save"/> checks again, which catches the one that would actually happen — a store
/// handed a legal path and later asked, by a caller nobody has written yet, to put its contents
/// somewhere else. See <see cref="DebriefWriteFence"/> for what is refused and why.
/// </para>
/// </summary>
public sealed class StandingDirectionsStore
{
    /// <summary>The key entries written before the journal has said who is flying are filed under.</summary>
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

    private readonly string _path;
    private readonly ILogger _logger;
    private readonly Lock _gate = new();

    private Dictionary<string, IReadOnlyList<StandingDirection>> _byCommander = new(StringComparer.Ordinal);
    private IReadOnlyList<DirectionProblem> _problems = [];

    /// <summary>The file's contents as last read. What "has it changed" is answered against.</summary>
    private string? _seen;

    /// <exception cref="DebriefWriteRefused">The path is not the standing-directions file.</exception>
    public StandingDirectionsStore(string path, ILogger<StandingDirectionsStore> logger)
    {
        // Before the field is even assigned. A store that exists pointed at the guardrails is a
        // store somebody can later call Save on.
        DebriefWriteFence.Enforce(path);

        _path = path;
        _logger = logger;
    }

    public string Path => _path;

    /// <summary>Raised when the contents changed, whoever changed them. The pane follows this.</summary>
    public event Action? Changed;

    /// <summary>Entries that were refused, and why. Empty in the ordinary case.</summary>
    public IReadOnlyList<DirectionProblem> Problems
    {
        get
        {
            lock (_gate)
            {
                return _problems;
            }
        }
    }

    /// <summary>Everything filed under one Commander, oldest first.</summary>
    public IReadOnlyList<StandingDirection> For(string? frontierId)
    {
        lock (_gate)
        {
            return _byCommander.GetValueOrDefault(frontierId ?? NoCommander, []);
        }
    }

    /// <summary>Every entry in the file, whoever it is for. What "readable in one place" means.</summary>
    public IReadOnlyList<(string FrontierId, StandingDirection Entry)> Everything()
    {
        lock (_gate)
        {
            return
            [
                .. _byCommander
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .SelectMany(pair => pair.Value.Select(entry => (pair.Key, entry))),
            ];
        }
    }

    /// <summary>
    /// Re-reads if the file changed. Pull-based and clock-free like every other reader in Core, so
    /// a direction edited in a text editor is live without a restart — and, because adoption only
    /// reaches a prompt at a session boundary, without changing a prompt mid-session either.
    /// </summary>
    public bool Poll()
    {
        string text;

        try
        {
            if (!File.Exists(_path))
            {
                // Not an error: an empty file is the normal state until the first debrief runs.
                if (_seen is null)
                {
                    return false;
                }

                lock (_gate)
                {
                    _byCommander = new Dictionary<string, IReadOnlyList<StandingDirection>>(StringComparer.Ordinal);
                    _problems = [];
                    _seen = null;
                }

                Changed?.Invoke();
                return true;
            }

            using var stream = new FileStream(
                _path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

            using var reader = new StreamReader(stream);
            text = reader.ReadToEnd();
        }
        catch (IOException ex)
        {
            // A read that lands mid-write is retried next tick. The writer is atomic, so the
            // window is a rename and this is never a lasting state.
            _logger.LogDebug(ex, "Could not read {Path}", _path);
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
    /// Writes one entry, replacing whatever shared its key. The single write path, so the pass and
    /// the pane cannot disagree about what a write does.
    /// <para>
    /// Polls first, so a hand edit made since the last tick is not overwritten by a change made on
    /// top of a stale copy.
    /// </para>
    /// </summary>
    public StandingDirection Write(string? frontierId, StandingDirection entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        Poll();

        var commander = frontierId ?? NoCommander;

        lock (_gate)
        {
            var kept = _byCommander
                .GetValueOrDefault(commander, [])
                .Where(existing => !string.Equals(existing.Key, entry.Key, StringComparison.Ordinal));

            _byCommander = new Dictionary<string, IReadOnlyList<StandingDirection>>(_byCommander, StringComparer.Ordinal)
            {
                [commander] = [.. kept, entry],
            };
        }

        Save();
        Changed?.Invoke();
        return entry;
    }

    /// <summary>Removes one entry outright, by key. Returns whether there was one.</summary>
    public bool Remove(string? frontierId, string key)
    {
        Poll();

        var commander = frontierId ?? NoCommander;

        lock (_gate)
        {
            var existing = _byCommander.GetValueOrDefault(commander, []);
            var kept = existing.Where(entry => !string.Equals(entry.Key, key, StringComparison.Ordinal)).ToArray();

            if (kept.Length == existing.Count)
            {
                return false;
            }

            _byCommander = new Dictionary<string, IReadOnlyList<StandingDirection>>(_byCommander, StringComparer.Ordinal)
            {
                [commander] = kept,
            };
        }

        Save();
        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// Empties the file — every Commander, not just the one flying — and reports how many went.
    /// What the privacy erase reaches, on the same reading <see cref="Memory.MemoryStore.Empty"/>
    /// records: a Commander pressing it means "forget me", not "forget this character".
    /// </summary>
    public int Empty()
    {
        Poll();

        int removed;

        lock (_gate)
        {
            removed = _byCommander.Sum(pair => pair.Value.Count);

            if (removed == 0 && _problems.Count == 0)
            {
                return 0;
            }

            _byCommander = new Dictionary<string, IReadOnlyList<StandingDirection>>(StringComparer.Ordinal);
            _problems = [];
        }

        Save();
        Changed?.Invoke();
        return removed;
    }

    private void Save()
    {
        // Again, and this is the check that earns its keep. Construction proves the store was
        // built legally; this proves the write is going where the store said it would.
        DebriefWriteFence.Enforce(_path);

        Document document;

        lock (_gate)
        {
            document = new Document
            {
                Commanders =
                [
                    .. _byCommander
                        .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                        .Select(pair => new CommanderRecord
                        {
                            FrontierId = pair.Key,
                            Directions =
                            [
                                .. pair.Value.Select(entry => new EntryRecord
                                {
                                    Key = entry.Key,
                                    Text = entry.Text,
                                    State = entry.State,
                                    Kind = entry.Kind,
                                    Because = entry.Because.Length == 0 ? null : entry.Because,
                                    Suggested = entry.Suggested,
                                    Persona = entry.Persona,
                                    SaidUnder = entry.SaidUnder,
                                    Clip = entry.Clip,
                                    ProposedAt = entry.ProposedAt,
                                    AdoptedAt = entry.AdoptedAt,
                                }),
                            ],
                        }),
                ],
            };
        }

        var text = JsonSerializer.Serialize(document, Json);

        try
        {
            AtomicFile.WriteAllText(_path, text);

            lock (_gate)
            {
                _seen = text;
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or NotSupportedException)
        {
            _logger.LogWarning(ex, "Could not write {Path}", _path);
        }
    }

    private void Reload(string text)
    {
        var loaded = new Dictionary<string, IReadOnlyList<StandingDirection>>(StringComparer.Ordinal);
        var problems = new List<DirectionProblem>();

        try
        {
            foreach (var commander in JsonSerializer.Deserialize<Document>(text, Json)?.Commanders ?? [])
            {
                var entries = new List<StandingDirection>();
                var keys = new HashSet<string>(StringComparer.Ordinal);

                foreach (var record in commander.Directions ?? [])
                {
                    if (string.IsNullOrWhiteSpace(record.Text))
                    {
                        problems.Add(new DirectionProblem(record.Key ?? "(an entry)", "nothing in it"));
                        continue;
                    }

                    var key = string.IsNullOrWhiteSpace(record.Key)
                        ? DirectionKeys.Next(DirectionKeys.HandPrefix, keys)
                        : record.Key.Trim();

                    if (!keys.Add(key))
                    {
                        problems.Add(new DirectionProblem(key, "two entries share this key"));
                        continue;
                    }

                    entries.Add(new StandingDirection(key, Clamp(record.Text))
                    {
                        State = record.State,
                        Kind = record.Kind,
                        Because = record.Because?.Trim() ?? string.Empty,
                        Suggested = record.Suggested?.Trim(),
                        Persona = Blank(record.Persona),
                        SaidUnder = Blank(record.SaidUnder),
                        Clip = Blank(record.Clip),
                        ProposedAt = record.ProposedAt,
                        AdoptedAt = record.AdoptedAt,
                    });
                }

                loaded[commander.FrontierId ?? NoCommander] = entries;
            }
        }
        catch (JsonException ex)
        {
            // The whole file, not one entry. Reported rather than discarded, for the reason the
            // memory store reports rather than discards: some of these are the Commander's own
            // words, and starting empty looks exactly like d47 having dropped them.
            problems.Add(new DirectionProblem(System.IO.Path.GetFileName(_path), ex.Message));
            _logger.LogWarning(ex, "Could not parse {Path}", _path);
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

        _logger.LogInformation(
            "Loaded {Count} standing directions from {Path} ({Adopted} adopted, {Problems} refused)",
            _byCommander.Sum(pair => pair.Value.Count),
            _path,
            _byCommander.Sum(pair => pair.Value.Count(entry => entry.State == DirectionState.Adopted)),
            problems.Count);
    }

    private static string Clamp(string text)
    {
        var trimmed = text.Trim();

        return trimmed.Length > StandingDirection.MaxText
            ? trimmed[..StandingDirection.MaxText].TrimEnd() + "…"
            : trimmed;
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class Document
    {
        public IReadOnlyList<CommanderRecord> Commanders { get; set; } = [];
    }

    private sealed class CommanderRecord
    {
        public string? FrontierId { get; set; }

        public IReadOnlyList<EntryRecord>? Directions { get; set; }
    }

    private sealed class EntryRecord
    {
        public string? Key { get; set; }

        public string? Text { get; set; }

        /// <summary>
        /// <b>Defaulted to proposed, which is the opposite of what the memory file does</b> and is
        /// the right default here for the same reason that one defaults the other way. An
        /// unlabelled line in <c>memories.json</c> is a fact somebody typed about themselves, and
        /// the safest reading of it is their own word. An unlabelled line here would be an
        /// instruction going into a prompt, and the safest reading of that is that nobody has
        /// agreed to it yet. Adoption is a person's act, and a missing field is not one.
        /// </summary>
        public DirectionState State { get; set; } = DirectionState.Proposed;

        public DirectionKind Kind { get; set; } = DirectionKind.Direction;

        public string? Because { get; set; }

        public string? Suggested { get; set; }

        public string? Persona { get; set; }

        public string? SaidUnder { get; set; }

        public string? Clip { get; set; }

        public DateTimeOffset? ProposedAt { get; set; }

        public DateTimeOffset? AdoptedAt { get; set; }
    }
}

/// <summary>
/// Where a direction's key comes from. Deterministic and lowest-unused, the rule every other store
/// in Core follows, and for the same reason: Core reads no clock and owns no randomness, so a key
/// has to be a function of what is already there.
/// </summary>
public static class DirectionKeys
{
    /// <summary>Drafted by the pass from something the Commander said.</summary>
    public const string DraftedPrefix = "drafted";

    /// <summary>Drafted from a pattern nobody put into words.</summary>
    public const string AskedPrefix = "asked";

    /// <summary>Written into the file by hand with no key on it.</summary>
    public const string HandPrefix = "hand";

    /// <summary>The lowest <c>prefix-N</c> not already taken.</summary>
    public static string Next(string prefix, IReadOnlyCollection<string> taken)
    {
        for (var n = 1; ; n++)
        {
            var candidate = $"{prefix}-{n}";

            if (!taken.Contains(candidate))
            {
                return candidate;
            }
        }
    }
}
