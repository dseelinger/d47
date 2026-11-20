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
    string Key,
    string Name,
    string? System,
    bool IsHere,
    string? Body,
    string? Position,
    DateTimeOffset At,
    bool IsNew,
    DateTimeOffset? First,
    string? Origin,
    CodexFirst Possible = CodexFirst.None,
    string? PlaceholderFile = null)
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

/// <summary>What one entry's page draws: the entry's facts and each time it was logged in the region, newest first.</summary>
public sealed record CodexEntryView(
    string Key,
    string Name,
    string? Category,
    string? SubCategory,
    string? PlaceholderFile,
    IReadOnlyList<CodexEntryRow> Loggings,
    RegionFirstRecord? Record = null);

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

    public const string PossibleFirstsCrumbWord = "Possible firsts";

    public const string RegionsCrumbWord = "Regions";

    public const string RegionsRowLine = "What you logged in each region: this trip, this session, or your whole record.";

    public const string PossibleFirstsPlaceholder = "Search entry, region or system";

    public const string PossibleFirstsRowLine = "Entries you logged that the codex table did not list for their region, kept as they were judged. Whole record, not this trip.";

    public const string PossibleFirstsNoMatchText = "No possible first matches that search.";

    public const string DiscoveriesRowLine = "The entries known in this region that are not on your record. Whole record, not this trip.";

    public const string DiscoveriesNoRegionLine = "D47 has no region number for these entries, so it cannot list what is left to discover.";

    public const string NoPictureText = "No picture of this entry yet.";

    public const string StockOffText = "No picture of this entry. Stock codex pictures are off.";

    public const string FetchingText = "Fetching the picture.";

    public const string ChooseWord = "Choose a picture";

    public const string CropWord = "Crop";

    public const string NoCapturesText = "No screenshots yet. Say \"take a picture\" in game, or press Elite's screenshot key, to take one.";

    public const string TakenWithHead = "Taken with this entry";

    public const string OtherCapturesHead = "Other screenshots";

    public const string UnknownSystemText = "Unknown system";

    public const string CaptureGoneText = "The capture could not be read.";

    public const string BrighteningText = "Brightening the capture.";

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

    /// <summary>"12 regions · 34 entries this trip": the figure on the Codex root's Regions row.</summary>
    public static string RegionsFigure(CodexRootView view, CodexPeriod period) =>
        $"{RegionCount(view.Regions)} · {EntryCount(view.Entries)} " + period switch
        {
            CodexPeriod.Trip => "this trip",
            CodexPeriod.Session => "this session",
            _ => "on record",
        };

    /// <summary>"142 entries", "1 entry".</summary>
    public static string EntryCount(int count) => $"{Number(count)} {(count == 1 ? "entry" : "entries")}";

    public static string Number(long count) => count.ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>The record lookup for the active Commander, or null when there is no book.</summary>
    public static Func<long, int, RegionFirstRecord?>? RegionFirstLookup(RegionFirstBook? book, CommanderGameState? state) =>
        book is null || state?.Identity.FrontierId is not { Length: > 0 } id ? null : (entry, region) => book.Find(id, entry, region);

    public static string RegionFirstWord(CodexFirst first) =>
        first == CodexFirst.Galaxy ? "POSSIBLE FIRST IN THE GALAXY" : "POSSIBLE REGION FIRST";

    /// <summary>"3 region · 1 galaxy · 2 not reported": the figures on the Possible firsts row.</summary>
    public static string PossibleFirstsFigures(IReadOnlyList<RegionFirstRecord> records) =>
        $"{Number(records.Count(record => record.First == CodexFirst.Region))} region · "
        + $"{Number(records.Count(record => record.First == CodexFirst.Galaxy))} galaxy · "
        + $"{Number(records.Count(record => record.Reported is null))} not reported";

    /// <summary>The Possible firsts page's grey line.</summary>
    public static string PossibleFirstsLine(IReadOnlyList<RegionFirstRecord> records) =>
        "Each was judged once, against the codex table D47 had when it was recorded. The table may list some of them by now. "
        + $"{Number(records.Count)} possible {(records.Count == 1 ? "first" : "firsts")} · {Number(records.Count(record => record.Reported is null))} not reported.";

    /// <summary>The records the search keeps: it matches the entry, the region and the system.</summary>
    public static IReadOnlyList<RegionFirstRecord> PossibleFirstsMatching(IReadOnlyList<RegionFirstRecord> records, string? query) =>
        string.IsNullOrWhiteSpace(query)
            ? records
            : [.. records.Where(record => new[] { record.Name, record.Region ?? NoRegionName, record.System }
                .Any(text => text?.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase) == true))];

    /// <summary>The Possible firsts table's columns; Logged is the default order, newest first, and Report sorts not reported first.</summary>
    public static IReadOnlyList<SortColumn<RegionFirstRecord>> PossibleFirstHeads() =>
    [
        SortColumn<RegionFirstRecord>.Text("Entry", record => record.Name) with { Second = "Region or galaxy first" },
        SortColumn<RegionFirstRecord>.Text("Region", record => record.Region ?? NoRegionName),
        SortColumn<RegionFirstRecord>.Text("System", record => record.System) with { Second = "Body · lat, lon" },
        SortColumn<RegionFirstRecord>.Figure("Logged", record => (long?)record.At.UtcTicks, SortDirection.Descending) with { Second = "UTC" },
        SortColumn<RegionFirstRecord>.Figure("Report", record => (long?)(record.Reported is null ? 0L : 1L), SortDirection.Ascending) with { Second = "Not reported first" },
        SortColumn<RegionFirstRecord>.Plain(string.Empty),
        SortColumn<RegionFirstRecord>.Plain(string.Empty),
    ];

    /// <summary>The band's sentence: "You may be the first Commander to log Entry in Region." or "… anywhere in the galaxy."</summary>
    public static string RegionFirstSentence(RegionFirstRecord record, string entry) =>
        record.First == CodexFirst.Galaxy
            ? $"You may be the first Commander to log {entry} anywhere in the galaxy."
            : $"You may be the first Commander to log {entry} in {record.Region ?? NoRegionName}.";

    /// <summary>The band's grey line: the table the record was judged against and whether it has been reported.</summary>
    public static string RegionFirstMeta(RegionFirstRecord record) =>
        (record.TableDate is { Length: > 0 } table ? $"table {table} · " : string.Empty)
        + (record.Reported is { } reported ? $"reported {DateTime(reported)} UTC" : "not reported");

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
            var last = ledger.Entries.Where(entry => entry.RegionId == id).Select(entry => (DateTimeOffset?)entry.At)
                .Concat(found.Logged.Select(item => item.Found?.At))
                .Max();

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
        string? query = null,
        Func<long, int, RegionFirstRecord?>? regionFirst = null)
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
                [.. group.Select(item => Row(item.Entry, item.Index, all, firsts, currentSystem, bodyName, ledger.IsWhole, regionFirst))]))
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
        string key,
        int? regionId,
        string fallbackName,
        string? currentSystem,
        Func<long, int, string?> bodyName,
        Func<long, int, RegionFirstRecord?>? regionFirst = null)
    {
        var all = ledger.Entries;
        var firsts = new Dictionary<(long, int?), int>();
        var entryId = CodexKeys.EntryIdOf(key);

        for (var i = 0; i < all.Count; i++)
        {
            firsts.TryAdd((all[i].EntryId, all[i].RegionId), i);
        }

        var here = all
            .Select((entry, index) => (Entry: entry, Index: index))
            .Where(item => item.Entry.EntryId == entryId && item.Entry.RegionId == regionId)
            .Reverse()
            .OrderByDescending(item => item.Entry.At)
            .Select(item => Row(item.Entry, item.Index, all, firsts, currentSystem, bodyName, ledger.IsWhole, regionFirst))
            .ToList();

        var logged = all.LastOrDefault(entry => entry.EntryId == entryId);
        var known = table.Entry(key);

        if (known?.Kind is { } kind)
        {
            if (regionId is { } kindRegion && ledger.Found(key, kindRegion) is { } found)
            {
                here.Add(KindRow(found, known.Name, currentSystem));
            }

            return new CodexEntryView(
                key,
                known.Name,
                known.SubCategory,
                kind.ParentKey is { } parent ? table.Entry(parent)?.Name : null,
                CodexFamilies.FileOfTableName(known.SubCategory),
                here);
        }

        return new CodexEntryView(
            key,
            logged?.Name ?? known?.Name ?? fallbackName,
            logged?.Category ?? known?.Category,
            logged?.SubCategory ?? known?.SubCategory,
            logged is not null
                ? CodexFamilies.FileOfSymbol(logged.SubCategorySymbol)
                : CodexFamilies.FileOfTableName(known?.SubCategory),
            here,
            regionId is { } id && entryId is { } logId ? regionFirst?.Invoke(logId, id) : null);
    }

    /// <summary>"708 missing · 38 of 746 on record": the figures on the region page's Discoveries row.</summary>
    public static string DiscoveriesFigures(CodexDiscoveries found) =>
        $"{Number(found.Missing.Count)} missing · {Number(found.Logged.Count)} of {Number(found.Known.Count)} on record";

    /// <summary>"An entry logged before 2025-03-13 reads as missing." followed by the coverage sentence and the count the table does not list.</summary>
    public static string DiscoveriesNote(DateTimeOffset? since, int uncovered) =>
        (since is { } at ? $"An entry logged before {Date(at)} reads as missing. " : "An entry logged before your record starts reads as missing. ")
        + CodexDiscoveries.Coverage
        + (uncovered > 0 ? $" You logged {Number(uncovered)} more here that the codex table does not list." : string.Empty);

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

    private static CodexEntryRow KindRow(CodexFoundKind found, string name, string? currentSystem) =>
        new(
            found.Key,
            name,
            found.System,
            currentSystem is not null && string.Equals(found.System, currentSystem, StringComparison.OrdinalIgnoreCase),
            BodyOf(found.System, found.Body),
            null,
            found.At,
            false,
            null,
            null);

    private static CodexEntryRow Row(
        CodexLogEntry entry,
        int index,
        IReadOnlyList<CodexLogEntry> all,
        Dictionary<(long, int?), int> firsts,
        string? currentSystem,
        Func<long, int, string?> bodyName,
        bool whole,
        Func<long, int, RegionFirstRecord?>? regionFirst)
    {
        var body = entry.SubCategorySymbol is "$Codex_SubCategory_Terrestrials;" or "$Codex_SubCategory_Gas_Giants;"
            ? entry.Body
            : entry.SystemAddress is { } address && entry.BodyId is { } id ? bodyName(address, id) : null;
        var first = firsts[(entry.EntryId, entry.RegionId)];
        var record = entry.RegionId is { } region ? regionFirst?.Invoke(entry.EntryId, region) : null;

        return new CodexEntryRow(
            CodexKeys.Of(entry.EntryId),
            entry.Name ?? $"Entry {entry.EntryId}",
            entry.System,
            currentSystem is not null && string.Equals(entry.System, currentSystem, StringComparison.OrdinalIgnoreCase),
            BodyOf(entry.System, body),
            PositionOf(entry.Latitude, entry.Longitude),
            entry.At,
            entry.IsNew,
            !entry.IsNew && first < index ? all[first].At : null,
            entry.IsNew ? null : first < index ? null : whole ? "before your journals" : "before D47 started",
            record is not null && record.At == entry.At ? record.First : CodexFirst.None,
            CodexFamilies.FileOfSymbol(entry.SubCategorySymbol) ?? CodexFamilies.FileOfTableName(entry.SubCategory));
    }

    private static bool Matches(CodexLogEntry entry, string text) =>
        entry.Name?.Contains(text, StringComparison.OrdinalIgnoreCase) == true
        || entry.System?.Contains(text, StringComparison.OrdinalIgnoreCase) == true;

    private static string Label(CodexLogEntry entry) => entry.Category is null
        ? entry.SubCategory ?? Other
        : entry.SubCategory is null ? entry.Category : $"{entry.Category} · {entry.SubCategory}";

    private static string Degrees(double value) =>
        (value < 0 ? Minus : string.Empty) + Math.Abs(value).ToString("0.0000", CultureInfo.InvariantCulture);


    private static string Day(DateTimeOffset at) => at.UtcDateTime.ToString("d MMMM", CultureInfo.InvariantCulture);
}
