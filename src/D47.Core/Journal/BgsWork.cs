using System.Collections.Immutable;
using D47.Core.Knowledge;

namespace D47.Core.Journal;

public enum BgsWorkKind
{
    Mission,
    Bounty,
    CombatBond,
    Trade,
    Cartography,
    Organic,
}

/// <summary>One piece of BGS work. <c>Amount</c> is signed marks for a mission and credits otherwise; <c>Profit</c> is set for trade.</summary>
public readonly record struct BgsEntry(
    DateTimeOffset At,
    long SystemAddress,
    string Faction,
    BgsWorkKind Kind,
    long Amount,
    long Profit = 0);

/// <summary>The station the Commander is docked at; <c>Faction</c> is null where it has no minor faction.</summary>
public sealed record BgsStation(long MarketId, long SystemAddress, string? Faction);

/// <summary>BGS work over the last week, each entry with the faction it was done for.</summary>
public sealed record BgsWork
{
    public static readonly BgsWork None = new();

    private const string FleetCarrierFaction = "FleetCarrier";

    private static readonly TimeSpan Kept = BgsTickWatch.MostBehind;

    /// <summary>The entries, oldest first.</summary>
    public IReadOnlyList<BgsEntry> Entries { get; init; } = [];

    /// <summary>The station the Commander is docked at, or null.</summary>
    public BgsStation? Station { get; init; }

    private ImmutableDictionary<long, string> Names { get; init; } = ImmutableDictionary<long, string>.Empty;

    public bool IsKnown => Entries.Count > 0 || !Names.IsEmpty;

    /// <summary>The name of a system by address, or null when none has been seen.</summary>
    public string? NameOf(long address) => Names.GetValueOrDefault(address);

    /// <summary>The start of the period to report: the BGS tick when it is known, otherwise the 24 hours before <paramref name="now"/>.</summary>
    public static DateTimeOffset PeriodStart(DateTimeOffset? tick, DateTimeOffset now) => tick ?? now - TimeSpan.FromHours(24);

    public BgsWork Apply(JournalEvent journalEvent)
    {
        ArgumentNullException.ThrowIfNull(journalEvent);

        var result = Located(journalEvent);
        var added = result.Read(journalEvent).ToList();

        if (added.Count > 0)
        {
            return result with { Entries = Trimmed([.. result.Entries, .. added]) };
        }

        return result.Entries.Count > 0 && result.Entries[0].At <= journalEvent.Timestamp - Kept
            ? result with { Entries = Trimmed([.. result.Entries], journalEvent.Timestamp) }
            : result;
    }

    /// <summary>
    /// This, recovered from older journals, with what the live journal has folded since laid over it. An entry
    /// in both is counted once.
    /// </summary>
    public BgsWork With(BgsWork live)
    {
        ArgumentNullException.ThrowIfNull(live);

        if (!live.IsKnown && live.Station is null)
        {
            return this;
        }

        var names = live.Names.Aggregate(Names, (all, pair) => all.SetItem(pair.Key, pair.Value));

        if (Entries.Count == 0)
        {
            return live with { Names = names };
        }

        var last = Entries[^1].At;
        var atLast = Entries.Count(entry => entry.At == last);
        var newer = live.Entries.Where(entry => entry.At > last);
        var sameSecond = live.Entries.Where(entry => entry.At == last).Skip(atLast);

        return new BgsWork { Entries = Trimmed([.. Entries, .. sameSecond, .. newer]), Station = live.Station, Names = names };
    }

    private BgsWork Located(JournalEvent journalEvent)
    {
        var result = this;

        if (journalEvent.Kind is "FSDJump" or "Location" or "CarrierJump" or "Docked"
            && journalEvent.Long("SystemAddress") is { } address
            && journalEvent.String("StarSystem") is { Length: > 0 } name
            && !string.Equals(Names.GetValueOrDefault(address), name, StringComparison.Ordinal))
        {
            result = result with { Names = Names.SetItem(address, name) };
        }

        return journalEvent.Kind switch
        {
            "Docked" => result with { Station = StationOf(journalEvent) },
            "Location" => result with { Station = journalEvent.Bool("Docked") ? StationOf(journalEvent) : null },
            "Undocked" => result with { Station = null },
            _ => result,
        };
    }

    private static BgsStation? StationOf(JournalEvent journalEvent)
    {
        if (journalEvent.Long("MarketID") is not { } market || journalEvent.Long("SystemAddress") is not { } system)
        {
            return null;
        }

        var faction = journalEvent.Object("StationFaction")?.String("Name");

        return new BgsStation(
            market,
            system,
            faction is { Length: > 0 } && !string.Equals(faction, FleetCarrierFaction, StringComparison.Ordinal) ? faction : null);
    }

    private IEnumerable<BgsEntry> Read(JournalEvent journalEvent)
    {
        var at = journalEvent.Timestamp;

        switch (journalEvent.Kind)
        {
            case "MissionCompleted":
                foreach (var effect in journalEvent.Items("FactionEffects"))
                {
                    if (effect.String("Faction") is not { Length: > 0 } faction)
                    {
                        continue;
                    }

                    foreach (var influence in effect.Items("Influence"))
                    {
                        var sign = influence.String("Trend") switch
                        {
                            "UpGood" => 1,
                            "DownBad" => -1,
                            _ => 0,
                        };

                        if (sign != 0 && influence.Long("SystemAddress") is { } system)
                        {
                            var marks = Math.Max(influence.String("Influence")?.Count(mark => mark == '+') ?? 0, 1);
                            yield return new BgsEntry(at, system, faction, BgsWorkKind.Mission, sign * marks);
                        }
                    }
                }

                break;

            case "RedeemVoucher" when Station is { } voucherStation:
                switch (journalEvent.String("Type")?.ToLowerInvariant())
                {
                    case "bounty":
                        foreach (var item in journalEvent.Items("Factions"))
                        {
                            if (item.String("Faction") is { Length: > 0 } faction && item.Long("Amount") is { } amount)
                            {
                                yield return new BgsEntry(at, voucherStation.SystemAddress, faction, BgsWorkKind.Bounty, amount);
                            }
                        }

                        break;

                    case "combatbond":
                        if (journalEvent.String("Faction") is { Length: > 0 } bondFaction && journalEvent.Long("Amount") is { } bond)
                        {
                            yield return new BgsEntry(at, voucherStation.SystemAddress, bondFaction, BgsWorkKind.CombatBond, bond);
                        }

                        break;
                }

                break;

            case "MarketSell" when SoldAt(journalEvent) is { } market:
                if (journalEvent.Long("TotalSale") is { } sale)
                {
                    var profit = journalEvent.Long("SellPrice") is { } price
                        && journalEvent.Long("AvgPricePaid") is { } paid
                        && journalEvent.Long("Count") is { } count
                            ? (price - paid) * count
                            : 0;

                    yield return new BgsEntry(at, market.SystemAddress, market.Faction!, BgsWorkKind.Trade, sale, profit);
                }

                break;

            case "SellOrganicData" when SoldAt(journalEvent) is { } organicMarket:
                yield return new BgsEntry(
                    at,
                    organicMarket.SystemAddress,
                    organicMarket.Faction!,
                    BgsWorkKind.Organic,
                    journalEvent.Items("BioData").Sum(data => (data.Long("Value") ?? 0) + (data.Long("Bonus") ?? 0)));
                break;

            case "SellExplorationData" or "MultiSellExplorationData"
                when Station is { Faction: not null } docked && journalEvent.Long("TotalEarnings") is { } earned:
                yield return new BgsEntry(at, docked.SystemAddress, docked.Faction, BgsWorkKind.Cartography, earned);
                break;
        }
    }

    /// <summary>The docked station when it has a minor faction and the event's market is its own.</summary>
    private BgsStation? SoldAt(JournalEvent journalEvent) =>
        Station is { Faction: not null } station && journalEvent.Long("MarketID") == station.MarketId ? station : null;

    private static BgsEntry[] Trimmed(BgsEntry[] entries) =>
        Trimmed(entries, entries.Length > 0 ? entries.Max(entry => entry.At) : default);

    private static BgsEntry[] Trimmed(BgsEntry[] entries, DateTimeOffset newest) =>
        [.. entries.Where(entry => entry.At > newest - Kept).OrderBy(entry => entry.At)];
}
