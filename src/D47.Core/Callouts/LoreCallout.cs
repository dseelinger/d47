using D47.Core.Journal;
using D47.Core.Lore;

namespace D47.Core.Callouts;

/// <summary>
/// How much of the lore remark a Commander wants (Phase 23, "Look it up, and say where
/// the answer came from").
/// <para>
/// <b>Three states rather than two independent switches</b>, because a lookup with the remark off
/// is detail about something that was never announced.
/// </para>
/// </summary>
public enum LoreRemarks
{
    /// <summary>Nothing is said on arrival. The book is still there to be asked about.</summary>
    Off,

    /// <summary>The bare fact the book holds, and nothing further.</summary>
    Remark,

    /// <summary>
    /// The fact, and then whatever a web search turns up about the place. Named for the part that
    /// distinguishes it — it is the remark <em>and</em> the lookup, never the lookup alone.
    /// </summary>
    Lookup,
}

/// <summary>
/// Something about where you have just arrived, when there is something to say (Phase 23,
/// "Remark on arrival, and not again today").
/// <para>
/// <b>Once per system per 24 hours</b>, which is the whole difference between a companion and a
/// tour guide who has forgotten meeting you. Measured rather than guessed: across the 913-journal
/// corpus, 30.1% of all 7,966 jumps re-enter a system visited inside a day — so without the rule
/// nearly a third of arrivals would be a repeat. Widening it to a week would suppress only 4.2
/// points more, because 88% of all repeat visits happen within the first day.
/// </para>
/// <para>
/// <b>It is a callout, not an autonomous action.</b> <c>AutonomousCapability</c> is one protected
/// row per action because an action that fires a game input has no sentence of consent behind it.
/// This presses nothing, so it takes the callouts' settings shape instead.
/// </para>
/// <para>
/// <b>Carrier jumps count as arrivals too.</b> The checklist says <c>FSDJump</c>, which is where
/// the interesting case is; <c>CarrierJump</c> is the same event with the Commander asleep in the
/// back, and a Commander whose carrier has just put them in Colonia is exactly the person who
/// wants to be told about it. Both carry <c>SystemAddress</c> and <c>StarSystem</c>, so this
/// costs one string in a comparison.
/// </para>
/// </summary>
public sealed class LoreCallout(LoreBook book, LoreVisits visits) : ICallout
{
    public const string KeyPrefix = "lore.";

    /// <summary>The two events that mean "the Commander is somewhere new".</summary>
    private static readonly string[] Arrivals = ["FSDJump", "CarrierJump"];

    public string Id => "lore";

    /// <summary>
    /// How much to say. Read per tick rather than held, because a settings change applies to the
    /// next arrival rather than the next session.
    /// </summary>
    public Func<LoreRemarks> Remarks { get; set; } = () => LoreRemarks.Lookup;

    /// <summary>
    /// How long a system stays quiet after being remarked on. Settable so the replay harness and
    /// the tests can state the window they are testing rather than sleeping through it.
    /// </summary>
    public TimeSpan Window { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Which system a remark was about, from its key — what the app needs to run the lookup for
    /// it. Carried on the key rather than read back off the callout, because a batch may hold
    /// more than one and a property would only remember the last (the trap
    /// <see cref="FlavourBriefs.SituationOf"/> already documents).
    /// </summary>
    public static long? AddressOf(string key) =>
        key.StartsWith(KeyPrefix, StringComparison.Ordinal)
        && long.TryParse(key[KeyPrefix.Length..], out var address)
            ? address
            : null;

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        // Nothing to fold. This reacts to an arrival event and to nothing else, so a backlog
        // leaves it with nothing to say and nothing to remember — unlike the trackers, whose
        // whole difficulty is priming.
        if (context.IsPriming || Remarks() == LoreRemarks.Off)
        {
            yield break;
        }

        foreach (var journalEvent in context.Events)
        {
            if (!Arrivals.Contains(journalEvent.Kind)
                || journalEvent.Long("SystemAddress") is not { } address)
            {
                continue;
            }

            // Absolute, and compared against the tick's own time. An elapsed figure written to
            // disk would start lying the moment the app exited, and would make "24 hours" mean
            // fourteen minutes under the replay harness.
            if (visits.LastSpokenAt(address) is { } last && context.Now - last < Window)
            {
                continue;
            }

            var entries = book.For(address);

            if (entries.Count == 0)
            {
                continue;
            }

            visits.Record(address, context.Now);

            yield return new Announcement(
                $"{KeyPrefix}{address}",
                string.Join(" ", entries.Select(entry => entry.Spoken())))
            {
                // Routine, and it never silences anything. A remark about a crash site arriving
                // over the top of an interdiction warning would be the worst possible trade.
                Urgency = CalloutUrgency.Routine,

                // The 24-hour rule is the cooldown, and it lives in the visit store rather than
                // in the engine's table: the engine's is per session, and this one has to survive
                // a restart or logging off in a system means hearing about it again on return.
                Cooldown = TimeSpan.Zero,
            };
        }
    }
}
