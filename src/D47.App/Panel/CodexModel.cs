using System.Globalization;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>The periods the Codex pages offer.</summary>
public enum CodexPeriod
{
    Trip,
    Session,
    Everything,
}

/// <summary>One region of the regions table. <c>Recorded</c> and <c>Known</c> are the Discoveries figures, null for a region D47 cannot name; <c>Other</c> marks a region with no entries in the period.</summary>
public sealed record CodexRegionRow(
    int? RegionId,
    string Name,
    bool Named,
    int Entries,
    int New,
    int Logged,
    string Detail,
    int? Recorded = null,
    int? Known = null,
    bool Other = false);

/// <summary>What the Codex page draws for one period, history state and search.</summary>
public sealed record CodexRootView(
    bool Running,
    bool Failed,
    DateTimeOffset? OnRecordSince,
    bool Whole,
    string Line,
    int Entries,
    int New,
    int Logged,
    int Regions,
    IReadOnlyList<CodexRegionRow> Rows,
    string? Empty,
    IReadOnlyList<CodexRegionRow> Others,
    int OtherRegions,
    string OtherLine);

/// <summary>One entry of a region's table. <c>Origin</c> says why no earlier entry is shown for an entry already logged.</summary>
public sealed record CodexEntryRow(
    long EntryId,
    string Name,
    string? System,
    bool IsHere,
    string? Body,
    string? Position,
    DateTimeOffset At,
    bool IsNew,
    DateTimeOffset? First,
    string? Origin)
{
    /// <summary>The body and the position as one line: "A 4 c · 12.4512, −108.2231".</summary>
    public string Place => string.Join(" · ", new[] { Body, Position }.Where(part => !string.IsNullOrWhiteSpace(part)));
}

/// <summary>The entries of one category and subcategory, newest first.</summary>
public sealed record CodexGroup(string Label, IReadOnlyList<CodexEntryRow> Rows)
{
    public int Count => Rows.Count;

    public bool Equals(CodexGroup? other) => other is not null && Label == other.Label && Rows.SequenceEqual(other.Rows);

    public override int GetHashCode() => HashCode.Combine(Label, Rows.Count);
}

/// <summary>What a region's page draws for one period, history state and search.</summary>
public sealed record CodexRegionView(
    bool Running,
    bool Failed,
    DateTimeOffset? OnRecordSince,
    bool Whole,
    string Line,
    int Entries,
    int New,
    int Logged,
    int Subcategories,
    int Shown,
    IReadOnlyList<CodexGroup> Groups,
    string? Empty);

/// <summary>One codex entry the table lists for the region, with the systems that reported it there.</summary>
public sealed record CodexMissingRow(long EntryId, string Name, string Category, string SubCategory, int Systems, bool OnRecord, string? PlaceholderFile = null);

/// <summary>Which of a region's known entries the Discoveries page lists.</summary>
public enum CodexStatusFilter
{
    Missing,
    OnRecord,
    All,
}

/// <summary>The Discoveries page's filters; a null category or subcategory is All.</summary>
public sealed record CodexDiscoveryFilter(CodexStatusFilter Status = CodexStatusFilter.Missing, string? Category = null, string? SubCategory = null)
{
    public bool IsDefault => this == new CodexDiscoveryFilter();
}

/// <summary>What a region's Discoveries page draws for one ledger, table, history state, filter and search.</summary>
public sealed record CodexDiscoveriesView(
    bool Running,
    bool Failed,
    DateTimeOffset? OnRecordSince,
    int Known,
    int Logged,
    int Missing,
    int Subcategories,
    int Uncovered,
    IReadOnlyList<CodexMissingRow> Rows,
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> SubCategories,
    string? Empty)
{
    public int Shown => Rows.Count;
}

/// <summary>What one entry's page draws: the entry's facts and each time it was logged in the region, newest first.</summary>
public sealed record CodexEntryView(
    long EntryId,
    string Name,
    string? Category,
    string? SubCategory,
    string? PlaceholderFile,
    IReadOnlyList<CodexEntryRow> Loggings);

/// <summary>The rows behind Commander › Ledgers › Codex, built from a <see cref="CodexLedger"/> (#1290).</summary>
public static class CodexModel
{
    public const string NoRegionName = "A region D47 cannot name";

    public const string RunningText = "D47 is reading your journals for past codex entries. The ledger appears here when it finishes.";

    public const string FailedLabel = "History not finished";

    public const string NoRegionMatchText = "No codex entry, system or region in this period matches that search.";

    public const string NoEntryMatchText = "No codex entry or system in this region and period matches that search.";

    public const string OtherRegionsHead = "Other regions";

    public const string OtherRegionsOpenLine = "Open one to see what is left to discover.";

    public const string Placeholder = "Search entry, system or region";

    public const string DiscoveriesPlaceholder = "Search entry";

    public const string DiscoveriesCrumbWord = "Discoveries";

    public const string DiscoveriesRowLine = "The entries known in this region that are not on your record. Whole record, not this trip.";

    public const string DiscoveriesNoRegionLine = "D47 has no region number for these entries, so it cannot list what is left to discover.";

    public const string DiscoveriesLine = "Every entry the codex table lists for this region, against everything on your record.";

    public static string DiscoveriesGroupLine(CodexStatusFilter status) => status switch
    {
        CodexStatusFilter.Missing => "Known in this region and not on your record, most reported first.",
        CodexStatusFilter.OnRecord => "Known in this region and on your record, most reported first.",
        _ => "Every entry known in this region, most reported first.",
    };

    public const string NoMissingMatchText = "No codex entry in this region matches those filters and that search.";

    public const string NoPictureText = "No picture of this entry yet.";

    public const string StockOffText = "No picture of this entry. Stock codex pictures are off.";

    public const string FetchingText = "Fetching the picture.";

    public const string ChooseWord = "Choose a picture";

    public const string CropWord = "Crop";

    public const string NoCapturesText = "No captures yet. Say \"take a picture\" in game to take one.";

    public const string TakenWithHead = "Taken with this entry";

    public const string OtherCapturesHead = "Other captures";

    public const string UnknownSystemText = "Unknown system";

    public const string CaptureGoneText = "The capture could not be read.";

    public const string SmallestSquareText = "Smallest square";

    /// <summary>The crop readout: the square's side, its position and the size it is saved at.</summary>
    public static string CropReadout(CropSquare square) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"SQUARE {square.Side} × {square.Side} · AT {square.X}, {square.Y} · SAVED AT {CropSquare.SavedSide} × {CropSquare.SavedSide}");

    public const string NotHereText = "Not on your record in this region.";

    public const string LoggedHereHead = "Logged in this region";

    private const string Minus = "−";

    private const string Other = "Other";

    private static readonly string[] PeriodNames = ["This trip", "This session", "Everything"];

    public static IReadOnlyList<string> Periods => PeriodNames;

    public static string PeriodName(CodexPeriod period) => PeriodNames[(int)period];

    /// <summary>The start of <paramref name="period"/>; null for everything and for a trip with no sale on record.</summary>
    public static DateTimeOffset? Start(CodexLedger ledger, CodexPeriod period) => period switch
    {
        CodexPeriod.Trip => ledger.TripStart,
        CodexPeriod.Session => ledger.SessionStart,
        _ => null,
    };

    public static string FailedText(DateTimeOffset? since) =>
        "D47 couldn't finish reading your older journals, so this ledger starts when D47 started"
        + (since is { } at ? $", at {PageKit.Clock(at)} UTC today" : string.Empty)
        + ". Codex entries from here on are still recorded.";

    /// <summary>The line under the group head.</summary>
    public static string Line(CodexLedger ledger, CodexPeriod period, DateTimeOffset now)
    {
        var since = ledger.OnRecordSince is { } first ? "since " + Date(first) : "on record";

        switch (period)
        {
            case CodexPeriod.Trip when ledger.TripStart is { } sale:
                return $"Since you sold exploration data on {Day(sale)} at {PageKit.Clock(sale)} UTC.";
            case CodexPeriod.Trip:
                return ledger.OnRecordSince is { } start
                    ? $"No sale of exploration data on record, so this trip is everything since {Date(start)}."
                    : "No sale of exploration data on record, so this trip is everything on record.";
            case CodexPeriod.Session when ledger.SessionStart is { } load:
                return load.UtcDateTime.Date == now.UtcDateTime.Date
                    ? $"Since you loaded the game at {PageKit.Clock(load)} UTC today."
                    : $"Since you loaded the game on {Day(load)} at {PageKit.Clock(load)} UTC.";
            case CodexPeriod.Session:
                return $"No game load on record, so this session is everything {since}.";
            default:
                return ledger.OnRecordSince is { } all ? $"Everything on record, since {Date(all)}." : "Everything on record.";
        }
    }

    /// <summary>"No codex entries since you sold exploration data on 12 October." and the like; <paramref name="region"/> is the region's name on its page.</summary>
    public static string EmptyText(CodexLedger ledger, CodexPeriod period, string? region = null)
    {
        var where = region is null ? string.Empty : " in " + region;

        return period switch
        {
            CodexPeriod.Trip when ledger.TripStart is { } sale => $"No codex entries{where} since you sold exploration data on {Day(sale)}.",
            CodexPeriod.Session => $"No codex entries{where} this session.",
            _ => $"No codex entries{where} on record.",
        };
    }

    /// <summary>"3 regions", "1 region".</summary>
    public static string RegionCount(int count) => $"{Number(count)} {(count == 1 ? "region" : "regions")}";

    /// <summary>"142 entries", "1 entry".</summary>
    public static string EntryCount(int count) => $"{Number(count)} {(count == 1 ? "entry" : "entries")}";

    public static string Number(long count) => count.ToString("N0", CultureInfo.InvariantCulture);

    public static string Date(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>"2026-10-17 18:21".</summary>
    public static string DateTime(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    /// <summary>The date of the record's start, or its date and time while the history walk has not finished.</summary>
    public static string OnRecord(DateTimeOffset since, bool failed) => failed ? DateTime(since) : Date(since);

    public static string RegionName(int? id) => id is { } known && GalacticRegions.NameOf(known) is { } name ? name : NoRegionName;

    /// <summary>"Organic structures 31 · Stars 9 · Geology 4": the three largest subcategories.</summary>
    public static string Subcategories(IEnumerable<CodexLogEntry> entries) =>
        string.Join(
            " · ",
            entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.SubCategory))
                .GroupBy(entry => entry.SubCategory!)
                .Select(group => (Name: group.Key, Count: group.Count()))
                .OrderByDescending(item => item.Count)
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .Select(item => $"{item.Name} {Number(item.Count)}"));

    /// <summary>The body's name without its system's name; null for no name.</summary>
    public static string? BodyOf(string? system, string? body) =>
        body is not null && system is not null && body.StartsWith(system + " ", StringComparison.OrdinalIgnoreCase)
            ? body[(system.Length + 1)..]
            : body;

    /// <summary>"12.4512, −108.2231"; null without both coordinates.</summary>
    public static string? PositionOf(double? latitude, double? longitude) =>
        latitude is { } lat && longitude is { } lon ? $"{Degrees(lat)}, {Degrees(lon)}" : null;

    /// <summary>"A 4 c · 12.4512, −108.2231": the body, then the position.</summary>
    public static string Place(string? system, string? body, double? latitude, double? longitude) =>
        new CodexEntryRow(0, string.Empty, system, false, BodyOf(system, body), PositionOf(latitude, longitude), default, false, null, null).Place;

    /// <summary>"Nothing logged this trip. Open one to see what is left to discover."</summary>
    public static string OtherRegionsLine(CodexPeriod period) =>
        period switch
        {
            CodexPeriod.Trip => "Nothing logged this trip.",
            CodexPeriod.Session => "Nothing logged this session.",
            _ => "Nothing on record.",
        } + " " + OtherRegionsOpenLine;

    /// <summary>The regions with entries in the period, most entries first, kept by the search; then the codex table's other regions.</summary>
    public static CodexRootView Regions(
        CodexLedger ledger,
        CodexPeriod period,
        DateTimeOffset now,
        HistoryState history,
        string? query = null,
        CodexTable? table = null)
    {
        table ??= CodexCatalogue.Table;
        var slice = ledger.Since(Start(ledger, period), null);
        var from = Start(ledger, period);
        var items = ledger.Entries.Where(entry => from is null || entry.At >= from).ToList();
        var text = query?.Trim();
        var rows = new List<CodexRegionRow>();

        foreach (var group in items.GroupBy(entry => entry.RegionId))
        {
            var name = RegionName(group.Key);
            var byName = string.IsNullOrEmpty(text) || name.Contains(text, StringComparison.OrdinalIgnoreCase);
            var hits = byName ? 0 : group.Count(entry => Matches(entry, text!));

            if (!byName && hits == 0)
            {
                continue;
            }

            rows.Add(new CodexRegionRow(
                group.Key,
                name,
                GalacticRegions.NameOf(group.Key ?? -1) is not null,
                group.Count(),
                group.Count(entry => entry.IsNew),
                group.Select(entry => entry.EntryId).Distinct().Count(),
                byName ? Subcategories(group) : $"{Number(hits)} of {Number(group.Count())} entries match",
                group.Key is { } id && GalacticRegions.NameOf(id) is not null ? CodexDiscoveries.For(ledger, table, id).Logged.Count : null,
                group.Key is { } known && GalacticRegions.NameOf(known) is not null ? table.InRegion(known).Count : null));
        }

        var inPeriod = items.Select(entry => entry.RegionId).ToHashSet();
        var others = new List<CodexRegionRow>();

        foreach (var id in table.Regions.Where(id => !inPeriod.Contains(id)))
        {
            if (GalacticRegions.NameOf(id) is not { } name
                || (!string.IsNullOrEmpty(text) && !name.Contains(text, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var found = CodexDiscoveries.For(ledger, table, id);
            var last = ledger.Entries.Where(entry => entry.RegionId == id).Select(entry => (DateTimeOffset?)entry.At).Max();

            others.Add(new CodexRegionRow(
                id,
                name,
                true,
                0,
                0,
                0,
                last is { } at ? "Last logged " + Date(at) : "Nothing on record",
                found.Logged.Count,
                found.Known.Count,
                true));
        }

        var otherTotal = table.Regions.Count(id => !inPeriod.Contains(id) && GalacticRegions.NameOf(id) is not null);

        return new CodexRootView(
            history is HistoryState.Pending or HistoryState.Running,
            history is HistoryState.Failed or HistoryState.Stopped,
            ledger.OnRecordSince,
            ledger.IsWhole,
            Line(ledger, period, now),
            slice.Entries,
            slice.New,
            slice.Logged,
            slice.ByRegion.Count,
            [.. rows.OrderByDescending(row => row.Entries).ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase)],
            slice.Entries == 0 ? EmptyText(ledger, period) : null,
            [.. others.OrderByDescending(row => row.Recorded).ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase)],
            otherTotal,
            OtherRegionsLine(period));
    }

    /// <summary>The region's entries in the period, grouped by category and subcategory, largest group first and newest entry first within it.</summary>
    public static CodexRegionView Entries(
        CodexLedger ledger,
        CodexPeriod period,
        int? regionId,
        DateTimeOffset now,
        HistoryState history,
        string? currentSystem,
        Func<long, int, string?> bodyName,
        string? query = null)
    {
        var from = Start(ledger, period);
        var slice = ledger.Since(from, regionId);
        var name = RegionName(regionId);
        var text = query?.Trim();
        var all = ledger.Entries;

        var firsts = new Dictionary<(long, int?), int>();

        for (var i = 0; i < all.Count; i++)
        {
            firsts.TryAdd((all[i].EntryId, all[i].RegionId), i);
        }

        var wanted = all
            .Select((entry, index) => (Entry: entry, Index: index))
            .Where(item => item.Entry.RegionId == regionId && (from is null || item.Entry.At >= from))
            .Reverse()
            .OrderByDescending(item => item.Entry.At)
            .ToList();

        var byRegion = string.IsNullOrEmpty(text) || name.Contains(text, StringComparison.OrdinalIgnoreCase);
        var kept = byRegion ? wanted : [.. wanted.Where(item => Matches(item.Entry, text!))];

        var groups = kept
            .GroupBy(item => Label(item.Entry))
            .Select(group => new CodexGroup(
                group.Key,
                [.. group.Select(item => Row(item.Entry, item.Index, all, firsts, currentSystem, bodyName, ledger.IsWhole))]))
            .OrderByDescending(group => group.Count)
            .ThenBy(group => group.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new CodexRegionView(
            history is HistoryState.Pending or HistoryState.Running,
            history is HistoryState.Failed or HistoryState.Stopped,
            ledger.OnRecordSince,
            ledger.IsWhole,
            Line(ledger, period, now),
            slice.Entries,
            slice.New,
            slice.Logged,
            wanted.Select(item => Label(item.Entry)).Distinct().Count(),
            kept.Count,
            groups,
            slice.Entries == 0 ? EmptyText(ledger, period, name) : null);
    }

    /// <summary>"Your picture", "Stock picture", and " · from Outer Orion Spur" for a picture from another region.</summary>
    public static string PictureLine(CodexPictureChoice choice) =>
        (choice.Source == CodexPictureSource.Local ? "Your picture" : "Stock picture")
        + (choice.FromOtherRegion ? " · from " + RegionName(choice.RegionId) : string.Empty);

    /// <summary>The entry's facts and the times it was logged in <paramref name="regionId"/>, newest first.</summary>
    public static CodexEntryView EntryPage(
        CodexLedger ledger,
        CodexTable table,
        long entryId,
        int? regionId,
        string fallbackName,
        string? currentSystem,
        Func<long, int, string?> bodyName)
    {
        var all = ledger.Entries;
        var firsts = new Dictionary<(long, int?), int>();

        for (var i = 0; i < all.Count; i++)
        {
            firsts.TryAdd((all[i].EntryId, all[i].RegionId), i);
        }

        var here = all
            .Select((entry, index) => (Entry: entry, Index: index))
            .Where(item => item.Entry.EntryId == entryId && item.Entry.RegionId == regionId)
            .Reverse()
            .OrderByDescending(item => item.Entry.At)
            .Select(item => Row(item.Entry, item.Index, all, firsts, currentSystem, bodyName, ledger.IsWhole))
            .ToList();

        var logged = all.LastOrDefault(entry => entry.EntryId == entryId);
        var known = table.Entry(entryId);

        return new CodexEntryView(
            entryId,
            logged?.Name ?? known?.Name ?? fallbackName,
            logged?.Category ?? known?.Category,
            logged?.SubCategory ?? known?.SubCategory,
            logged is not null
                ? CodexFamilies.FileOfSymbol(logged.SubCategorySymbol)
                : CodexFamilies.FileOfTableName(known?.SubCategory),
            here);
    }

    /// <summary>"708 missing · 38 of 746 on record": the figures on the region page's Discoveries row.</summary>
    public static string DiscoveriesFigures(CodexDiscoveries found) =>
        $"{Number(found.Missing.Count)} missing · {Number(found.Logged.Count)} of {Number(found.Known.Count)} on record";

    /// <summary>"An entry logged before 2025-03-13 reads as missing." followed by the coverage sentence and the count the table does not list.</summary>
    public static string DiscoveriesNote(DateTimeOffset? since, int uncovered) =>
        (since is { } at ? $"An entry logged before {Date(at)} reads as missing. " : "An entry logged before your record starts reads as missing. ")
        + CodexDiscoveries.Coverage
        + (uncovered > 0 ? $" You logged {Number(uncovered)} more here that the codex table does not list." : string.Empty);

    /// <summary>The region's entries from <paramref name="table"/> that <paramref name="filter"/> and the search keep, most reported first.</summary>
    public static CodexDiscoveriesView Discoveries(
        CodexLedger ledger, CodexTable table, int regionId, HistoryState history, CodexDiscoveryFilter? filter = null, string? query = null)
    {
        filter ??= new CodexDiscoveryFilter();

        var found = CodexDiscoveries.For(ledger, table, regionId);
        var text = query?.Trim();
        var categories = found.Known
            .Select(item => item.Entry.Category)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var subCategories = filter.Category is null
            ? []
            : found.Known
                .Where(item => string.Equals(item.Entry.Category, filter.Category, StringComparison.OrdinalIgnoreCase))
                .Select(item => item.Entry.SubCategory)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToList();

        var rows = found.Known
            .Where(item => filter.Status switch
            {
                CodexStatusFilter.Missing => item.Logged is null,
                CodexStatusFilter.OnRecord => item.Logged is not null,
                _ => true,
            })
            .Where(item => filter.Category is null || string.Equals(item.Entry.Category, filter.Category, StringComparison.OrdinalIgnoreCase))
            .Where(item => filter.SubCategory is null || string.Equals(item.Entry.SubCategory, filter.SubCategory, StringComparison.OrdinalIgnoreCase))
            .Where(item => string.IsNullOrEmpty(text) || item.Entry.Name.Contains(text, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.Systems)
            .ThenBy(item => item.Entry.Name, StringComparer.OrdinalIgnoreCase)
            .Select(item => new CodexMissingRow(
                item.Entry.EntryId,
                item.Entry.Name,
                item.Entry.Category,
                item.Entry.SubCategory,
                item.Systems,
                item.Logged is not null,
                item.Logged is { } logged
                    ? CodexFamilies.FileOfSymbol(logged.SubCategorySymbol)
                    : CodexFamilies.FileOfTableName(item.Entry.SubCategory)))
            .ToList();

        return new CodexDiscoveriesView(
            history is HistoryState.Pending or HistoryState.Running,
            history is HistoryState.Failed or HistoryState.Stopped,
            ledger.OnRecordSince,
            found.Known.Count,
            found.Logged.Count,
            found.Missing.Count,
            found.Missing.Select(item => GroupLabel(item.Entry)).Distinct().Count(),
            found.Uncovered.Count,
            rows,
            categories,
            subCategories,
            found.Known.Count == 0
                ? $"The codex table lists no entries for {RegionName(regionId)}."
                : found.Missing.Count == 0 && filter.Status == CodexStatusFilter.Missing ? "Every entry the codex table lists for this region is on your record." : null);
    }

    /// <summary>The Discoveries table's columns; the default order is most reported first.</summary>
    public static IReadOnlyList<SortColumn<CodexMissingRow>> MissingHeads() =>
    [
        SortColumn<CodexMissingRow>.Plain(string.Empty),
        SortColumn<CodexMissingRow>.Text("Entry", row => row.Name) with { Second = "Codex name" },
        SortColumn<CodexMissingRow>.Text("Category", row => row.Category, row => row.SubCategory) with { Second = "Then sub-category" },
        SortColumn<CodexMissingRow>.Text("Sub-category", row => row.Category, row => row.SubCategory) with { Second = "Within category" },
        SortColumn<CodexMissingRow>.Figure("Reported in", row => (long?)row.Systems, SortDirection.Descending) with { Second = "Systems" },
        SortColumn<CodexMissingRow>.Plain("Status") with { Second = "Whole record" },
    ];

    /// <summary>The index of "Reported in" in <see cref="MissingHeads"/>, the column the default order follows.</summary>
    public const int MissingDefaultColumn = 4;

    /// <summary>The regions table's columns.</summary>
    public static IReadOnlyList<SortColumn<CodexRegionRow>> RegionHeads() =>
    [
        SortColumn<CodexRegionRow>.Text("Region", row => row.Name) with { Second = "Most entries in" },
        SortColumn<CodexRegionRow>.Figure("New", row => (long?)row.New, SortDirection.Descending) with { Second = "New to codex" },
        SortColumn<CodexRegionRow>.Figure("Logged", row => (long?)row.Logged, SortDirection.Descending) with { Second = "Distinct" },
        SortColumn<CodexRegionRow>.Figure("Entries", row => (long?)row.Entries, SortDirection.Descending) with { Second = "Scans" },
        SortColumn<CodexRegionRow>.Figure("Discoveries", row => (long?)row.Recorded, SortDirection.Descending) with { Second = "Of known · whole record" },
        SortColumn<CodexRegionRow>.Plain(string.Empty),
    ];

    /// <summary>The entries table's columns; Status sorts new first, then newest.</summary>
    public static IReadOnlyList<SortColumn<CodexEntryRow>> EntryHeads() =>
    [
        SortColumn<CodexEntryRow>.Text("Entry", row => row.Name) with { Second = "Codex name" },
        SortColumn<CodexEntryRow>.Text("System", row => row.System) with { Second = "Body · lat, lon" },
        SortColumn<CodexEntryRow>.Figure("Time", row => (long?)row.At.UtcTicks, SortDirection.Descending) with { Second = "UTC" },
        SortColumn<CodexEntryRow>.Figure("Status", row => (long?)(row.IsNew ? 1L : 0L), SortDirection.Descending) with { Second = "New or logged" },
    ];

    private static CodexEntryRow Row(
        CodexLogEntry entry,
        int index,
        IReadOnlyList<CodexLogEntry> all,
        Dictionary<(long, int?), int> firsts,
        string? currentSystem,
        Func<long, int, string?> bodyName,
        bool whole)
    {
        var body = entry.SubCategorySymbol is "$Codex_SubCategory_Terrestrials;" or "$Codex_SubCategory_Gas_Giants;"
            ? entry.Body
            : entry.SystemAddress is { } address && entry.BodyId is { } id ? bodyName(address, id) : null;
        var first = firsts[(entry.EntryId, entry.RegionId)];

        return new CodexEntryRow(
            entry.EntryId,
            entry.Name ?? $"Entry {entry.EntryId}",
            entry.System,
            currentSystem is not null && string.Equals(entry.System, currentSystem, StringComparison.OrdinalIgnoreCase),
            BodyOf(entry.System, body),
            PositionOf(entry.Latitude, entry.Longitude),
            entry.At,
            entry.IsNew,
            !entry.IsNew && first < index ? all[first].At : null,
            entry.IsNew ? null : first < index ? null : whole ? "before your journals" : "before D47 started");
    }

    private static bool Matches(CodexLogEntry entry, string text) =>
        entry.Name?.Contains(text, StringComparison.OrdinalIgnoreCase) == true
        || entry.System?.Contains(text, StringComparison.OrdinalIgnoreCase) == true;

    private static string GroupLabel(CodexEntry entry) => $"{entry.Category} · {entry.SubCategory}";

    private static string Label(CodexLogEntry entry) => entry.Category is null
        ? entry.SubCategory ?? Other
        : entry.SubCategory is null ? entry.Category : $"{entry.Category} · {entry.SubCategory}";

    private static string Degrees(double value) =>
        (value < 0 ? Minus : string.Empty) + Math.Abs(value).ToString("0.0000", CultureInfo.InvariantCulture);


    private static string Day(DateTimeOffset at) => at.UtcDateTime.ToString("d MMMM", CultureInfo.InvariantCulture);
}
