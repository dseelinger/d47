using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Diagnostics.Flight;

/// <summary>One utterance, ready to be written down. Everything but the identity, which the log issues.</summary>
/// <param name="Wav">The clip as a whole file, header and all, so it is playable where it lands.</param>
public sealed record FlightCapture(
    FlightDirection Direction,
    DateTimeOffset When,
    byte[] Wav,
    TimeSpan Duration)
{
    public string Text { get; init; } = string.Empty;

    public string? Phonemes { get; init; }

    public string? Provider { get; init; }

    public string? Voice { get; init; }

    public string? Model { get; init; }

    public TimeSpan Elapsed { get; init; }
}

/// <summary>
/// The capped ring the audio flight recorder writes to, and the two corpora a kept row joins
/// (<a href="https://github.com/dseelinger/d47/issues/164">#164</a>).
/// <para>
/// <b>The cap is enforced here, by the writer, rather than by anybody remembering.</b> That is
/// the shape <c>logs\</c> and <c>data\backups\</c> already have — fourteen days and ten deploys,
/// each decided by the code that writes — and it is the shape this had to have on day one,
/// because a retention rule added after a feature ships arrives too late for everyone who
/// already ran it. Retained audio is the most sensitive thing d47 holds: what the transcriber
/// heard is a rolling recording of a room, and it catches people who never installed d47.
/// </para>
/// <para>
/// <b>A kept row is exempt, and that is not an oversight.</b> Keeping a row is the Commander
/// saying this one is a test case, which means outliving the rolling window is the whole
/// request. Kept clips are copied out of the ring so that eviction cannot reach them, and the
/// wipe still takes them — a wipe that left recordings behind would not be one.
/// </para>
/// <para>
/// Owns no thread and reads no clock: every timestamp arrives on a <see cref="FlightCapture"/>,
/// and whoever calls this decides which thread it happens on (architecture.md, invariants).
/// </para>
/// </summary>
public sealed class FlightLog
{
    /// <summary>
    /// What the rolling window costs at most. A session is tens of megabytes at 16 kHz mono, so
    /// this is a few flights rather than a number anybody has to think about — which is the
    /// point of stating it rather than leaving the sizing question open.
    /// </summary>
    public const long CapBytes = 200L * 1024 * 1024;

    /// <summary>Where kept clips and the two corpus manifests go. Never evicted.</summary>
    public const string KeptFolderName = "kept";

    private const string IndexFileName = "index.json";
    private const string MishearsFileName = "mishears.json";
    private const string PronunciationsFileName = "pronunciations.json";

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _folder;
    private readonly ILogger _logger;
    private readonly long _cap;
    private readonly Lock _gate = new();

    private List<FlightRow> _rows;

    /// <param name="cap">
    /// What the rolling window costs at most. <b>The default is the policy</b> — no caller in the
    /// app passes anything else, and there is deliberately no setting for it. It is a parameter
    /// so that a test can prove eviction happens without writing two hundred megabytes to do it,
    /// which is the difference between the rule being asserted and being taken on trust.
    /// </param>
    public FlightLog(string folder, ILogger logger, long cap = CapBytes)
    {
        _folder = folder;
        _logger = logger;
        _cap = cap;
        _rows = Read();
    }

    /// <summary>The folder the whole record lives in, for a row that wants to name it.</summary>
    public string Folder => _folder;

    /// <summary>Newest first, which is the order a review pane wants and the reverse of the ring's.</summary>
    public IReadOnlyList<FlightRow> Rows
    {
        get
        {
            lock (_gate)
            {
                return [.. _rows.OrderByDescending(row => row.Id, StringComparer.Ordinal)];
            }
        }
    }

    /// <summary>What the whole record costs on disk right now, kept clips included.</summary>
    public long Bytes
    {
        get
        {
            lock (_gate)
            {
                return _rows.Sum(row => row.Bytes) + KeptBytes();
            }
        }
    }

    /// <summary>
    /// Writes one utterance down and evicts whatever that pushed past the cap.
    /// <para>
    /// Eviction runs on every add rather than on a schedule, so the folder is under the cap at
    /// every moment there is nothing being written — including the moment the app is killed,
    /// which is the moment a scheduled sweep would have missed.
    /// </para>
    /// </summary>
    public FlightRow Add(FlightCapture capture)
    {
        ArgumentNullException.ThrowIfNull(capture);

        var row = new FlightRow
        {
            Id = Identify(capture),
            Direction = capture.Direction,
            When = capture.When,
            Text = capture.Text,
            Phonemes = capture.Phonemes,
            Provider = capture.Provider,
            Voice = capture.Voice,
            Model = capture.Model,
            Elapsed = capture.Elapsed,
            Duration = capture.Duration,
            Bytes = capture.Wav.Length,
        };

        lock (_gate)
        {
            Directory.CreateDirectory(_folder);
            File.WriteAllBytes(Path.Combine(_folder, row.Clip), capture.Wav);

            _rows.Add(row);
            Evict();
            Save();
        }

        return row;
    }

    /// <summary>
    /// Keeps one row as a test case: the clip is copied where eviction cannot reach it, and the
    /// pair it makes is appended to the corpus its kind belongs to.
    /// <para>
    /// <paramref name="expected"/> is the half a recording cannot supply — the words that were
    /// actually said, or the IPA the line should have been given. It is typed by the Commander,
    /// which is what makes this an adoption rather than d47 grading its own homework.
    /// </para>
    /// </summary>
    /// <returns>The row as it now stands, or null if it has been evicted since it was listed.</returns>
    public FlightRow? Keep(string id, FlightKeepKind kind, string expected, DateTimeOffset when)
    {
        lock (_gate)
        {
            var index = _rows.FindIndex(row => string.Equals(row.Id, id, StringComparison.Ordinal));

            if (index < 0)
            {
                return null;
            }

            var kept = _rows[index] with { Kept = new FlightKeep(kind, when, expected) };
            _rows[index] = kept;

            var into = Path.Combine(_folder, KeptFolderName);
            Directory.CreateDirectory(into);

            var clip = Path.Combine(_folder, kept.Clip);

            if (File.Exists(clip))
            {
                File.Copy(clip, Path.Combine(into, kept.Clip), overwrite: true);
            }

            Append(kind, kept);
            Save();

            _logger.LogInformation("Kept {Id} as a {Kind} test case", kept.Id, kind);

            return kept;
        }
    }

    /// <summary>
    /// Deletes every recording, kept clips and corpora included.
    /// <para>
    /// <b>All of it, and that is deliberate.</b> A wipe that spared the kept rows would leave
    /// the Commander's voice on disk under a button that said it had been deleted, which is a
    /// worse promise than not offering the button.
    /// </para>
    /// </summary>
    public void Empty()
    {
        lock (_gate)
        {
            _rows = [];

            if (!Directory.Exists(_folder))
            {
                return;
            }

            try
            {
                Directory.Delete(_folder, recursive: true);
                _logger.LogInformation("Emptied the audio flight recorder");
            }
            catch (Exception ex)
            {
                // A clip the Commander has open in a player, most likely. Said rather than
                // thrown: the rest of the wipe happened, and the row will report what is left.
                _logger.LogWarning(ex, "Could not delete every recording under {Folder}", _folder);
            }
        }
    }

    /// <summary>One line for the settings row: how much is held, and how much of it is kept.</summary>
    public string Summary()
    {
        lock (_gate)
        {
            if (_rows.Count == 0)
            {
                return "Nothing recorded yet this flight.";
            }

            var heard = _rows.Count(row => row.Direction == FlightDirection.Heard);
            var kept = _rows.Count(row => row.Kept is not null);
            var megabytes = (_rows.Sum(row => row.Bytes) + KeptBytes()) / (1024d * 1024d);

            return $"{_rows.Count} utterances — {heard} heard, {_rows.Count - heard} said — "
                + $"{megabytes:0.#} MB of {_cap / (1024 * 1024)} MB"
                + (kept == 0 ? "." : $", {kept} kept as test cases.");
        }
    }

    /// <summary>
    /// Sortable to the millisecond, with the direction in it so a heard row and a said row
    /// landing in the same millisecond cannot collide over one file name.
    /// </summary>
    private string Identify(FlightCapture capture)
    {
        var stem = $"{capture.When:yyyyMMdd-HHmmss-fff}-{(capture.Direction == FlightDirection.Heard ? "heard" : "said")}";

        if (!_rows.Exists(row => string.Equals(row.Id, stem, StringComparison.Ordinal)))
        {
            return stem;
        }

        for (var suffix = 2; ; suffix++)
        {
            var candidate = $"{stem}-{suffix}";

            if (!_rows.Exists(row => string.Equals(row.Id, candidate, StringComparison.Ordinal)))
            {
                return candidate;
            }
        }
    }

    /// <summary>Oldest first, until what is left fits. Kept rows are passed over.</summary>
    private void Evict()
    {
        var total = _rows.Sum(row => row.Bytes);

        foreach (var row in _rows.OrderBy(row => row.Id, StringComparer.Ordinal).ToList())
        {
            if (total <= _cap)
            {
                return;
            }

            if (row.Kept is not null)
            {
                continue;
            }

            Delete(row);
            _rows.Remove(row);
            total -= row.Bytes;
        }
    }

    private void Delete(FlightRow row)
    {
        try
        {
            File.Delete(Path.Combine(_folder, row.Clip));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete {Clip}", row.Clip);
        }
    }

    /// <summary>
    /// Appends the pair to its corpus. Read, add, write — the file is a handful of entries and
    /// is read by a person as often as by the suite, so it stays a formatted array rather than
    /// becoming an append-only log nobody can skim.
    /// </summary>
    private void Append(FlightKeepKind kind, FlightRow row)
    {
        var file = Path.Combine(
            _folder,
            KeptFolderName,
            kind == FlightKeepKind.Mishear ? MishearsFileName : PronunciationsFileName);

        var entries = new List<KeptCase>();

        if (File.Exists(file))
        {
            try
            {
                entries = JsonSerializer.Deserialize<List<KeptCase>>(File.ReadAllText(file), Json) ?? [];
            }
            catch (JsonException ex)
            {
                // A hand-edited corpus that no longer parses. Kept beside the new one rather
                // than overwritten: the entries in it are the Commander's own work.
                var spoiled = file + ".unreadable";
                _logger.LogWarning(ex, "{File} could not be read; it is now {Spoiled}", file, spoiled);
                File.Move(file, spoiled, overwrite: true);
            }
        }

        entries.RemoveAll(entry => string.Equals(entry.Clip, row.Clip, StringComparison.Ordinal));

        entries.Add(new KeptCase
        {
            Clip = row.Clip,
            Text = row.Text,
            Expected = row.Kept?.Expected ?? string.Empty,
            Phonemes = row.Phonemes,
            Provider = row.Provider,
            Voice = row.Voice,
            Model = row.Model,
            Recorded = row.When,
        });

        AtomicFile.WriteAllText(file, JsonSerializer.Serialize(entries, Json));
    }

    private long KeptBytes()
    {
        var into = Path.Combine(_folder, KeptFolderName);

        if (!Directory.Exists(into))
        {
            return 0;
        }

        try
        {
            return new DirectoryInfo(into).EnumerateFiles().Sum(file => file.Length);
        }
        catch (IOException)
        {
            return 0;
        }
    }

    private List<FlightRow> Read()
    {
        var file = Path.Combine(_folder, IndexFileName);

        if (!File.Exists(file))
        {
            return [];
        }

        try
        {
            var rows = JsonSerializer.Deserialize<List<FlightRow>>(File.ReadAllText(file), Json) ?? [];

            // A row whose clip went with a manual delete of the folder is not a row any more.
            // Reconciling on load rather than trusting the index keeps the cap arithmetic true.
            return [.. rows.Where(row => File.Exists(Path.Combine(_folder, row.Clip)))];
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            _logger.LogWarning(ex, "The flight recorder index could not be read; starting a fresh one");
            return [];
        }
    }

    private void Save()
    {
        try
        {
            AtomicFile.WriteAllText(
                Path.Combine(_folder, IndexFileName),
                JsonSerializer.Serialize(_rows, Json));
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Could not write the flight recorder index");
        }
    }

    /// <summary>
    /// One entry in a corpus file, which is a different shape from a row: it is a test case
    /// rather than a recording, so it names what should have happened and drops the timings.
    /// </summary>
    private sealed record KeptCase
    {
        public string Clip { get; init; } = string.Empty;

        /// <summary>What d47 produced — the mishear itself, or the line that was said.</summary>
        public string Text { get; init; } = string.Empty;

        /// <summary>What it should have been. The Commander's word.</summary>
        public string Expected { get; init; } = string.Empty;

        public string? Phonemes { get; init; }

        public string? Provider { get; init; }

        public string? Voice { get; init; }

        public string? Model { get; init; }

        public DateTimeOffset Recorded { get; init; }
    }
}
