namespace D47.Core.Journal;

/// <summary>The colonisation deadlines and the haul figure, as the maintainer checked them.</summary>
public static class ColonisationRules
{
    /// <summary>The date the rules below were last checked against the game.</summary>
    public const string CheckedOn = "2026-09-27";

    /// <summary>From the claim to deploying the beacon.</summary>
    public static readonly TimeSpan BeaconWindow = TimeSpan.FromHours(24);

    /// <summary>From the claim to finishing the system's primary port.</summary>
    public static readonly TimeSpan PrimaryPortWindow = TimeSpan.FromDays(28);

    /// <summary>The exclusive claim window of a Commander in no squadron.</summary>
    public static readonly TimeSpan ExclusiveClaimWindow = TimeSpan.FromMinutes(30);

    /// <summary>The exclusive claim window of a Commander in a squadron, even of one.</summary>
    public static readonly TimeSpan SquadronExclusiveClaimWindow = TimeSpan.FromDays(1);

    /// <summary>How long before the beacon deadline the reminder is said.</summary>
    public static readonly TimeSpan BeaconReminder = TimeSpan.FromHours(2);

    /// <summary>How long before the primary port deadline each reminder is said, longest first.</summary>
    public static readonly IReadOnlyList<TimeSpan> PrimaryPortReminders = [TimeSpan.FromDays(7), TimeSpan.FromHours(48)];

    /// <summary>Tonnes still to deliver to <paramref name="site"/>, and the loads that takes in a hold of <paramref name="capacity"/>.</summary>
    public static (int Remaining, int Trips) Haul(ConstructionSite site, int capacity)
    {
        ArgumentNullException.ThrowIfNull(site);

        var remaining = site.Resources.Sum(resource => resource.Remaining);

        return (remaining, capacity > 0 ? (remaining + capacity - 1) / capacity : 0);
    }

    /// <summary>The haul as said aloud, or null with nothing left or no hold size.</summary>
    public static string? HaulSentence(ConstructionSite site, int? capacity)
    {
        if (capacity is null or <= 0)
        {
            return null;
        }

        var (remaining, trips) = Haul(site, capacity.Value);

        return remaining <= 0
            ? null
            : $"{remaining} tonnes to go: about {trips} {(trips == 1 ? "trip" : "trips")} in this ship";
    }
}
