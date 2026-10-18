using System.Globalization;
using D47.App.Theming;
using D47.Core.Journal;

namespace D47.App.Panel;

/// <summary>One kind of the credits table; a null figure means nothing moved that way.</summary>
public sealed record CreditKindRow(
    CreditKind Kind,
    string Name,
    string Description,
    long? In,
    long? Out,
    int Count,
    IReadOnlyList<string> Events)
{
    internal long Movement => (In ?? 0) + (Out ?? 0);
}

/// <summary>What the credits page draws for one period and history state.</summary>
public sealed record CreditsView(
    bool Running,
    bool Failed,
    DateTimeOffset? OnRecordSince,
    long In,
    long Out,
    long CarrierBank,
    long Unaccounted,
    int Gaps,
    long? Balance,
    DateTimeOffset? BalanceAt,
    IReadOnlyList<CreditKindRow> Kinds,
    IReadOnlyList<CreditKindRow> Moved)
{
    public int Total => Kinds.Count + Moved.Count;
}

/// <summary>One row of the transactions table. A folded row stands for <c>Folded</c> small gaps from <c>From</c> to <c>At</c>.</summary>
public sealed record CreditTxRow(
    DateTimeOffset At,
    CreditKind Kind,
    string Event,
    long Amount,
    long? Balance,
    string? Note,
    DateTimeOffset? From = null,
    int Folded = 0);

/// <summary>What the transactions page draws for one period, kind and history state.</summary>
public sealed record CreditTxView(
    bool Running,
    bool Failed,
    DateTimeOffset? OnRecordSince,
    long In,
    long Out,
    int Entries,
    int Shown,
    bool HasGap,
    bool HasNoBalance,
    IReadOnlyList<CreditTxRow> Rows,
    CreditTxRow? Fold);

/// <summary>The rows behind Commander › Ledgers › Credits, built from a <see cref="CreditLedger"/> (#1289).</summary>
public static class CreditsModel
{
    public const long SmallGap = 1000;

    public const string MovedLabel = "Moved, not earned or spent";

    public const string NoMatchText = "Nothing in this period matches that search.";

    public const string RunningText = "D47 is reading your journals for past transactions. The ledger appears here when it finishes.";

    public const string FailedLabel = "History not finished";

    public const string StripLine =
        "In and Out count what you earned and spent. Carrier bank transfers only move credits between you and your carrier, and Unaccounted is the gap between the journal and the game's own balance.";

    public const string GapLine =
        "An Unaccounted entry is where the game's own balance, at a login or a carrier bank transfer, differs from what the journal adds up to: credits from another Commander, a carrier's scrap refund, or a payment the journal does not record.";

    public const string NoBalanceLine = "Entries before your first login on record have no balance.";

    public const string FoldNote = "Gaps under 1,000 CR each, at logins and carrier bank transfers, folded into one line.";

    private const string Minus = "−";

    private static readonly Dictionary<CreditKind, (string Name, string Description)> Words = new()
    {
        [CreditKind.Trade] = ("Trade", "goods and trade data"),
        [CreditKind.Exploration] = ("Exploration", "data and organics sold"),
        [CreditKind.Missions] = ("Missions", "rewards, donations and fines"),
        [CreditKind.Combat] = ("Combat", "bounties and bonds redeemed"),
        [CreditKind.Vouchers] = ("Vouchers", "other vouchers redeemed"),
        [CreditKind.Rescue] = ("Rescue", string.Empty),
        [CreditKind.CommunityGoals] = ("Community goals", string.Empty),
        [CreditKind.Colonisation] = ("Colonisation", "deliveries paid"),
        [CreditKind.Powerplay] = ("Powerplay", string.Empty),
        [CreditKind.Ships] = ("Ships", string.Empty),
        [CreditKind.Modules] = ("Modules", string.Empty),
        [CreditKind.Upkeep] = ("Upkeep", "fuel, repairs, ammo, limpets"),
        [CreditKind.Rebuy] = ("Rebuy", string.Empty),
        [CreditKind.Fines] = ("Fines", "fines and bounties paid"),
        [CreditKind.Crew] = ("Crew", "wages and hires"),
        [CreditKind.OnFoot] = ("On foot", "suits, weapons, goods, taxis"),
        [CreditKind.Engineers] = ("Engineers", string.Empty),
        [CreditKind.Carrier] = ("Carrier", "carrier bought"),
        [CreditKind.CarrierBank] = ("Carrier bank", "between you and your carrier"),
        [CreditKind.Unaccounted] = ("Unaccounted", "the game's balance against the journal"),
    };

    public static string KindName(CreditKind kind) => Words[kind].Name;

    public static bool IsMoved(CreditKind kind) => kind is CreditKind.CarrierBank or CreditKind.Unaccounted;

    /// <summary>"in the last 7 days", or "on record" for everything.</summary>
    public static string InPeriod(CombatPeriod period) => period switch
    {
        CombatPeriod.Day => "in the last 24 hours",
        CombatPeriod.Week => "in the last 7 days",
        CombatPeriod.Month => "in the last 30 days",
        _ => "on record",
    };

    public static string EmptyText(CombatPeriod period) =>
        period == CombatPeriod.Everything ? "No credits moved on record." : $"No credits moved {InPeriod(period)}.";

    public static string EmptyKindText(CreditKind kind, CombatPeriod period) =>
        $"No {KindName(kind)} entries {InPeriod(period)}.";

    public static string FailedText(DateTimeOffset? since) =>
        "D47 couldn't finish reading your older journals, so this ledger starts when D47 started"
        + (since is { } at ? $", at {Clock(at)} UTC today" : string.Empty)
        + ". Transactions from here on are still recorded.";

    /// <summary>"11 kinds", "1 kind".</summary>
    public static string KindCount(int count) => $"{Number(count)} {(count == 1 ? "kind" : "kinds")}";

    /// <summary>"212 entries", "1 entry".</summary>
    public static string EntryCount(int count) => $"{Number(count)} {(count == 1 ? "entry" : "entries")}";

    /// <summary>"+4,812,000 CR" or "−3,120,400 CR", with U+2212 for the minus.</summary>
    public static string Signed(long amount) =>
        (amount < 0 ? Minus : "+") + Math.Abs(amount).ToString("N0", CultureInfo.InvariantCulture) + " CR";

    public static string Credits(long amount) => amount.ToString("N0", CultureInfo.InvariantCulture) + " CR";

    public static string Number(long count) => count.ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>The line under the Carrier bank cell.</summary>
    public static string CarrierLine(long net) =>
        net > 0 ? "net, moved from your carrier" : net < 0 ? "net, moved to your carrier" : "nothing moved";

    public static string GapsLine(int gaps) => $"{Number(gaps)} {(gaps == 1 ? "gap" : "gaps")} with the game";

    public static string BalanceLine(DateTimeOffset at) => $"at {at.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} UTC";

    /// <summary>The second line of a Carrier bank or Unaccounted row; null for the other kinds.</summary>
    public static string? NoteFor(CreditKind kind, string eventName, long amount)
    {
        if (kind == CreditKind.CarrierBank)
        {
            return amount < 0 ? "Moved to your carrier. Not spent." : "Moved from your carrier. Not earned.";
        }

        if (kind != CreditKind.Unaccounted)
        {
            return null;
        }

        var where = eventName switch
        {
            "LoadGame" => "at this login",
            "CarrierBankTransfer" => "at this carrier bank transfer",
            _ => "here",
        };

        return $"Not in the journal. The game's balance {where} was {Credits(Math.Abs(amount))} {(amount < 0 ? "less" : "more")} than the journal adds up to.";
    }

    /// <summary>The credits table for the period: kinds by size of movement, then the two kinds that only move credits.</summary>
    public static CreditsView Rows(CreditLedger ledger, CombatPeriod period, DateTimeOffset now, HistoryState history)
    {
        var from = CombatStatsPage.PeriodStart(period, now);
        var slice = ledger.Since(from);
        var entries = EntriesSince(ledger, from);

        var rows = slice.ByKind
            .Where(total => total.Kind != CreditKind.Unaccounted)
            .Select(total => Row(total.Kind, Positive(total.In), Positive(total.Out), total.Count, entries))
            .ToList();

        var gaps = entries.Where(entry => entry.Kind == CreditKind.Unaccounted).ToList();

        if (gaps.Count > 0)
        {
            rows.Add(Row(
                CreditKind.Unaccounted,
                Positive(gaps.Where(gap => gap.Amount > 0).Sum(gap => gap.Amount)),
                Positive(-gaps.Where(gap => gap.Amount < 0).Sum(gap => gap.Amount)),
                gaps.Count,
                entries));
        }

        var kinds = rows.Where(row => !IsMoved(row.Kind)).OrderByDescending(row => row.Movement).ToList();
        var bank = rows.Where(row => row.Kind == CreditKind.CarrierBank);
        var unaccounted = rows.Where(row => row.Kind == CreditKind.Unaccounted);

        return new CreditsView(
            history is HistoryState.Pending or HistoryState.Running,
            history is HistoryState.Failed or HistoryState.Stopped,
            ledger.OnRecordSince,
            kinds.Sum(row => row.In ?? 0),
            kinds.Sum(row => row.Out ?? 0),
            rows.Where(row => row.Kind == CreditKind.CarrierBank).Sum(row => (row.In ?? 0) - (row.Out ?? 0)),
            slice.Unaccounted,
            gaps.Count,
            slice.Balance,
            slice.BalanceAt,
            kinds,
            [.. bank, .. unaccounted]);
    }

    /// <summary>The kinds whose name, or an event in the period, contains <paramref name="query"/>, ignoring case.</summary>
    public static IReadOnlyList<CreditKindRow> Matching(IReadOnlyList<CreditKindRow> rows, string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return rows;
        }

        var text = query.Trim();

        return
        [
            .. rows.Where(row => row.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
                || row.Events.Any(name => name.Contains(text, StringComparison.OrdinalIgnoreCase))),
        ];
    }

    /// <summary>The kinds table's columns, each sortable from its heading.</summary>
    public static IReadOnlyList<SortColumn<CreditKindRow>> KindHeads() =>
    [
        SortColumn<CreditKindRow>.Text("Kind", row => row.Name) with { Second = "What moved the credits" },
        SortColumn<CreditKindRow>.Figure("In", row => row.In, SortDirection.Descending) with { Second = "Earned" },
        SortColumn<CreditKindRow>.Figure("Out", row => row.Out, SortDirection.Descending) with { Second = "Spent" },
        SortColumn<CreditKindRow>.Figure("Entries", row => (long?)row.Count, SortDirection.Descending) with { Second = "Transactions" },
    ];

    /// <summary>The transactions table's columns.</summary>
    public static IReadOnlyList<SortColumn<CreditTxRow>> TxHeads() =>
    [
        SortColumn<CreditTxRow>.Figure("Time", row => (long?)row.At.UtcTicks, SortDirection.Descending) with { Second = "UTC" },
        SortColumn<CreditTxRow>.Text("Kind", row => KindName(row.Kind), row => row.Event) with { Second = "Journal event" },
        SortColumn<CreditTxRow>.Figure("Amount", row => (long?)row.Amount, SortDirection.Descending) with { Second = "In or out" },
        SortColumn<CreditTxRow>.Figure("Balance", row => row.Balance, SortDirection.Descending) with { Second = "After it" },
    ];

    /// <summary>
    /// The entries for the period and kind (every kind for null) whose kind or event name contains
    /// <paramref name="query"/>, newest first, with small gaps folded into one row.
    /// </summary>
    public static CreditTxView Transactions(
        CreditLedger ledger,
        CombatPeriod period,
        CreditKind? kind,
        DateTimeOffset now,
        HistoryState history,
        string? query = null)
    {
        var from = CombatStatsPage.PeriodStart(period, now);
        var all = EntriesSince(ledger, from).Where(entry => kind is null || entry.Kind == kind).Select(Row).ToList();
        var totals = Rows(ledger, period, now, history);

        var kept = string.IsNullOrWhiteSpace(query)
            ? all
            : [.. all.Where(row => KindName(row.Kind).Contains(query.Trim(), StringComparison.OrdinalIgnoreCase)
                || row.Event.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase))];

        var small = kept.Where(IsSmallGap).ToList();

        CreditTxRow? fold = small.Count == 0
            ? null
            : new CreditTxRow(
                small.Max(row => row.At),
                CreditKind.Unaccounted,
                $"{Number(small.Count)} small {(small.Count == 1 ? "gap" : "gaps")}",
                small.Sum(row => row.Amount),
                null,
                FoldNote,
                small.Min(row => row.At),
                small.Count);

        var rows = Enumerable.Reverse(kept.Where(row => !IsSmallGap(row)))
            .OrderByDescending(row => row.At)
            .ToList();

        long inTotal;
        long outTotal;

        if (kind is { } one)
        {
            var row = totals.Kinds.Concat(totals.Moved).FirstOrDefault(candidate => candidate.Kind == one);
            inTotal = row?.In ?? 0;
            outTotal = row?.Out ?? 0;
        }
        else
        {
            inTotal = totals.In;
            outTotal = totals.Out;
        }

        return new CreditTxView(
            totals.Running,
            totals.Failed,
            ledger.OnRecordSince,
            inTotal,
            outTotal,
            all.Count,
            kept.Count,
            kept.Any(row => row.Kind == CreditKind.Unaccounted),
            kept.Any(row => row.Balance is null),
            rows,
            fold);
    }

    // The event name is copied as display and search text; nothing dispatches on it.
    private static CreditTxRow Row(CreditEntry entry) =>
        new(entry.At, entry.Kind, entry.Event.ToString(), entry.Amount, entry.Balance, NoteFor(entry.Kind, entry.Event, entry.Amount));

    private static bool IsSmallGap(CreditTxRow row) =>
        row.Kind == CreditKind.Unaccounted && Math.Abs(row.Amount) < SmallGap;

    private static long? Positive(long amount) => amount > 0 ? amount : null;

    private static List<CreditEntry> EntriesSince(CreditLedger ledger, DateTimeOffset? from) =>
        [.. ledger.Entries.Where(entry => from is null || entry.At >= from)];

    private static CreditKindRow Row(CreditKind kind, long? received, long? spent, int count, List<CreditEntry> entries) =>
        new(
            kind,
            Words[kind].Name,
            Words[kind].Description,
            received,
            spent,
            count,
            [.. entries.Where(entry => entry.Kind == kind).Select(entry => entry.Event).Distinct(StringComparer.Ordinal)]);

    private static string Clock(DateTimeOffset at) => at.UtcDateTime.ToString("HH:mm", CultureInfo.InvariantCulture);
}
