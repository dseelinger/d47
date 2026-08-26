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

    /// <summary>What was charged inside a window.</summary>
    public SpendTotals Total(SpendPeriod window)
    {
        lock (_lock)
        {
            var inside = _entries.Where(entry => window.Holds(entry.At)).ToList();

            if (inside.Count == 0)
            {
                return SpendTotals.Nothing;
            }

            return new SpendTotals(
                inside.Where(e => e.Kind == SpendKind.Model).Sum(e => e.Dollars),
                inside.Where(e => e.Kind == SpendKind.Voice).Sum(e => e.Dollars),
                inside.Count(e => e.Kind == SpendKind.Model),
                inside.Sum(e => e.Characters),
                inside.All(e => e.Priced));
        }
    }

    /// <summary>The four windows the dialog reports, against the clock this ledger was given.</summary>
    public IReadOnlyList<(SpendPeriod Period, SpendTotals Totals)> Summary(TimeZoneInfo zone) =>
        [.. SpendPeriods.All(_clock.UtcNow, zone).Select(period => (period, Total(period)))];

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
