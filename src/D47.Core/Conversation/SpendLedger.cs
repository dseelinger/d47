using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace D47.Core.Conversation;

/// <summary>What kind of spend a row is. The two are billed in different units and priced apart.</summary>
public enum SpendKind
{
    Model,
    Voice,
}

/// <summary>
/// One charge, with the instant it happened.
/// <para>
/// <b>The instant is the field the whole feature turns on</b>, and it is absolute. Everything
/// the dialog reports is a question about a stretch of time, and none of it could be answered
/// from <see cref="TurnCost"/>, which carries no timestamp at all.
/// </para>
/// <para>
/// <see cref="Priced"/> travels with the row rather than being inferred later. A model with no
/// entry in the price table costs an unknown amount, not nothing, and a total that quietly
/// treated it as zero would be wrong in the direction that reassures.
/// </para>
/// </summary>
public sealed record SpendEntry
{
    public DateTimeOffset At { get; init; }

    public SpendKind Kind { get; init; }

    public string ProviderId { get; init; } = string.Empty;

    /// <summary>The model that answered, or the voice provider that spoke.</summary>
    public string Model { get; init; } = string.Empty;

    public decimal Dollars { get; init; }

    public bool Priced { get; init; }

    public int InputTokens { get; init; }

    public int CacheWriteTokens { get; init; }

    public int CacheReadTokens { get; init; }

    public int OutputTokens { get; init; }

    public int WebSearchRequests { get; init; }

    /// <summary>Characters sent to a speech provider. Zero on a model row; speech is billed by these.</summary>
    public long Characters { get; init; }

    /// <summary>
    /// How much audio a speech charge produced, for a provider billed by the length of it rather
    /// than by the characters handed over
    /// (<a href="https://github.com/dseelinger/d47/issues/63">#63</a>).
    /// <para>
    /// Null on a model row and on a charge from a provider that bills by the character, so the
    /// absence says which kind of charge this was rather than claiming a zero-length clip. Rows
    /// written before this existed read as null, which is correct for every one of them.
    /// </para>
    /// </summary>
    public double? AudioSeconds { get; init; }

    /// <summary>
    /// On a <em>reset mark</em>, the instant its window began. Null on every charge
    /// (<a href="https://github.com/dseelinger/d47/issues/197">#197</a>).
    /// <para>
    /// <b>A mark rather than a deletion, so the file stays append-only.</b> Rewriting the whole
    /// document to drop rows would spend the invariant this format exists for — a crash mid-write
    /// costs the last line rather than the file — and it would spend it on the one number in the
    /// app that represents real money. A mark keeps the history recoverable, makes the act
    /// auditable, and makes an accidental reset undoable by deleting one line by hand.
    /// </para>
    /// <para>
    /// Together with <see cref="At"/> it is a closed interval, and every charge stamped inside it
    /// stops counting in <em>every</em> window — which is the rule the ask states, and which the
    /// query-per-window model already made true by construction: a row leaves exactly the windows
    /// whose span contains its instant. Marks compose, because a charge is dropped if any of them
    /// covers it.
    /// </para>
    /// </summary>
    public DateTimeOffset? ResetFrom { get; init; }

    /// <summary>
    /// What the Commander called the window they reset, for the audit trail. Read by nothing —
    /// <see cref="ResetFrom"/> and <see cref="At"/> are the whole of what a total consults — and
    /// written so somebody opening the file can tell one mark from another.
    /// </summary>
    public string? ResetWindow { get; init; }

    /// <summary>Whether this row is a reset mark rather than a charge.</summary>
    [JsonIgnore]
    public bool IsReset => ResetFrom is not null;

    /// <summary>Whether this mark covers a charge, and so stops it counting anywhere.</summary>
    public bool Covers(SpendEntry charge) =>
        ResetFrom is { } from && charge.At >= from && charge.At <= At;
}

/// <summary>
/// What one model or one voice provider cost inside a window
/// (<a href="https://github.com/dseelinger/d47/issues/226">#226</a>).
/// <para>
/// <b>Nothing new is stored for this.</b> Every row already carries its kind, its provider, its
/// model and its price; what threw the breakdown away was the query, which summed and forgot. So
/// this is the same shape the reset was — a query that keeps something it used to discard.
/// </para>
/// </summary>
/// <param name="Model">
/// The model that answered, or the provider that spoke. Empty on a row written before either was
/// recorded, which groups under the provider alone rather than being dropped.
/// </param>
/// <param name="Priced">
/// False when any row in this group had no rate behind it. Carried per group rather than only for
/// the window, because *"part of it unpriced"* over a whole window does not say which part — and
/// naming the model d47 has no rate for is the difference between a disclaimer and something the
/// Commander can act on.
/// </param>
public sealed record SpendShare(
    SpendKind Kind,
    string Provider,
    string Model,
    decimal Dollars,
    long Characters,
    int Charges,
    bool Priced)
{
    /// <summary>What to call it: the model where there is one, and the provider where there is not.</summary>
    public string Name => string.IsNullOrWhiteSpace(Model) ? Provider : Model;
}

/// <summary>What a window came to, and whether the figure is the whole of it.</summary>
/// <param name="Complete">
/// False when any row in the window had no price behind it. The figure is then a floor rather
/// than a total, and the dialog has to say so — a number presented as authoritative while
/// covering part of the cost is worse than no number.
/// </param>
public sealed record SpendTotals(
    decimal ModelDollars,
    decimal VoiceDollars,
    int Turns,
    long Characters,
    bool Complete)
{
    public static readonly SpendTotals Nothing = new(0m, 0m, 0, 0, Complete: true);

    /// <summary>
    /// What each model and each voice provider came to inside this window, most expensive first
    /// (<a href="https://github.com/dseelinger/d47/issues/226">#226</a>).
    /// <para>
    /// <b>An init property rather than a sixth positional parameter</b>, so every existing caller
    /// and every test that builds a total by hand keeps compiling and reads the same. Empty is a
    /// window with nothing in it, which is what <see cref="Nothing"/> is.
    /// </para>
    /// <para>
    /// <b>Ordered by spend, because alphabetical buries the answer.</b> The column exists to say
    /// where the money went, and the model that cost the most is the one that says it.
    /// </para>
    /// </summary>
    public IReadOnlyList<SpendShare> Shares { get; init; } = [];

    public decimal Dollars => ModelDollars + VoiceDollars;

    public bool Any => Turns > 0 || Characters > 0;
}

/// <summary>
/// Every charge d47 has made, kept between runs so "what has this cost this month" has an answer
/// (docs/plans/change-requests.md item 2).
/// <para>
/// <b>Nothing used to survive the process.</b> <see cref="SpendTracker"/> is a list in memory and
/// <see cref="Audio.SpeechSpend"/> is a dictionary beside it; both start empty at every launch, so
/// the only honest total was "this session". Four running windows cannot be computed from state
/// that forgets.
/// </para>
/// <para>
/// <b>One line of JSON per charge, appended.</b> A ledger is append-only by nature and the format
/// follows: a crash mid-write costs the last line rather than the file, a line that will not parse
/// is skipped rather than taking its neighbours with it, and adding a field later leaves every
/// existing row readable. Rewriting a whole document per turn would put the entire history at risk
/// on each one.
/// </para>
/// <para>
/// <b>Both kinds go in the same file.</b> Ledgering the model alone would leave the week and month
/// figures looking authoritative while covering half the cost, which is the specific failure this
/// was asked to avoid.
/// </para>
/// </summary>
public sealed class SpendLedger
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _path;
    private readonly IWallClock _clock;
    private readonly ILogger _logger;
    private readonly List<SpendEntry> _entries;
    private readonly Lock _lock = new();

    /// <summary>
    /// Whether the file's last line is unterminated, which is what a process killed mid-append
    /// leaves behind. Appending straight onto it would splice the new row into the broken one and
    /// lose both — turning one damaged row into two, on every launch after the first.
    /// </summary>
    private bool _danglingLine;

    /// <summary>
    /// Reads the history in. Synchronous and at startup, because the first turn can be charged
    /// before anything has asked for a total, and a ledger that answers from a half-loaded file
    /// reports a month that is missing its beginning.
    /// </summary>
    public SpendLedger(string path, IWallClock clock, ILogger logger)
    {
        _path = path;
        _clock = clock;
        _logger = logger;
        _entries = Read(path, logger);
        _danglingLine = EndsMidLine(path);
    }

    /// <summary>
    /// Whether the file ends without a line terminator. Read as bytes rather than as text: the
    /// question is about the last byte, and decoding the whole file to answer it would be work
    /// proportional to the history on every launch.
    /// </summary>
    private static bool EndsMidLine(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            using var stream = File.OpenRead(path);

            if (stream.Length == 0)
            {
                return false;
            }

            stream.Seek(-1, SeekOrigin.End);
            var last = stream.ReadByte();

            return last is not ((byte)'\n' or (byte)'\r');
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Assume the worst and start on a fresh line. A spurious blank line is skipped on
            // the way back in; a spliced one is two rows lost.
            return true;
        }
    }

    /// <summary>Every charge, oldest first.</summary>
    public IReadOnlyList<SpendEntry> Entries
    {
        get
        {
            lock (_lock)
            {
                return [.. _entries];
            }
        }
    }

    /// <summary>
    /// Records a charge, stamping it with the current instant.
    /// <para>
    /// A failed write is logged and dropped rather than thrown. This is bookkeeping beside the
    /// answer the Commander asked for, and losing a row costs a slightly low total; letting it
    /// escape would cost the turn.
    /// </para>
    /// </summary>
    public void Append(SpendEntry entry)
    {
        var stamped = entry with { At = entry.At == default ? _clock.UtcNow : entry.At };

        lock (_lock)
        {
            _entries.Add(stamped);

            try
            {
                var lead = _danglingLine ? Environment.NewLine : string.Empty;

                File.AppendAllText(
                    _path,
                    lead + JsonSerializer.Serialize(stamped, Json) + Environment.NewLine);

                _danglingLine = false;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogDebug(ex, "Could not append to the spend ledger at {Path}", _path);
            }
        }
    }

    /// <summary>
    /// Stops counting everything charged inside a window
    /// (<a href="https://github.com/dseelinger/d47/issues/197">#197</a>).
    /// <para>
    /// <b>Appended, not deleted.</b> See <see cref="SpendEntry.ResetFrom"/> for why, and note what
    /// falls out of it for free: because every period figure is a query rather than a running
    /// counter, a charge that stops counting leaves <em>every</em> window that contained it at
    /// once. There are no per-period totals to keep in step, which is the rule the ask states
    /// already being true.
    /// </para>
    /// <para>
    /// The windows do not nest, and that is correct rather than a gap. <c>Today ⊆ This week ⊆ Last
    /// 7 days ⊆ Last 30 days</c> lines up, but resetting <c>This month</c> on the 3rd drops three
    /// days and leaves the rest of <c>Last 30 days</c> standing. It is set semantics, not a tree.
    /// </para>
    /// </summary>
    /// <returns>What the window held, so the caller can say what was cleared.</returns>
    public SpendTotals Reset(SpendPeriod window)
    {
        var cleared = Total(window);

        Append(new SpendEntry
        {
            At = _clock.UtcNow,
            ResetFrom = window.From,
            ResetWindow = window.Name,

            // **Priced, though it prices nothing**, so a build that predates reset marks reads
            // this row as a settled zero-dollar charge rather than as an unpriced one — which
            // would turn every total covering it into "at least $X, part of it unpriced". A
            // Commander who resets on a local build and then goes back to a release with
            // `get-ver latest` is the path this is for. Such a build counts one extra turn per
            // mark and cannot honour it; that is the whole of the cost, and it is bounded.
            Priced = true,
        });

        return cleared;
    }

    /// <summary>What was charged inside a window, less anything a reset has stopped counting.</summary>
    public SpendTotals Total(SpendPeriod window)
    {
        lock (_lock)
        {
            var marks = _entries.Where(entry => entry.IsReset).ToList();

            // A mark is not a charge, so it is out of the count and the sum before anything else
            // asks a question about it — including its own window's.
            var inside = _entries
                .Where(entry => !entry.IsReset && window.Holds(entry.At))
                .Where(entry => !marks.Any(mark => mark.Covers(entry)))
                .ToList();

            if (inside.Count == 0)
            {
                return SpendTotals.Nothing;
            }

            return new SpendTotals(
                inside.Where(e => e.Kind == SpendKind.Model).Sum(e => e.Dollars),
                inside.Where(e => e.Kind == SpendKind.Voice).Sum(e => e.Dollars),
                inside.Count(e => e.Kind == SpendKind.Model),
                inside.Sum(e => e.Characters),
                inside.All(e => e.Priced))
            {
                // **From `inside` and from nothing else** (#226). A second pass over `_entries`
                // would be one line shorter and would include the charges a reset dropped, so the
                // breakdown would disagree with the figure standing beside it. A breakdown that
                // does not add up to its own total teaches a Commander to distrust both, which is
                // the worst thing this column can do.
                Shares = [.. inside
                    .GroupBy(entry => (entry.Kind, entry.ProviderId, entry.Model))
                    .Select(group => new SpendShare(
                        group.Key.Kind,
                        group.Key.ProviderId,
                        group.Key.Model,
                        group.Sum(entry => entry.Dollars),
                        group.Sum(entry => entry.Characters),
                        group.Count(),
                        group.All(entry => entry.Priced)))
                    .OrderByDescending(share => share.Dollars)
                    .ThenBy(share => share.Name, StringComparer.OrdinalIgnoreCase)],
            };
        }
    }

    /// <summary>Every window the dialog reports, against the clock this ledger was given.</summary>
    public IReadOnlyList<(SpendPeriod Period, SpendTotals Totals)> Summary(TimeZoneInfo zone) =>
        [.. Immediate(zone), .. Windows(zone)];

    /// <summary>
    /// The windows that read beside the turn and the session — today, and nothing else
    /// (<a href="https://github.com/dseelinger/d47/issues/227">#227</a>).
    /// <para>
    /// <b>Two members rather than the dialog slicing <see cref="Summary"/>.</b> Which windows
    /// belong together is <see cref="SpendPeriods"/>' answer, and taking the first entry off a
    /// list would make it positional — a re-order there would move a window into the wrong group
    /// with nothing to notice. It also keeps the clock in here: the dialog reads none.
    /// </para>
    /// </summary>
    public IReadOnlyList<(SpendPeriod Period, SpendTotals Totals)> Immediate(TimeZoneInfo zone) =>
        [.. SpendPeriods.Immediate(_clock.UtcNow, zone).Select(period => (period, Total(period)))];

    /// <summary>The four a Commander compares, each calendar window beside its rolling twin (#227).</summary>
    public IReadOnlyList<(SpendPeriod Period, SpendTotals Totals)> Windows(TimeZoneInfo zone) =>
        [.. SpendPeriods.Windows(_clock.UtcNow, zone).Select(period => (period, Total(period)))];

    /// <summary>
    /// The windows the dialog offers to reset, against the same clock (#197). Asked of the ledger
    /// rather than worked out in the window, so the spans a Commander is offered are pinned to the
    /// same instant the figures beside them are — and so nothing in the UI reads a clock.
    /// </summary>
    public IReadOnlyList<SpendPeriod> Resettable(TimeZoneInfo zone, DateTimeOffset launchedAt) =>
        SpendPeriods.Resettable(_clock.UtcNow, zone, launchedAt);

    /// <summary>
    /// Loads what is on disk, skipping anything that will not parse.
    /// <para>
    /// A bad line is dropped and counted rather than failing the load. The commonest way this
    /// file breaks is a half-written last line from a process that was killed mid-append, and
    /// refusing to start — or refusing to bill — over one truncated row would be a worse
    /// failure than under-reporting by one turn.
    /// </para>
    /// </summary>
    private static List<SpendEntry> Read(string path, ILogger logger)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        var entries = new List<SpendEntry>();
        var skipped = 0;

        try
        {
            foreach (var line in File.ReadLines(path))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    if (JsonSerializer.Deserialize<SpendEntry>(line, Json) is { } entry)
                    {
                        entries.Add(entry);
                    }
                }
                catch (JsonException)
                {
                    skipped++;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogInformation(ex, "Spend ledger at {Path} could not be read; totals start from empty", path);
            return entries;
        }

        if (skipped > 0)
        {
            logger.LogInformation(
                "Skipped {Count} unreadable row(s) in the spend ledger at {Path}",
                skipped.ToString(CultureInfo.InvariantCulture),
                path);
        }

        // Sorted, because the windows are ranges rather than a suffix and a file that was
        // appended to by two processes is not guaranteed to be in order.
        entries.Sort((a, b) => a.At.CompareTo(b.At));
        return entries;
    }
}
