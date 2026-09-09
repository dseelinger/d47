using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace D47.Core.Conversation;

/// <summary>What kind of spend a row is.</summary>
public enum SpendKind
{
    Model,
    Voice,
}

/// <summary>One charge, with the instant it happened.</summary>
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

    /// <summary>Characters sent to a speech provider.</summary>
    public long Characters { get; init; }

    /// <summary>
    /// How much audio a speech charge produced, for a provider billed by the length of it rather than
    /// by the characters handed over (#63).
    /// </summary>
    public double? AudioSeconds { get; init; }

    /// <summary>On a reset mark, the instant its window began.</summary>
    public DateTimeOffset? ResetFrom { get; init; }

    /// <summary>What the Commander called the window they reset, for the audit trail.</summary>
    public string? ResetWindow { get; init; }

    /// <summary>Whether this row is a reset mark rather than a charge.</summary>
    [JsonIgnore]
    public bool IsReset => ResetFrom is not null;

    /// <summary>Whether this mark covers a charge, and so stops it counting anywhere.</summary>
    public bool Covers(SpendEntry charge) =>
        ResetFrom is { } from && charge.At >= from && charge.At <= At;
}

/// <summary>What one model or one voice provider cost inside a window (#226).</summary>
/// <param name="Model">The model that answered, or the provider that spoke.</param>
/// <param name="Priced">False when any row in this group had no rate behind it.</param>
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
/// <param name="Complete">False when any row in the window had no price behind it.</param>
public sealed record SpendTotals(
    decimal ModelDollars,
    decimal VoiceDollars,
    int Turns,
    long Characters,
    bool Complete)
{
    public static readonly SpendTotals Nothing = new(0m, 0m, 0, 0, Complete: true);

    /// <summary>
    /// What each model and each voice provider came to inside this window, most expensive first (#226).
    /// </summary>
    public IReadOnlyList<SpendShare> Shares { get; init; } = [];

    public decimal Dollars => ModelDollars + VoiceDollars;

    public bool Any => Turns > 0 || Characters > 0;
}

/// <summary>
/// Every charge d47 has made, kept between runs so "what has this cost this month" has an answer
/// (docs/plans/change-requests.md item 2).
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
    /// Whether the file's last line is unterminated, which is what a process killed mid-append leaves
    /// behind.
    /// </summary>
    private bool _danglingLine;

    /// <summary>Reads the history in.</summary>
    public SpendLedger(string path, IWallClock clock, ILogger logger)
    {
        _path = path;
        _clock = clock;
        _logger = logger;
        _entries = Read(path, logger);
        _danglingLine = EndsMidLine(path);
    }

    /// <summary>Whether the file ends without a line terminator.</summary>
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
            // Assume the worst and start on a fresh line.
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

    /// <summary>Records a charge, stamping it with the current instant.</summary>
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

    /// <summary>Stops counting everything charged inside a window (#197).</summary>
    /// <returns>What the window held, so the caller can say what was cleared.</returns>
    public SpendTotals Reset(SpendPeriod window)
    {
        var cleared = Total(window);

        Append(new SpendEntry
        {
            At = _clock.UtcNow,
            ResetFrom = window.From,
            ResetWindow = window.Name,

            // **Priced, though it prices nothing**, so a build that predates reset marks reads this row as a
            // settled zero-dollar charge rather than as an unpriced one — which would turn every total
            // covering it into "at least $X, part of it unpriced".
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

            // A mark is not a charge, so it is out of the count and the sum before anything else asks a
            // question about it — including its own window's.
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
                // **From `inside` and from nothing else** (#226).
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

    /// <summary>The windows that read beside the turn and the session — today, and nothing else (#227).</summary>
    public IReadOnlyList<(SpendPeriod Period, SpendTotals Totals)> Immediate(TimeZoneInfo zone) =>
        [.. SpendPeriods.Immediate(_clock.UtcNow, zone).Select(period => (period, Total(period)))];

    /// <summary>The four a Commander compares, each calendar window beside its rolling twin (#227).</summary>
    public IReadOnlyList<(SpendPeriod Period, SpendTotals Totals)> Windows(TimeZoneInfo zone) =>
        [.. SpendPeriods.Windows(_clock.UtcNow, zone).Select(period => (period, Total(period)))];

    /// <summary>The windows the dialog offers to reset, against the same clock (#197).</summary>
    public IReadOnlyList<SpendPeriod> Resettable(TimeZoneInfo zone, DateTimeOffset launchedAt) =>
        SpendPeriods.Resettable(_clock.UtcNow, zone, launchedAt);

    /// <summary>Loads what is on disk, skipping anything that will not parse.</summary>
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

        // Sorted, because the windows are ranges rather than a suffix and a file that was appended to by two
        // processes is not guaranteed to be in order.
        entries.Sort((a, b) => a.At.CompareTo(b.At));
        return entries;
    }
}
