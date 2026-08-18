using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Habits;

/// <summary>
/// What d47 has noticed about the Commander, on disk (list.md Phase 32).
/// <para>
/// The <see cref="Memory.MemoryStore"/> shape with a different payload: written through
/// <see cref="AtomicFile"/>, polled by comparing <b>content</b> rather than a last-write time
/// (Phase 21's correction, for the reason <c>SwitchStore</c> records), keyed per Commander with the
/// key <b>inside</b> the document, and hand-editable — a line that cannot be read back is reported
/// rather than dropped.
/// </para>
/// <para>
/// <b>Dismissals are stored apart from the claims and outlive them.</b> This is the one structural
/// decision in the store and it is load-bearing. Mining rebuilds every claim from scratch — that is
/// what mining is — so a dismissal held as a flag on a claim record would be erased by the next run,
/// and the Commander would be told the same wrong thing a month later with no memory of their having
/// refused it. That is precisely the failure item 2 says "this whole phase risks". So the dismissed
/// keys are their own list, mining never touches them, and even emptying the store leaves them
/// standing.
/// </para>
/// </summary>
public sealed class HabitStore(string path, ILogger<HabitStore> logger)
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

    private Dictionary<string, HabitReport> _reports = new(StringComparer.Ordinal);
    private Dictionary<string, IReadOnlyList<string>> _dismissed = new(StringComparer.Ordinal);
    private IReadOnlyList<HabitProblem> _problems = [];
    private string? _seen;

    public string Path => path;

    /// <summary>Raised when the contents changed, whoever changed them. The panel follows this.</summary>
    public event Action? Changed;

    public IReadOnlyList<HabitProblem> Problems
    {
        get
        {
            lock (_gate)
            {
                return _problems;
            }
        }
    }

    /// <summary>The last mining run for one Commander, or null where none has been done.</summary>
    public HabitReport? For(string? frontierId)
    {
        lock (_gate)
        {
            return _reports.GetValueOrDefault(frontierId ?? NoCommander);
        }
    }

    /// <summary>Everything mined, whoever it is about. What the panel's window reads.</summary>
    public IReadOnlyList<HabitReport> Everything()
    {
        lock (_gate)
        {
            return [.. _reports.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Value)];
        }
    }

    /// <summary>Claim keys this Commander has refused, permanently.</summary>
    public IReadOnlyList<string> DismissedBy(string? frontierId)
    {
        lock (_gate)
        {
            return _dismissed.GetValueOrDefault(frontierId ?? NoCommander, []);
        }
    }

    /// <summary>Re-reads if the file changed. Pull-based and clock-free like every other reader in Core.</summary>
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
                    _reports = new Dictionary<string, HabitReport>(StringComparer.Ordinal);
                    _dismissed = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
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
    /// Files the results of one mining run, replacing whatever that Commander had before.
    /// <b>Replacing, because mining is a recomputation</b> — a run that merged with the last one
    /// would carry forward a claim the current journals no longer support.
    /// </summary>
    public void Record(IReadOnlyList<HabitReport> reports)
    {
        ArgumentNullException.ThrowIfNull(reports);

        Poll();

        lock (_gate)
        {
            var next = new Dictionary<string, HabitReport>(_reports, StringComparer.Ordinal);

            foreach (var report in reports)
            {
                next[report.FrontierId] = report;
            }

            _reports = next;
        }

        Save();
        Changed?.Invoke();
    }

    /// <summary>
    /// Refuses one claim, for good. Returns whether it was not already refused.
    /// <para>
    /// Item 2: "the same wrong observation arriving monthly is the failure mode this whole phase
    /// risks". A dismissal is the Commander's instruction rather than an observation, which is why
    /// it survives both a re-mine and <see cref="Empty"/>.
    /// </para>
    /// </summary>
    public bool Dismiss(string? frontierId, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        Poll();

        var commander = frontierId ?? NoCommander;

        lock (_gate)
        {
            var existing = _dismissed.GetValueOrDefault(commander, []);

            if (existing.Contains(key, StringComparer.Ordinal))
            {
                return false;
            }

            _dismissed = new Dictionary<string, IReadOnlyList<string>>(_dismissed, StringComparer.Ordinal)
            {
                [commander] = [.. existing, key],
            };
        }

        Save();
        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// Throws away everything mined, for every Commander, and reports how many claims went.
    /// <para>
    /// <b>The dismissals stay.</b> They are the only thing in this file a person authored, and a
    /// button that erased them would resurrect exactly the observations the Commander had already
    /// refused — which is the outcome the split above exists to prevent. The privacy row says so
    /// in as many words.
    /// </para>
    /// </summary>
    public int Empty()
    {
        Poll();

        int removed;

        lock (_gate)
        {
            removed = _reports.Sum(pair => pair.Value.Claims.Count);

            if (_reports.Count == 0 && _problems.Count == 0)
            {
                return 0;
            }

            _reports = new Dictionary<string, HabitReport>(StringComparer.Ordinal);
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
            var commanders = _reports.Keys
                .Concat(_dismissed.Keys)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(key => key, StringComparer.Ordinal);

            document = new Document
            {
                Commanders =
                [
                    .. commanders.Select(key => new CommanderRecord
                    {
                        FrontierId = key,
                        MinedAt = _reports.GetValueOrDefault(key)?.MinedAt,
                        Journals = _reports.GetValueOrDefault(key)?.Journals ?? 0,
                        From = _reports.GetValueOrDefault(key)?.From,
                        To = _reports.GetValueOrDefault(key)?.To,
                        Refusal = _reports.GetValueOrDefault(key)?.Refusal,
                        Claims =
                        [
                            .. (_reports.GetValueOrDefault(key)?.Claims ?? []).Select(claim => new ClaimRecord
                            {
                                Key = claim.Key,
                                Subject = claim.Subject,
                                Observation = claim.Observation,
                                Denominator = claim.Denominator,
                                Occasion = claim.Occasion,
                                Occurrences = claim.Evidence.Occurrences,
                                Opportunities = claim.Evidence.Opportunities,
                                Recent = claim.Evidence.Recent,
                                Latest = claim.Evidence.Latest,
                                From = claim.Evidence.From,
                                To = claim.Evidence.To,
                                Journals = claim.Evidence.Journals,
                            }),
                        ],
                        Quiet =
                        [
                            .. (_reports.GetValueOrDefault(key)?.Quiet ?? []).Select(silence => new SilenceRecord
                            {
                                Key = silence.Key,
                                Subject = silence.Subject,
                                Why = silence.Why,
                            }),
                        ],
                        Dismissed = _dismissed.GetValueOrDefault(key, []) is { Count: > 0 } dismissed
                            ? [.. dismissed]
                            : null,
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
        var reports = new Dictionary<string, HabitReport>(StringComparer.Ordinal);
        var dismissals = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        var problems = new List<HabitProblem>();

        try
        {
            foreach (var commander in JsonSerializer.Deserialize<Document>(text, Json)?.Commanders ?? [])
            {
                var key = commander.FrontierId ?? NoCommander;
                var claims = new List<HabitClaim>();

                foreach (var record in commander.Claims ?? [])
                {
                    if (string.IsNullOrWhiteSpace(record.Key) || string.IsNullOrWhiteSpace(record.Observation))
                    {
                        problems.Add(new HabitProblem(record.Key ?? "(a claim)", "no key or nothing claimed"));
                        continue;
                    }

                    claims.Add(new HabitClaim(record.Key.Trim(), record.Subject ?? record.Key.Trim())
                    {
                        Observation = record.Observation.Trim(),

                        // A hand-edited claim with no denominator still reads back, because the
                        // file is meant to be editable and a missing noun costs a sentence its
                        // polish rather than its meaning.
                        Denominator = string.IsNullOrWhiteSpace(record.Denominator)
                            ? "chances"
                            : record.Denominator.Trim(),
                        Occasion = record.Occasion,
                        Evidence = new HabitEvidence
                        {
                            Occurrences = record.Occurrences,
                            Opportunities = record.Opportunities,
                            Recent = record.Recent,
                            Latest = record.Latest,
                            From = record.From,
                            To = record.To,
                            Journals = record.Journals,
                        },
                    });
                }

                if (commander.Dismissed is { Count: > 0 } dismissed)
                {
                    dismissals[key] =
                    [
                        .. dismissed.Where(entry => !string.IsNullOrWhiteSpace(entry)).Select(entry => entry.Trim()),
                    ];
                }

                // A commander record carrying only dismissals is a real state — everything mined
                // was emptied and the refusals stayed — and it gets no report rather than an empty
                // one, so "have you ever looked" stays answerable.
                if (commander.MinedAt is not { } minedAt)
                {
                    continue;
                }

                reports[key] = new HabitReport
                {
                    FrontierId = key,
                    Journals = commander.Journals,
                    From = commander.From,
                    To = commander.To,
                    MinedAt = minedAt,
                    Refusal = commander.Refusal,
                    Claims = claims,
                    Quiet =
                    [
                        .. (commander.Quiet ?? [])
                            .Where(record => !string.IsNullOrWhiteSpace(record.Key))
                            .Select(record => new HabitSilence(
                                record.Key!.Trim(),
                                record.Subject ?? record.Key!.Trim(),
                                record.Why ?? "no reason recorded")),
                    ],
                };
            }
        }
        catch (JsonException ex)
        {
            problems.Add(new HabitProblem(System.IO.Path.GetFileName(path), ex.Message));
            logger.LogWarning(ex, "Could not parse {Path}", path);
        }

        lock (_gate)
        {
            if (problems.Count == 0 || reports.Count > 0)
            {
                _reports = reports;
                _dismissed = dismissals;
            }

            _problems = problems;
            _seen = text;
        }

        logger.LogInformation(
            "Loaded {Claims} noticed habits from {Path} ({Problems} refused)",
            _reports.Sum(pair => pair.Value.Claims.Count),
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

        public string? Refusal { get; set; }

        public IReadOnlyList<ClaimRecord>? Claims { get; set; }

        public IReadOnlyList<SilenceRecord>? Quiet { get; set; }

        /// <summary>Written last and read first — the only part of this file a person authored.</summary>
        public IReadOnlyList<string>? Dismissed { get; set; }
    }

    private sealed class ClaimRecord
    {
        public string? Key { get; set; }

        public string? Subject { get; set; }

        public string? Observation { get; set; }

        public string? Denominator { get; set; }

        public HabitOccasion Occasion { get; set; }

        public int Occurrences { get; set; }

        public int Opportunities { get; set; }

        public int Recent { get; set; }

        public DateTimeOffset? Latest { get; set; }

        public DateTimeOffset From { get; set; }

        public DateTimeOffset To { get; set; }

        public int Journals { get; set; }
    }

    private sealed class SilenceRecord
    {
        public string? Key { get; set; }

        public string? Subject { get; set; }

        public string? Why { get; set; }
    }
}

/// <summary>One claim that could not be read back, and why. Kept rather than dropped.</summary>
public sealed record HabitProblem(string What, string Why);
