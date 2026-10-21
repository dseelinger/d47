using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Globalization;
using D47.Core.Knowledge;

namespace D47.Core.Journal;

/// <summary>One <c>CodexEntry</c> event. <c>RegionId</c> is the codex region number, null when <c>Region</c> does not parse. <c>SubCategorySymbol</c> is the raw symbol, which does not change with the game's language.</summary>
public sealed record CodexLogEntry(
    DateTimeOffset At,
    long EntryId,
    string? Name,
    string? Category,
    string? SubCategory,
    int? RegionId,
    string? System,
    long? SystemAddress,
    int? BodyId,
    double? Latitude,
    double? Longitude,
    bool IsNew,
    string? SubCategorySymbol = null);

/// <summary>Entries logged in one region.</summary>
public sealed record CodexRegionTotal(int? RegionId, int Count);

/// <summary>Entries logged in one category.</summary>
public sealed record CodexCategoryTotal(string? Category, int Count);

/// <summary>The entries since a time, optionally in one region. <c>Logged</c> counts distinct (entry, region) pairs.</summary>
public sealed record CodexSlice(
    DateTimeOffset? From,
    int? RegionId,
    int Entries,
    int New,
    int Logged,
    IReadOnlyList<CodexRegionTotal> ByRegion,
    IReadOnlyList<CodexCategoryTotal> ByCategory);

/// <summary>Every codex entry the journals show for one Commander.</summary>
public sealed record CodexLedger
{
    public static readonly CodexLedger None = new();

    /// <summary>The event names <see cref="Apply"/> reads, for callers that filter lines before parsing.</summary>
    public static readonly FrozenSet<string> Reads = FrozenSet.ToFrozenSet<string>(
    [
        "CodexEntry", "Commander", "Fileheader", "LoadGame", "MultiSellExplorationData", "NewCommander",
        "SellExplorationData",
    ], StringComparer.Ordinal);

    private const string RegionPrefix = "$Codex_RegionName_";

    /// <summary>The entries, oldest first.</summary>
    public IReadOnlyList<CodexLogEntry> Entries => Held;

    /// <summary>The newest exploration data sale, or null when none is on record.</summary>
    public DateTimeOffset? TripStart { get; init; }

    /// <summary>The time of the newest <c>LoadGame</c>.</summary>
    public DateTimeOffset? SessionStart { get; init; }

    /// <summary>The first event this record folded; entries before it are not counted.</summary>
    public DateTimeOffset? OnRecordSince { get; init; }

    /// <summary>True once the walk over earlier journals has been merged in.</summary>
    public bool IsWhole { get; init; }

    public bool IsKnown => !Held.IsEmpty || OnRecordSince is not null;

    /// <summary>The distinct (entry, region) pairs the record holds.</summary>
    public IReadOnlyCollection<(long EntryId, int? RegionId)> Logged =>
        [.. Held.Select(entry => (entry.EntryId, entry.RegionId)).Distinct()];

    private ImmutableList<CodexLogEntry> Held { get; init; } = [];

    private bool Legacy { get; init; }

    /// <summary>The codex region number in a <c>Region</c> value such as <c>$Codex_RegionName_18;</c>, or null.</summary>
    public static int? RegionIdOf(string? region)
    {
        if (region is null || !region.StartsWith(RegionPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var digits = region.AsSpan(RegionPrefix.Length).TrimEnd(';');

        return int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : null;
    }

    /// <summary>The entries at or after <paramref name="from"/> (all for null), optionally in one region.</summary>
    public CodexSlice Since(DateTimeOffset? from, int? regionId)
    {
        var items = Held
            .Where(entry => (from is null || entry.At >= from) && (regionId is null || entry.RegionId == regionId))
            .ToList();

        return new CodexSlice(
            from,
            regionId,
            items.Count,
            items.Count(entry => entry.IsNew),
            items.Select(entry => (entry.EntryId, entry.RegionId)).Distinct().Count(),
            [
                .. items
                    .GroupBy(entry => entry.RegionId)
                    .OrderBy(group => group.Key)
                    .Select(group => new CodexRegionTotal(group.Key, group.Count())),
            ],
            [
                .. items
                    .GroupBy(entry => entry.Category)
                    .OrderBy(group => group.Key, StringComparer.Ordinal)
                    .Select(group => new CodexCategoryTotal(group.Key, group.Count())),
            ]);
    }

    public CodexLedger Apply(JournalEvent journalEvent)
    {
        ArgumentNullException.ThrowIfNull(journalEvent);

        switch (journalEvent.Kind)
        {
            case "NewCommander":
                return None with { IsWhole = IsWhole, SessionStart = SessionStart };

            case "Fileheader":
                return this with { Legacy = CreditLedger.IsLegacy(journalEvent) };
        }

        if (Legacy)
        {
            return this;
        }

        var result = this with { OnRecordSince = OnRecordSince ?? journalEvent.Timestamp };

        return journalEvent.Kind switch
        {
            "LoadGame" => result with { SessionStart = journalEvent.Timestamp },
            "SellExplorationData" or "MultiSellExplorationData" => result with { TripStart = journalEvent.Timestamp },
            "CodexEntry" when journalEvent.Long("EntryID") is { } id => result with { Held = Held.Add(Entry(journalEvent, id)) },
            _ => result,
        };
    }

    /// <summary>
    /// This, recovered from older journals, with what the live journal has folded since laid over it, marked
    /// whole. An entry in both is counted once.
    /// </summary>
    public CodexLedger With(CodexLedger live)
    {
        ArgumentNullException.ThrowIfNull(live);

        var last = Held.Count > 0 ? Held[^1].At : (DateTimeOffset?)null;
        var atLast = last is null ? 0 : Held.Count(entry => entry.At == last);
        var seenAtLast = 0;

        var added = live.Held.Where(entry =>
            last is not { } at || entry.At > at || entry.At == at && ++seenAtLast > atLast);

        return new CodexLedger
        {
            Held = Held.AddRange(added),
            TripStart = Later(TripStart, live.TripStart),
            SessionStart = Later(SessionStart, live.SessionStart),
            OnRecordSince = OnRecordSince is { } first && live.OnRecordSince is { } theirs
                ? (first < theirs ? first : theirs)
                : OnRecordSince ?? live.OnRecordSince,
            IsWhole = true,
            Legacy = live.Legacy,
        };
    }

    private static DateTimeOffset? Later(DateTimeOffset? a, DateTimeOffset? b) =>
        a is { } x && b is { } y ? (x > y ? x : y) : a ?? b;

    private static CodexLogEntry Entry(JournalEvent e, long id) => new(
        e.Timestamp,
        id,
        e.Named("Name"),
        e.Named("Category"),
        e.Named("SubCategory"),
        RegionIdOf(e.String("Region")),
        e.String("System"),
        e.Long("SystemAddress"),
        e.Int("BodyID"),
        e.Double("Latitude"),
        e.Double("Longitude"),
        e.Bool("IsNewEntry"),
        e.String("SubCategory"));
}
