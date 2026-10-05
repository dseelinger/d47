using D47.Core.Audio;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Callouts;

/// <summary>The carrier captain, warning that the tank holds less than two full jumps (#834).</summary>
public sealed class CarrierFuelCallout : ICallout
{
    public string Id => "carrier-fuel";

    public const string Key = "carrier.fuel";

    /// <summary>The stored carrier plan, read from memory.</summary>
    public Func<StoredRoutePlan?> Plan { get; set; } = () => null;

    /// <summary>The <c>FuelLevel</c> last spoken for; cleared by a <c>LoadGame</c>.</summary>
    private int? _spokenFor;

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        var docked = false;
        var requested = false;
        DateTimeOffset at = default;

        foreach (var journalEvent in context.Events)
        {
            switch (journalEvent.Kind)
            {
                case "LoadGame":
                    _spokenFor = null;
                    break;

                case "Docked":
                    docked = true;
                    at = journalEvent.Timestamp;
                    break;

                case "CarrierJumpRequest":
                    requested = true;
                    at = journalEvent.Timestamp;
                    break;
            }
        }

        if (context.IsPriming
            || context.State is not { Carrier: { Owned: true, IsSquadron: false } carrier }
            || !(requested || (docked && carrier.DockedAtOwnCarrier))
            || carrier.FuelLevel is not { } fuel
            || fuel == _spokenFor
            || !CarrierFuel.IsLow(carrier)
            || CarrierFuel.FullJumpCost(carrier) is not { } cost)
        {
            yield break;
        }

        _spokenFor = fuel;

        var name = carrier.Name is { Length: > 0 } called ? called : carrier.CallSign ?? "The carrier";
        var text = $"{name} has {fuel} tonnes of tritium; a full jump at this load burns {Math.Round(cost)}.";

        if (carrier.StatsSeenAt is { } read && carrier.SeenAt is { } moved && moved > read)
        {
            text += $" That is the reading from {Age(at - read)} ago. The tank can only be lower.";
        }

        text += IcyFallback(carrier, context.State.Loadouts);

        yield return new Announcement(Key, text)
        {
            Voice = VoiceRole.CarrierCaptain,
            Cooldown = TimeSpan.FromSeconds(30),
        };
    }

    /// <summary>Pristine icy-ring waypoints and mining-fit ships, once the plan's last restock is behind the carrier.</summary>
    private string IcyFallback(CarrierState carrier, ShipLoadouts loadouts)
    {
        if (Plan() is not { Kind: RoutePlanKind.Carrier, Carrier: { } route, Reached: { } reached } plan
            || plan.CarrierId is not { } planned
            || planned != carrier.CarrierId)
        {
            return string.Empty;
        }

        var waypoints = route.Waypoints;
        var lastRestock = -1;

        for (var index = 0; index < waypoints.Count; index++)
        {
            if (waypoints[index].MustRestock)
            {
                lastRestock = index;
            }
        }

        if (lastRestock < 0 || reached <= lastRestock)
        {
            return string.Empty;
        }

        var icy = Enumerable.Range(0, waypoints.Count)
            .Where(index => waypoints[index] is { HasIcyRing: true, IsSystemPristine: true })
            .OrderBy(index => Math.Abs(index - reached))
            .ThenBy(index => index)
            .ToList();

        if (icy.Count == 0)
        {
            return " No waypoint on the plan has a pristine icy ring.";
        }

        var named = icy.Take(3).Select(index => Where(waypoints[index].Name, index - reached));
        var text = $" Pristine icy rings: {string.Join(", ", named)}";

        text += icy.Count > 3 ? $", and {icy.Count - 3} more." : ".";

        var fit = loadouts.Ships.Values
            .Select(ship => (Ship: ship.Loadout, Fit: MiningFit.For(ship.Loadout)))
            .Where(entry => entry.Fit is { IsFit: true })
            .OrderBy(entry => entry.Ship.Describe() ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (fit.Count == 0)
        {
            return text + " None of your ships is fitted to mine it.";
        }

        var ships = fit.Take(3).Select(entry => $"{entry.Ship.Describe() ?? "an unnamed ship"} {entry.Fit!.Evidence}");

        text += $" Fitted to mine it: {string.Join("; ", ships)}";
        return text + (fit.Count > 3 ? $"; and {fit.Count - 3} more." : ".");
    }

    private static string Where(string name, int offset) => offset switch
    {
        0 => $"{name}, where the carrier is",
        1 => $"{name}, one jump ahead",
        -1 => $"{name}, one jump behind",
        > 1 => $"{name}, {offset} jumps ahead",
        _ => $"{name}, {-offset} jumps behind",
    };

    private static string Age(TimeSpan age) => age.TotalHours switch
    {
        < 1 => $"{Math.Max(1, (int)age.TotalMinutes)} minutes",
        < 48 => $"{(int)age.TotalHours} hours",
        _ => $"{(int)age.TotalDays} days",
    };
}
