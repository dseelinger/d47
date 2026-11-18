using System.Collections.Frozen;
using D47.Core.Journal;

namespace D47.Core.Presence;

/// <summary>
/// Whether the Commander has been away from the controls for a given time. Activity is a <see cref="Stir"/> or one
/// of the journal events only the Commander causes. Time comes only from the caller; the first call starts it as
/// present.
/// </summary>
public sealed class PresenceWatch
{
    /// <summary>Events Elite writes only when the Commander acts; <c>Touchdown</c> and <c>Liftoff</c> count when flown by hand.</summary>
    public static readonly FrozenSet<string> Commanded = new HashSet<string>(StringComparer.Ordinal)
    {
        "ApproachBody",
        "ApproachSettlement",
        "BookTaxi",
        "BuyDrones",
        "CarrierJumpRequest",
        "Disembark",
        "DockSRV",
        "Docked",
        "DockingRequested",
        "EjectCargo",
        "Embark",
        "EngineerCraft",
        "FSDJump",
        "FSSDiscoveryScan",
        "LaunchSRV",
        "LeaveBody",
        "Market",
        "MarketBuy",
        "MarketSell",
        "MaterialTrade",
        "MissionAbandoned",
        "MissionAccepted",
        "MissionCompleted",
        "ModuleBuy",
        "MultiSellExplorationData",
        "NavRoute",
        "NavRouteClear",
        "Outfitting",
        "RedeemVoucher",
        "RefuelAll",
        "RepairAll",
        "Resurrect",
        "SAAScanComplete",
        "ScanOrganic",
        "Screenshot",
        "SellOrganicData",
        "SendText",
        "Shipyard",
        "ShipyardSwap",
        "StartJump",
        "SupercruiseDestinationDrop",
        "SupercruiseEntry",
        "SupercruiseExit",
        "SwitchSuitLoadout",
        "USSDrop",
        "Undocked",
    }.ToFrozenSet(StringComparer.Ordinal);

    private DateTimeOffset? _since;

    /// <summary>Activity that is not a journal event: input, a controller press, a press on the headset panel.</summary>
    public void Stir(DateTimeOffset now) => _since = now;

    /// <summary>Takes one tick's events.</summary>
    public void Observe(IEnumerable<JournalEvent> events, DateTimeOffset now)
    {
        _since ??= now;

        if (events.Any(IsCommanded))
        {
            _since = now;
        }
    }

    /// <summary>Whether <paramref name="after"/> has passed with no activity; never when it is zero or less.</summary>
    public bool IsAway(DateTimeOffset now, TimeSpan after)
    {
        _since ??= now;

        return after > TimeSpan.Zero && now - _since.Value >= after;
    }

    /// <summary>Whether the Commander caused this event.</summary>
    public static bool IsCommanded(JournalEvent journalEvent) =>
        Commanded.Contains(journalEvent.Kind)
        || (journalEvent.Kind is "Touchdown" or "Liftoff" && journalEvent.Bool("PlayerControlled"));
}
