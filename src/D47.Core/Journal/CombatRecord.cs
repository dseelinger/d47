using System.Collections.Immutable;
using System.Text.RegularExpressions;
using D47.Core.Knowledge;

namespace D47.Core.Journal;

public enum KillKind
{
    Bounty,
    Bond,
}

/// <summary>What the Commander was in when the kill was paid.</summary>
public enum CombatVehicle
{
    Ship,
    Srv,
    OnFoot,
}

/// <summary>One faction's payment for a kill.</summary>
public readonly record struct KillPayment(string Faction, long Reward);

/// <summary>
/// One kill. <c>Target</c> is null for a bond; <c>VictimFaction</c> and <c>SystemAddress</c> are null where the
/// journal did not give one; <c>ShipId</c> is set only when <c>Vehicle</c> is <see cref="CombatVehicle.Ship"/>
/// and the ship is known.
/// </summary>
public sealed record Kill(
    DateTimeOffset At,
    KillKind Kind,
    string? Target,
    string? VictimFaction,
    long Awarded,
    IReadOnlyList<KillPayment> PaidBy,
    long? SystemAddress,
    CombatVehicle Vehicle,
    int? ShipId);

/// <summary>The Commander's own vehicle: a hull by <c>ShipId</c>, the SRV, or on foot.</summary>
public readonly record struct OwnVehicle(CombatVehicle Vehicle, int? ShipId);

/// <summary>Kills and credits awarded for one key.</summary>
public sealed record CombatGroup<TKey>(TKey Key, int Kills, long Awarded);

/// <summary>The kills since a time, grouped four ways. Each list is ordered by kills, then credits, most first.</summary>
public sealed record CombatSlice(
    DateTimeOffset? From,
    int Kills,
    long Awarded,
    IReadOnlyList<CombatGroup<string?>> ByTarget,
    IReadOnlyList<CombatGroup<OwnVehicle>> ByVehicle,
    IReadOnlyList<CombatGroup<long?>> BySystem,
    IReadOnlyList<CombatGroup<string?>> ByVictimFaction,
    IReadOnlyList<CombatGroup<string>> ByPayingFaction);

/// <summary>Every kill the journals show for one Commander, with the context each was made in.</summary>
public sealed partial record CombatRecord
{
    public static readonly CombatRecord None = new();

    private readonly record struct ShipSeen(string Type, string? Name);

    /// <summary>The kills, oldest first.</summary>
    public IReadOnlyList<Kill> Kills => Held;

    /// <summary>The first event this record folded; kills before it are not counted.</summary>
    public DateTimeOffset? OnRecordSince { get; init; }

    /// <summary>True once the walk over earlier journals has been merged in.</summary>
    public bool IsWhole { get; init; }

    public bool IsKnown => !Held.IsEmpty || OnRecordSince is not null;

    private ImmutableList<Kill> Held { get; init; } = [];

    private ImmutableDictionary<string, string> TargetNames { get; init; } = ImmutableDictionary<string, string>.Empty;

    private ImmutableDictionary<long, string> Systems { get; init; } = ImmutableDictionary<long, string>.Empty;

    private ImmutableDictionary<int, ShipSeen> Ships { get; init; } = ImmutableDictionary<int, ShipSeen>.Empty;

    private long? Address { get; init; }

    private CombatVehicle Vehicle { get; init; }

    private int? ShipId { get; init; }

    /// <summary>The name of a target: a ship's name, the journal's localised name, else the target itself.</summary>
    public string TargetName(string target)
    {
        ArgumentNullException.ThrowIfNull(target);

        var key = Normalised(target);

        return EliteSpecifications.Ship(key)?.Name ?? TargetNames.GetValueOrDefault(key) ?? key;
    }

    /// <summary>The name of a system by address, or null when none has been seen.</summary>
    public string? SystemName(long address) => Systems.GetValueOrDefault(address);

    /// <summary>A ship as "Name (Type)", or the type alone where it has no name; null for a hull not seen.</summary>
    public string? ShipName(int id)
    {
        if (!Ships.TryGetValue(id, out var seen))
        {
            return null;
        }

        var type = EliteSpecifications.Ship(seen.Type)?.Name ?? seen.Type;

        return seen.Name is { Length: > 0 } name ? $"{name} ({type})" : type;
    }

    /// <summary>The kills at or after <paramref name="from"/>, or all of them for null.</summary>
    public CombatSlice Since(DateTimeOffset? from)
    {
        var kills = Held.Where(kill => from is null || kill.At >= from).ToList();

        static CombatGroup<TKey>[] Grouped<TKey>(IEnumerable<IGrouping<TKey, (int Kills, long Awarded)>> groups) =>
        [
            .. groups
                .Select(group => new CombatGroup<TKey>(group.Key, group.Sum(item => item.Kills), group.Sum(item => item.Awarded)))
                .OrderByDescending(group => group.Kills)
                .ThenByDescending(group => group.Awarded),
        ];

        return new CombatSlice(
            from,
            kills.Count,
            kills.Sum(kill => kill.Awarded),
            Grouped(kills.GroupBy(kill => kill.Target, kill => (1, kill.Awarded))),
            Grouped(kills.GroupBy(kill => new OwnVehicle(kill.Vehicle, kill.ShipId), kill => (1, kill.Awarded))),
            Grouped(kills.GroupBy(kill => kill.SystemAddress, kill => (1, kill.Awarded))),
            Grouped(kills.GroupBy(kill => kill.VictimFaction, kill => (1, kill.Awarded))),
            Grouped(kills
                .SelectMany(kill => kill.PaidBy.Select(payment => (payment.Faction, payment.Reward)))
                .GroupBy(payment => payment.Faction, payment => (1, payment.Reward))));
    }

    public CombatRecord Apply(JournalEvent journalEvent)
    {
        ArgumentNullException.ThrowIfNull(journalEvent);

        if (journalEvent.Kind == "NewCommander")
        {
            return None with { IsWhole = IsWhole };
        }

        var result = OnRecordSince is null ? this with { OnRecordSince = journalEvent.Timestamp } : this;

        switch (journalEvent.Kind)
        {
            case "LoadGame" or "Loadout" when journalEvent.Int("ShipID") is { } id:
                return result.Boarded(id, journalEvent.String("Ship"), journalEvent.String("ShipName"));

            case "ShipyardSwap" when journalEvent.Int("ShipID") is { } swapped:
                return result.Boarded(swapped, journalEvent.String("ShipType"), null);

            case "Location" or "FSDJump" or "CarrierJump":
                return result.Located(journalEvent);

            case "Disembark":
                return result with { Vehicle = CombatVehicle.OnFoot };

            case "LaunchSRV":
                return result with { Vehicle = CombatVehicle.Srv };

            case "Embark":
                return result with { Vehicle = journalEvent.Bool("SRV") ? CombatVehicle.Srv : CombatVehicle.Ship };

            case "DockSRV":
                return result with { Vehicle = CombatVehicle.Ship };

            case "Bounty":
                return result.Killed(BountyOf(journalEvent, result), journalEvent);

            case "FactionKillBond":
                return result.Killed(BondOf(journalEvent, result), journalEvent);

            default:
                return result;
        }
    }

    /// <summary>
    /// This, recovered from older journals, with what the live journal has folded since laid over it, marked
    /// whole. A kill in both is counted once.
    /// </summary>
    public CombatRecord With(CombatRecord live)
    {
        ArgumentNullException.ThrowIfNull(live);

        var kills = live.Held;

        if (!Held.IsEmpty)
        {
            var last = Held[^1].At;
            var atLast = Held.Count(kill => kill.At == last);

            kills = Held
                .AddRange(live.Held.Where(kill => kill.At == last).Skip(atLast))
                .AddRange(live.Held.Where(kill => kill.At > last));
        }

        var since = OnRecordSince is { } mine && live.OnRecordSince is { } theirs
            ? (mine < theirs ? mine : theirs)
            : OnRecordSince ?? live.OnRecordSince;

        var liveVehicleKnown = live.OnRecordSince is not null;

        return new CombatRecord
        {
            Held = kills,
            OnRecordSince = since,
            IsWhole = true,
            TargetNames = live.TargetNames.Aggregate(TargetNames, (all, pair) => all.SetItem(pair.Key, pair.Value)),
            Systems = live.Systems.Aggregate(Systems, (all, pair) => all.SetItem(pair.Key, pair.Value)),
            Ships = live.Ships.Aggregate(Ships, (all, pair) => all.SetItem(pair.Key, pair.Value)),
            Address = live.Address ?? Address,
            Vehicle = liveVehicleKnown ? live.Vehicle : Vehicle,
            ShipId = liveVehicleKnown ? live.ShipId : ShipId,
        };
    }

    private CombatRecord Boarded(int id, string? type, string? name)
    {
        var held = Ships.GetValueOrDefault(id);
        var seen = new ShipSeen(type is { Length: > 0 } ? type : held.Type, name is { Length: > 0 } ? name : held.Name);

        return this with
        {
            Vehicle = CombatVehicle.Ship,
            ShipId = id,
            Ships = seen.Type is { Length: > 0 } ? Ships.SetItem(id, seen) : Ships,
        };
    }

    private CombatRecord Located(JournalEvent journalEvent)
    {
        if (journalEvent.Long("SystemAddress") is not { } address)
        {
            return this;
        }

        var name = journalEvent.String("StarSystem");

        return this with
        {
            Address = address,
            Systems = name is { Length: > 0 } && !string.Equals(Systems.GetValueOrDefault(address), name, StringComparison.Ordinal)
                ? Systems.SetItem(address, name)
                : Systems,
        };
    }

    private CombatRecord Killed(Kill kill, JournalEvent journalEvent)
    {
        var result = this with { Held = Held.Add(kill) };

        if (kill.Target is { } target
            && journalEvent.String("Target_Localised") is { Length: > 0 } named
            && !named.StartsWith('$')
            && !TargetNames.ContainsKey(target))
        {
            result = result with { TargetNames = TargetNames.SetItem(target, named) };
        }

        return result;
    }

    private static Kill BountyOf(JournalEvent journalEvent, CombatRecord at)
    {
        var paid = journalEvent.Items("Rewards")
            .Where(item => item.String("Faction") is { Length: > 0 } && item.Long("Reward") is not null)
            .Select(item => new KillPayment(item.String("Faction")!, item.Long("Reward")!.Value))
            .ToArray();

        return at.Made(
            journalEvent,
            KillKind.Bounty,
            journalEvent.String("Target") is { Length: > 0 } target ? Normalised(target) : null,
            journalEvent.Long("TotalReward") ?? paid.Sum(payment => payment.Reward),
            paid);
    }

    private static Kill BondOf(JournalEvent journalEvent, CombatRecord at)
    {
        var reward = journalEvent.Long("Reward") ?? 0;

        return at.Made(
            journalEvent,
            KillKind.Bond,
            null,
            reward,
            journalEvent.String("AwardingFaction") is { Length: > 0 } faction ? [new KillPayment(faction, reward)] : []);
    }

    private Kill Made(JournalEvent journalEvent, KillKind kind, string? target, long awarded, KillPayment[] paid) =>
        new(
            journalEvent.Timestamp,
            kind,
            target,
            VictimOf(journalEvent),
            awarded,
            paid,
            Address,
            Vehicle,
            Vehicle == CombatVehicle.Ship ? ShipId : null);

    private static string? VictimOf(JournalEvent journalEvent) =>
        journalEvent.String("VictimFaction_Localised") is { Length: > 0 } localised
            ? localised
            : journalEvent.String("VictimFaction") is { Length: > 0 } faction && !faction.StartsWith('$') ? faction : null;

    private static string Normalised(string target) => ClassSuffix().Replace(target.ToLowerInvariant(), string.Empty);

    [GeneratedRegex(@"_class\d+$", RegexOptions.CultureInvariant)]
    private static partial Regex ClassSuffix();
}
