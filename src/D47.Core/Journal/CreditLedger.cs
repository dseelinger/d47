using System.Collections.Frozen;
using System.Collections.Immutable;

namespace D47.Core.Journal;

public enum CreditKind
{
    Trade,
    Exploration,
    Missions,
    Combat,
    Vouchers,
    Rescue,
    CommunityGoals,
    Colonisation,
    Powerplay,
    Ships,
    Modules,
    Upkeep,
    Rebuy,
    Fines,
    Crew,
    OnFoot,
    Engineers,
    Carrier,
    CarrierBank,
    Unaccounted,
}

/// <summary>One credit transaction. <c>Amount</c> is signed; <c>Balance</c> is the balance after it, or null before an anchor.</summary>
public sealed record CreditEntry(DateTimeOffset At, CreditKind Kind, string Event, long Amount, long? Balance);

/// <summary>Credits received and spent in one kind; <c>Out</c> is the amount spent, not negated.</summary>
public sealed record CreditKindTotal(CreditKind Kind, long In, long Out, int Count);

/// <summary>The entries since a time. <c>Unaccounted</c> is the signed sum of the gaps found at anchors.</summary>
public sealed record CreditSlice(
    DateTimeOffset? From,
    IReadOnlyList<CreditKindTotal> ByKind,
    long Unaccounted,
    long? Balance,
    DateTimeOffset? BalanceAt);

/// <summary>Every credit transaction the journals show for one Commander, with a running balance.</summary>
public sealed record CreditLedger
{
    public static readonly CreditLedger None = new();

    /// <summary>The event names <see cref="Apply"/> reads, for callers that filter lines before parsing.</summary>
    public static readonly FrozenSet<string> Reads = FrozenSet.ToFrozenSet<string>(
    [
        "BookDropship", "BookTaxi", "BuyAmmo", "BuyDrones", "BuyExplorationData", "BuyMicroResources", "BuySuit",
        "BuyTradeData", "BuyWeapon", "CancelDropship", "CancelTaxi", "CarrierBankTransfer", "CarrierBuy",
        "ColonisationConstructionDepot", "ColonisationContribution", "Commander", "CommunityGoalReward", "CrewHire",
        "EngineerContribution", "Fileheader", "FetchRemoteModule", "LoadGame", "MarketBuy", "MarketSell",
        "MissionAbandoned", "MissionCompleted", "MissionFailed", "ModuleBuy", "ModuleBuyAndStore", "ModuleRetrieve",
        "ModuleSell", "ModuleSellRemote", "ModuleStore", "MultiSellExplorationData", "NewCommander",
        "NpcCrewPaidWage", "PayBounties", "PayFines", "PayLegacyFines", "PowerplayFastTrack", "PowerplaySalary",
        "RedeemVoucher", "RefuelAll", "RefuelPartial", "Repair", "RepairAll", "RestockVehicle", "Resurrect",
        "SearchAndRescue", "SellDrones", "SellExplorationData", "SellMicroResources", "SellOrganicData",
        "SellShipOnRebuy", "SellSuit", "SellWeapon", "ShipyardBuy", "ShipyardSell", "ShipyardTransfer",
        "UpgradeSuit", "UpgradeWeapon",
    ], StringComparer.Ordinal);

    private readonly record struct Move(CreditKind Kind, long Amount);

    private readonly record struct Opening(DateTimeOffset At, string Event, long Balance);

    /// <summary>The entries, oldest first.</summary>
    public IReadOnlyList<CreditEntry> Entries => Held;

    /// <summary>The balance after the last entry or anchor, or null before any anchor.</summary>
    public long? Balance { get; private init; }

    /// <summary>The first event this record folded; transactions before it are not counted.</summary>
    public DateTimeOffset? OnRecordSince { get; init; }

    /// <summary>True once the walk over earlier journals has been merged in.</summary>
    public bool IsWhole { get; init; }

    public bool IsKnown => !Held.IsEmpty || OnRecordSince is not null;

    private ImmutableList<CreditEntry> Held { get; init; } = [];

    private ImmutableDictionary<string, long> Payments { get; init; } = ImmutableDictionary<string, long>.Empty;

    private DateTimeOffset? LastEventAt { get; init; }

    private Opening? FirstAnchor { get; init; }

    private bool Legacy { get; init; }

    /// <summary>True for a <c>Fileheader</c> of a legacy (3.x) journal, which keeps a separate balance.</summary>
    public static bool IsLegacy(JournalEvent fileheader)
    {
        ArgumentNullException.ThrowIfNull(fileheader);

        return fileheader.String("gameversion")?.StartsWith("3.", StringComparison.Ordinal) == true;
    }

    /// <summary>The entries at or after <paramref name="from"/>, or all of them for null.</summary>
    public CreditSlice Since(DateTimeOffset? from)
    {
        var items = Held.Where(entry => from is null || entry.At >= from).ToList();
        var last = items.Count > 0 ? items[^1] : null;

        return new CreditSlice(
            from,
            [
                .. items
                    .Where(entry => entry.Kind != CreditKind.Unaccounted)
                    .GroupBy(entry => entry.Kind)
                    .OrderBy(group => group.Key)
                    .Select(group => new CreditKindTotal(
                        group.Key,
                        group.Where(entry => entry.Amount > 0).Sum(entry => entry.Amount),
                        -group.Where(entry => entry.Amount < 0).Sum(entry => entry.Amount),
                        group.Count())),
            ],
            items.Where(entry => entry.Kind == CreditKind.Unaccounted).Sum(entry => entry.Amount),
            last?.Balance,
            last?.At);
    }

    public CreditLedger Apply(JournalEvent journalEvent)
    {
        ArgumentNullException.ThrowIfNull(journalEvent);

        switch (journalEvent.Kind)
        {
            case "NewCommander":
                return None with { IsWhole = IsWhole };

            case "Fileheader":
                return this with { Legacy = IsLegacy(journalEvent) };
        }

        if (Legacy)
        {
            return this;
        }

        var result = this with { OnRecordSince = OnRecordSince ?? journalEvent.Timestamp, LastEventAt = journalEvent.Timestamp };

        if (journalEvent.Kind == "ColonisationConstructionDepot")
        {
            return result.Priced(journalEvent);
        }

        foreach (var move in result.Moves(journalEvent))
        {
            if (move.Amount != 0)
            {
                result = result.Posted(journalEvent.Timestamp, move.Kind, journalEvent.Kind, move.Amount);
            }
        }

        return result.Anchored(journalEvent);
    }

    /// <summary>
    /// This, recovered from older journals, with what the live journal has folded since laid over it, marked
    /// whole. An entry in both is counted once.
    /// </summary>
    public CreditLedger With(CreditLedger live)
    {
        ArgumentNullException.ThrowIfNull(live);

        var lastMove = Held.FindLast(entry => entry.Kind != CreditKind.Unaccounted)?.At;
        var atLast = Held.Count(entry => entry.Kind != CreditKind.Unaccounted && entry.At == lastMove);
        var seenAtLast = 0;
        var added = new List<CreditEntry>();

        foreach (var entry in live.Held)
        {
            if (entry.Kind == CreditKind.Unaccounted
                ? LastEventAt is not { } seen || entry.At > seen
                : lastMove is not { } last || entry.At > last || entry.At == last && ++seenAtLast > atLast)
            {
                added.Add(entry);
            }
        }

        if (Balance is { } mine
            && live.FirstAnchor is { } anchor
            && LastEventAt < anchor.At
            && mine != anchor.Balance)
        {
            var gap = new CreditEntry(anchor.At, CreditKind.Unaccounted, anchor.Event, anchor.Balance - mine, anchor.Balance);
            var at = added.FindIndex(entry => entry.At >= anchor.At);

            added.Insert(at < 0 ? added.Count : at, gap);
        }

        var since = OnRecordSince is { } first && live.OnRecordSince is { } theirs
            ? (first < theirs ? first : theirs)
            : OnRecordSince ?? live.OnRecordSince;

        var latest = LastEventAt is { } own && live.LastEventAt is { } liveAt
            ? (own > liveAt ? own : liveAt)
            : LastEventAt ?? live.LastEventAt;

        return new CreditLedger
        {
            Held = Held.AddRange(added),
            Balance = live.Balance ?? (Balance is { } held ? held + added.Sum(entry => entry.Amount) : null),
            OnRecordSince = since,
            IsWhole = true,
            Payments = live.Payments.Aggregate(Payments, (all, pair) => all.SetItem(pair.Key, pair.Value)),
            LastEventAt = latest,
            Legacy = live.Legacy,
        };
    }

    private static Move In(CreditKind kind, long? amount) => new(kind, amount ?? 0);

    private static Move Out(CreditKind kind, long? amount) => new(kind, -(amount ?? 0));

    private static string PaymentKey(long market, string name) => $"{market}/{name.ToLowerInvariant()}";

    private static CreditKind VoucherKind(string? type) => type?.ToLowerInvariant() switch
    {
        "trade" => CreditKind.Trade,
        "codex" => CreditKind.Exploration,
        "bounty" or "combatbond" => CreditKind.Combat,
        _ => CreditKind.Vouchers,
    };

    private Move[] Moves(JournalEvent e)
    {
        switch (e.Kind)
        {
            case "MarketSell":
                return [In(CreditKind.Trade, e.Long("TotalSale"))];
            case "MarketBuy":
                return [Out(CreditKind.Trade, e.Long("TotalCost"))];
            case "BuyTradeData":
                return [Out(CreditKind.Trade, e.Long("Cost"))];

            case "RedeemVoucher":
                return [In(VoucherKind(e.String("Type")), e.Long("Amount"))];

            case "SellExplorationData" or "MultiSellExplorationData":
                return [In(CreditKind.Exploration, e.Long("TotalEarnings"))];
            case "SellOrganicData":
                return [In(CreditKind.Exploration, e.Items("BioData").Sum(data => (data.Long("Value") ?? 0) + (data.Long("Bonus") ?? 0)))];
            case "BuyExplorationData":
                return [Out(CreditKind.Exploration, e.Long("Cost"))];

            case "MissionCompleted":
                return [In(CreditKind.Missions, e.Long("Reward")), Out(CreditKind.Missions, e.Long("Donated"))];
            case "MissionFailed" or "MissionAbandoned":
                return [Out(CreditKind.Missions, e.Long("Fine"))];

            case "SearchAndRescue":
                return [In(CreditKind.Rescue, e.Long("Reward"))];
            case "CommunityGoalReward":
                return [In(CreditKind.CommunityGoals, e.Long("Reward"))];

            case "ColonisationContribution":
                return [In(CreditKind.Colonisation, Delivered(e))];

            case "PowerplaySalary":
                return [In(CreditKind.Powerplay, e.Long("Amount"))];
            case "PowerplayFastTrack":
                return [Out(CreditKind.Powerplay, e.Long("Cost"))];

            case "ShipyardSell" or "SellShipOnRebuy":
                return [In(CreditKind.Ships, e.Long("ShipPrice"))];
            case "ShipyardBuy":
                return [In(CreditKind.Ships, e.Long("SellPrice")), Out(CreditKind.Ships, e.Long("ShipPrice"))];
            case "ShipyardTransfer":
                return [Out(CreditKind.Ships, e.Long("TransferPrice"))];

            case "ModuleSell" or "ModuleSellRemote":
                return [In(CreditKind.Modules, e.Long("SellPrice"))];
            case "ModuleBuy":
                return [In(CreditKind.Modules, e.Long("SellPrice")), Out(CreditKind.Modules, e.Long("BuyPrice"))];
            case "ModuleBuyAndStore":
                return [Out(CreditKind.Modules, e.Long("BuyPrice"))];
            case "FetchRemoteModule":
                return [Out(CreditKind.Modules, e.Long("TransferCost"))];
            case "ModuleStore" or "ModuleRetrieve":
                return [Out(CreditKind.Modules, e.Long("Cost"))];

            case "SellDrones":
                return [In(CreditKind.Upkeep, e.Long("TotalSale"))];
            case "RefuelAll" or "RefuelPartial" or "Repair" or "RepairAll" or "BuyAmmo" or "RestockVehicle":
                return [Out(CreditKind.Upkeep, e.Long("Cost"))];
            case "BuyDrones":
                return [Out(CreditKind.Upkeep, e.Long("TotalCost"))];

            case "Resurrect":
                return [Out(CreditKind.Rebuy, e.Long("Cost"))];
            case "PayBounties" or "PayFines" or "PayLegacyFines":
                return [Out(CreditKind.Fines, e.Long("Amount"))];

            case "NpcCrewPaidWage":
                return [Out(CreditKind.Crew, e.Long("Amount"))];
            case "CrewHire":
                return [Out(CreditKind.Crew, e.Long("Cost"))];

            case "SellSuit" or "SellWeapon" or "SellMicroResources":
                return [In(CreditKind.OnFoot, e.Long("Price"))];
            case "CancelTaxi" or "CancelDropship":
                return [In(CreditKind.OnFoot, e.Long("Refund"))];
            case "BuySuit" or "BuyWeapon" or "BuyMicroResources":
                return [Out(CreditKind.OnFoot, e.Long("Price"))];
            case "UpgradeSuit" or "UpgradeWeapon" or "BookTaxi" or "BookDropship":
                return [Out(CreditKind.OnFoot, e.Long("Cost"))];

            case "EngineerContribution" when string.Equals(e.String("Type"), "Credits", StringComparison.OrdinalIgnoreCase):
                return [Out(CreditKind.Engineers, e.Long("Quantity"))];

            case "CarrierBuy":
                return [Out(CreditKind.Carrier, e.Long("Price"))];
            case "CarrierBankTransfer":
                return [In(CreditKind.CarrierBank, e.Long("Withdraw")), Out(CreditKind.CarrierBank, e.Long("Deposit"))];

            default:
                return [];
        }
    }

    private long Delivered(JournalEvent e) =>
        e.Long("MarketID") is { } market
            ? e.Items("Contributions")
                .Where(item => item.String("Name") is { Length: > 0 })
                .Sum(item => Payments.TryGetValue(PaymentKey(market, item.String("Name")!), out var payment)
                    ? (item.Long("Amount") ?? 0) * payment
                    : 0)
            : 0;

    private CreditLedger Priced(JournalEvent e)
    {
        if (e.Long("MarketID") is not { } market)
        {
            return this;
        }

        var payments = e.Items("ResourcesRequired")
            .Where(item => item.String("Name") is { Length: > 0 } && item.Long("Payment") is not null)
            .Aggregate(Payments, (all, item) => all.SetItem(PaymentKey(market, item.String("Name")!), item.Long("Payment")!.Value));

        return this with { Payments = payments };
    }

    private CreditLedger Posted(DateTimeOffset at, CreditKind kind, string eventName, long amount)
    {
        var balance = Balance is { } held ? held + amount : (long?)null;

        return this with { Held = Held.Add(new CreditEntry(at, kind, eventName, amount, balance)), Balance = balance };
    }

    private CreditLedger Anchored(JournalEvent e)
    {
        var anchor = e.Kind switch
        {
            "LoadGame" => e.Long("Credits"),
            "CarrierBankTransfer" => e.Long("PlayerBalance"),
            _ => null,
        };

        if (anchor is not { } balance)
        {
            return this;
        }

        var result = Balance is { } running && running != balance
            ? this with { Held = Held.Add(new CreditEntry(e.Timestamp, CreditKind.Unaccounted, e.Kind, balance - running, balance)) }
            : this;

        return result with
        {
            Balance = balance,
            FirstAnchor = FirstAnchor ?? (Balance is null ? new Opening(e.Timestamp, e.Kind, balance) : null),
        };
    }
}
