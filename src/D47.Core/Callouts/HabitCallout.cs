using D47.Core.Habits;
using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>
/// The one thing a Commander experiences out of Phase 32 (Phase 32, "Callouts that are
/// yours rather than everyone's").
/// <para>
/// <b>It fires because of the situation, not at the start of a session.</b>
/// <see cref="ContinuityCallout"/> already owns the opening line, and a second line arriving beside
/// it is exactly the "second voice competing with the first" the item forbids. So each claim
/// declares a <see cref="HabitOccasion"/> and this maps the tick's events onto it: dropping out at a
/// station, entering orbital cruise, being interdicted, walking up to a settlement.
/// </para>
/// <para>
/// <b>The bar is higher than for a generic warning, and it is met five separate ways.</b> A shipped
/// callout fires because the game said something; this one fires because of a claim d47 made about
/// the Commander, so:
/// <list type="number">
/// <item>it is <b>off by default</b>, because a companion that starts commenting on your flying
/// without being asked has changed the deal;</item>
/// <item>a claim must clear all three of <see cref="HabitFloor"/> before it exists at all;</item>
/// <item>and clear <see cref="HabitFloor.Rate"/> on top of that before it is <em>said</em>, which
/// is the fifth and the newest — sound arithmetic about something a Commander does one time in
/// thirty-five is still not a habit (remediation.md 17, item 12);</item>
/// <item>each claim has a <see cref="PerClaim"/> cooldown of hours, because a line repeated on
/// every approach is nagging rather than a habit report;</item>
/// <item>and every claim can be refused for good, in one phrase, from the moment it is said.</item>
/// </list>
/// </para>
/// <para>
/// <b>It says why it fired if asked.</b> The claim is handed to <see cref="HabitBook.Spoke"/> as it
/// goes out, which is what makes <em>why did you say that</em> and <em>stop telling me that</em>
/// answerable with no argument and therefore reachable through the model-free router.
/// </para>
/// </summary>
public sealed class HabitCallout(HabitBook book) : ICallout
{
    /// <summary>The prefix every one of these announces under. One per claim, so cooldowns are per claim.</summary>
    public const string KeyPrefix = "habit.";

    /// <summary>
    /// How long one claim stays quiet after being said. Hours rather than minutes: the point is to
    /// be right, and a Commander who hears the same observation on every approach stops hearing it.
    /// </summary>
    public static readonly TimeSpan PerClaim = TimeSpan.FromHours(4);

    /// <summary>
    /// How long after any habit line before another may follow, whatever it is about. Two of these
    /// in a row is a lecture.
    /// </summary>
    public static readonly TimeSpan BetweenAny = TimeSpan.FromMinutes(20);

    private DateTimeOffset _saidAnythingAt = DateTimeOffset.MinValue;

    public string Id => "habits";

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        // Nothing is said from the backlog. Every occasion here is an event, and priming replays
        // hours of them — the rule every event-driven callout in this folder follows.
        if (context.IsPriming || context.Events.Count == 0)
        {
            yield break;
        }

        if (context.Now - _saidAnythingAt < BetweenAny)
        {
            yield break;
        }

        var claims = book.Claims;

        if (claims.Count == 0)
        {
            yield break;
        }

        foreach (var journalEvent in context.Events)
        {
            if (OccasionOf(journalEvent) is not { } occasion)
            {
                continue;
            }

            // Common enough to be worth interrupting for, which the counts alone did not test
            // (remediation.md 17, item 12). A claim below the rate is still a claim — it is on the
            // Habits page and answers a question asked of it — and is simply never volunteered.
            var claim = claims.FirstOrDefault(candidate =>
                candidate.Occasion == occasion && candidate.Evidence.WorthSaying);

            if (claim is null)
            {
                continue;
            }

            _saidAnythingAt = context.Now;
            book.Spoke(claim);

            yield return new Announcement(KeyPrefix + claim.Key, claim.Spoken())
            {
                // Routine, always. It is an observation about the Commander rather than about the
                // world, and nothing d47 worked out about somebody outranks something the game
                // just said.
                Urgency = CalloutUrgency.Routine,
                Cooldown = PerClaim,
            };

            yield break;
        }
    }

    /// <summary>
    /// Which circumstance an event is, or null for the overwhelming majority that are none of them.
    /// <para>
    /// The mapping is deliberately narrow. Every occasion is a moment the Commander is about to do
    /// the thing the claim is about — not a moment that merely relates to it — because a personal
    /// callout arriving at the wrong time reads as d47 having misunderstood, which costs the claim
    /// its credibility whether or not the claim was true.
    /// </para>
    /// </summary>
    public static HabitOccasion? OccasionOf(JournalEvent journalEvent)
    {
        ArgumentNullException.ThrowIfNull(journalEvent);

        return journalEvent.Kind switch
        {
            "SupercruiseExit" when journalEvent.Raw.String("BodyType") is "Station"
                => HabitOccasion.ArrivingAtAStation,
            "ApproachBody" => HabitOccasion.ApproachingAPlanet,
            "Interdicted" => HabitOccasion.BeingInterdicted,
            "ApproachSettlement" => HabitOccasion.ArrivingOnFoot,
            "Touchdown" => HabitOccasion.TouchingDown,
            _ => null,
        };
    }
}
