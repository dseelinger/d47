using D47.Core.Knowledge;

namespace D47.Core.Journal;

/// <summary>Whether a mission that names a station can be handed in there now.</summary>
public static class MissionReadiness
{
    private static readonly HashSet<string> CargoFamilies = new(StringComparer.OrdinalIgnoreCase)
    {
        "Mission_Collect", "Mission_Delivery", "Mission_Altruism", "Mission_Mining", "Mission_Piracy", "Mission_Smuggle",
    };

    private static readonly HashSet<string> ArrivalFamilies = new(StringComparer.OrdinalIgnoreCase)
    {
        "Mission_Courier", "Mission_PassengerBulk", "Mission_PassengerVIP", "Mission_RS_PassengerBulk",
        "Mission_DS_PassengerBulk", "Mission_LongDistanceExpedition",
    };

    /// <summary>The tonnes of its commodity a mission still owes: <c>Count</c> less what was delivered; 0 where unknown.</summary>
    public static int Owed(Mission mission) =>
        mission is { Commodity: { Length: > 0 }, Count: > 0 and var count }
            ? Math.Max(0, count - (mission.Cargo?.Delivered ?? 0))
            : 0;

    /// <summary>The missions among <paramref name="candidates"/> that can be handed in now, in the order given.</summary>
    /// <remarks>The hold is shared out across cargo missions for one commodity, soonest expiry first.</remarks>
    public static IReadOnlyList<Mission> Ready(IEnumerable<Mission> candidates, CargoHold hold)
    {
        var list = candidates.ToList();
        var ready = new HashSet<long>();
        var left = new Dictionary<string, int>();

        foreach (var mission in list.OrderBy(mission => mission.Expiry ?? DateTimeOffset.MaxValue).ThenBy(mission => mission.Id))
        {
            if (mission.Redirected)
            {
                ready.Add(mission.Id);
                continue;
            }

            var family = MissionCatalogue.Find(mission.Name)?.Family;

            if (family is null)
            {
                continue;
            }

            if (ArrivalFamilies.Contains(family))
            {
                ready.Add(mission.Id);
            }
            else if (CargoFamilies.Contains(family) && hold is { IsKnown: true, IsShip: true } && Owed(mission) is > 0 and var owed)
            {
                var commodity = mission.Commodity!;
                var available = left.TryGetValue(commodity, out var remaining) ? remaining : hold.Of(commodity);

                if (available >= owed)
                {
                    left[commodity] = available - owed;
                    ready.Add(mission.Id);
                }
            }
        }

        return [.. list.Where(mission => ready.Contains(mission.Id))];
    }

    /// <summary>The missions that name <paramref name="station"/> and can be handed in there now.</summary>
    public static IReadOnlyList<Mission> ReadyAt(MissionBoard board, string? station, CargoHold hold) =>
        station is { Length: > 0 }
            ? Ready(board.Missions.Where(mission => string.Equals(mission.DestinationStation, station, StringComparison.OrdinalIgnoreCase)), hold)
            : [];
}
