using D47.Core.Adventures;
using D47.Core.Journal;

namespace D47.Core.Stories;

/// <summary>
/// What a story chapter is fitted to: the genre's three Save the Cat elements, the long-haul threshold, the chapter
/// size, and the activity a comfort-zone chapter must contain.
/// </summary>
public static class ChapterFit
{
    /// <summary>Blake Snyder's three elements for each genre in <see cref="StoryCard.Genres"/>.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Elements = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
    {
        ["Golden Fleece"] = ["a road", "a team", "a prize"],
        ["Dude with a Problem"] = ["an innocent hero", "a sudden event", "life or death"],
        ["Whydunit"] = ["a detective", "a secret", "a dark turn"],
        ["Buddy Love"] = ["an incomplete hero", "a counterpart", "a complication"],
        ["Rites of Passage"] = ["a life problem", "a wrong way to attack it", "acceptance"],
        ["Fool Triumphant"] = ["a fool", "an establishment", "a transmutation"],
        ["Institutionalized"] = ["a group", "a choice", "a sacrifice"],
        ["Out of the Bottle"] = ["a deserving hero", "a spell", "a lesson"],
        ["Superhero"] = ["a special power", "a nemesis", "a curse"],
    };

    /// <summary>Credits at the last load from which a chapter may go anywhere.</summary>
    public const long LongHaulCredits = 250_000_000;

    /// <summary>The hull that allows a long haul whatever the credits: the Caspian Explorer.</summary>
    public const string LongHaulHull = "explorer_nx";

    /// <summary>The shortest story length a long haul is allowed in.</summary>
    public static readonly StoryPacing LongHaulFrom = StoryPacing.ThreeMonths;

    /// <summary>Every how many chapters after the beacon a chapter must leave the comfort zone.</summary>
    public const int ComfortEvery = 3;

    /// <summary>The activities a comfort-zone chapter picks from, in tie order, each with its <c>Statistics</c> figure.</summary>
    public static readonly IReadOnlyList<(AdventureActivity Activity, string Figure)> Activities =
    [
        (new AdventureActivity("bounty", TriggerKind.Bounty), "Combat.Bounties_Claimed"),
        (new AdventureActivity("bond", TriggerKind.Bond), "Combat.Combat_Bonds"),
        (new AdventureActivity("mine", TriggerKind.Mine), "Mining.Quantity_Mined"),
        (new AdventureActivity("organic", TriggerKind.Organic), "Exobiology.Organic_Data"),
        (new AdventureActivity("rescue", TriggerKind.Rescue), "Search_And_Rescue.SearchRescue_Count"),
        (new AdventureActivity("passenger mission", TriggerKind.Mission, "Mission_Passenger"), "Passengers.Passengers_Missions_Delivered"),
    ];

    /// <summary>Whether a chapter of a story this long may go anywhere, for these credits and this game state.</summary>
    public static bool IsLongHaul(StoryPacing pacing, CommanderGameState? state)
    {
        ArgumentNullException.ThrowIfNull(pacing);

        return pacing.FinaleDay >= LongHaulFrom.FinaleDay
               && state is not null
               && (state.Session.Balance >= LongHaulCredits
                   || string.Equals(state.Ship.Type, LongHaulHull, StringComparison.OrdinalIgnoreCase)
                   || state.Fleet.Ships.Any(ship => string.Equals(ship.Type, LongHaulHull, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>The chapter size the writer is given for a story this long.</summary>
    public static string Size(StoryPacing pacing)
    {
        ArgumentNullException.ThrowIfNull(pacing);

        return pacing.FinaleDay < StoryPacing.ThreeMonths.FinaleDay ? "one play session" : "one to three play sessions";
    }

    /// <summary>Whether this chapter, counted from the first after the beacon scan, must leave the comfort zone.</summary>
    public static bool IsComfortChapter(int sinceBeacon) => sinceBeacon > 0 && sinceBeacon % ComfortEvery == 0;

    /// <summary>The activity with the lowest figure that is not refused, ties to the earlier row; null with no <c>Statistics</c> event yet.</summary>
    public static AdventureActivity? LeastDone(CareerStatistics statistics, IReadOnlyList<string>? refused = null)
    {
        ArgumentNullException.ThrowIfNull(statistics);

        if (!statistics.IsKnown)
        {
            return null;
        }

        AdventureActivity? least = null;
        var lowest = double.MaxValue;

        foreach (var (activity, figure) in Activities)
        {
            if (RefusedActivities.Refuses(refused, activity.Kind, activity.MissionFamily))
            {
                continue;
            }

            var value = statistics.Read(figure) ?? 0;

            if (value < lowest)
            {
                least = activity;
                lowest = value;
            }
        }

        return least;
    }
}
