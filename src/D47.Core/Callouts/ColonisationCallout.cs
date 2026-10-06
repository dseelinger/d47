using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>The colonisation deadlines at a claim and as they approach, the squadron rule, and the haul to a site (#580).</summary>
public sealed class ColonisationCallout : ICallout
{
    public string Id => "colonisation";

    public const string Key = "colonisation";

    private readonly HashSet<string> _spoken = [];

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        if (context.IsPriming || context.State is not { } state)
        {
            yield break;
        }

        foreach (var journalEvent in context.Events)
        {
            if (journalEvent.Kind == "ColonisationSystemClaim"
                && journalEvent.String("StarSystem") is { Length: > 0 } system)
            {
                yield return Say(ClaimLine(system, state.Squadron.IsMember));
            }
            else if (journalEvent.Kind == "ColonisationConstructionDepot"
                && journalEvent.Long("MarketID") is { } marketId
                && state.Colonisation.ById(marketId) is { } site
                && _spoken.Add($"{state.Identity.FrontierId}|haul|{marketId}")
                && ColonisationRules.HaulSentence(site, state.Ship.CargoCapacity) is { } haul)
            {
                yield return Say($"{haul}.");
            }
        }

        foreach (var claim in state.Colonisation.Claims)
        {
            if (Reminder(state, claim, context.Now) is { } reminder)
            {
                yield return Say(reminder);
            }
        }
    }

    private string? Reminder(CommanderGameState state, ColonisationClaim claim, DateTimeOffset now)
    {
        var owner = $"{state.Identity.FrontierId}|{claim.StarSystem}|{claim.ClaimedAt:O}";

        if (claim.BeaconDeployedAt is null)
        {
            var left = claim.ClaimedAt + ColonisationRules.BeaconWindow - now;

            if (left > TimeSpan.Zero
                && left <= ColonisationRules.BeaconReminder
                && _spoken.Add($"{owner}|beacon"))
            {
                return $"You have {Left(left)} left to deploy the beacon in {claim.StarSystem}.";
            }
        }

        if (claim.FirstSiteMarketId is { } first && state.Colonisation.ById(first)?.Complete == true)
        {
            return null;
        }

        var remaining = claim.ClaimedAt + ColonisationRules.PrimaryPortWindow - now;

        if (remaining <= TimeSpan.Zero)
        {
            return null;
        }

        // A moment that passed while d47 was off is covered by the latest one reached: the earlier reminders are marked said.
        var reached = ColonisationRules.PrimaryPortReminders.Where(lead => remaining <= lead).ToList();
        var fresh = false;

        foreach (var lead in reached)
        {
            fresh |= _spoken.Add($"{owner}|port|{lead.TotalHours}");
        }

        return fresh
            ? $"The primary port in {claim.StarSystem} has {Left(remaining)} left to finish."
            : null;
    }

    private static string ClaimLine(string system, bool inSquadron)
    {
        var line = $"You have {Span(ColonisationRules.BeaconWindow, inHours: true)} to deploy the beacon in {system}, "
            + $"and {Span(ColonisationRules.PrimaryPortWindow)} to finish its primary port.";

        return inSquadron
            ? line
            : $"{line} A squadron of your own, even of one, extends the exclusive claim window from "
                + $"{Span(ColonisationRules.ExclusiveClaimWindow)} to {Span(ColonisationRules.SquadronExclusiveClaimWindow)}.";
    }

    private static readonly Dictionary<int, string> Words = new()
    {
        [2] = "two", [3] = "three", [4] = "four", [5] = "five", [6] = "six", [7] = "seven", [30] = "thirty",
    };

    private static string Count(int n) => Words.GetValueOrDefault(n) ?? n.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>A span as said aloud: weeks, then days, then hours, then minutes, using the largest unit that divides it evenly.</summary>
    private static string Span(TimeSpan span, bool inHours = false)
    {
        if (!inHours && span.Ticks % TimeSpan.TicksPerDay == 0)
        {
            var days = (int)span.TotalDays;

            if (days % 7 == 0)
            {
                var weeks = days / 7;
                return weeks == 1 ? "a week" : $"{Count(weeks)} weeks";
            }

            return days == 1 ? "a day" : $"{Count(days)} days";
        }

        if (span.Ticks % TimeSpan.TicksPerHour == 0)
        {
            var hours = (int)span.TotalHours;
            return hours == 1 ? "an hour" : $"{hours} hours";
        }

        var minutes = (int)Math.Round(span.TotalMinutes);

        return minutes == 1 ? "a minute" : $"{Count(minutes)} minutes";
    }

    /// <summary>Time still to run, rounded up to whole hours within 48 hours and to whole days beyond.</summary>
    private static string Left(TimeSpan span) => span <= TimeSpan.FromHours(48)
        ? Span(TimeSpan.FromHours(Math.Ceiling(span.TotalHours)), inHours: true)
        : $"{(int)Math.Ceiling(span.TotalDays)} days";

    private static Announcement Say(string text) =>
        new(Key, text) { Cooldown = TimeSpan.Zero };
}
