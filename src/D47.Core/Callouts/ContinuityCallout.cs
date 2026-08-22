namespace D47.Core.Callouts;

/// <summary>
/// The opening line of a session (list.md Phase 31, "Picking up where you left off").
/// <para>
/// <b>A greeting, and nothing else</b> — amended 2026-08-21 on the Commander's instruction. The
/// line used to carry the gap since they were last seen, the engineer under their feet and the
/// top three items of their list, and the top three was the irritant: <i>"Top of your list: Grade
/// 5 Efficient Weapon on 2F Pulse Laser on Hammer (Type-11 Prospector); then…"</i> is long, it
/// is said before the headset is on, and the Commander's ruling was that if they want to know
/// about their list they will ask. So the line is now the time of day and a readiness, and the
/// list, the gap and the engineer are answered on request rather than announced.
/// </para>
/// <para>
/// <b>Assembled in Core, with no model in the path</b>, and then through the same
/// <see cref="FlavourBriefs"/> path the ambient remarks use — so a core finishes the readiness in
/// its own character and personality-off says it plainly: <i>"Good evening, Commander. Ready to
/// go."</i> The time of day is read off the context's clock, which is the Commander's local one,
/// and never off a clock of this class's own.
/// </para>
/// <para>
/// <b>It is a callout, not an autonomous action</b> — it presses nothing, so it takes the callout
/// family's settings shape, cooldown and precedence rather than a protected row of its own. The same
/// reasoning <see cref="LoreCallout"/> records.
/// </para>
/// </summary>
public sealed class ContinuityCallout : ICallout
{
    public const string Key = "continuity.resume";

    /// <summary>
    /// How long after the first live tick the line waits. Long enough for the journal backlog to
    /// have been folded and for Status.json to have been read at least once, and long enough that
    /// it does not arrive while the Commander is still reading the panel — the reason
    /// <see cref="AmbientCallout"/> seeds its own first interval.
    /// </summary>
    public TimeSpan Settle { get; set; } = TimeSpan.FromSeconds(8);

    private DateTimeOffset _firstLiveTick;
    private bool _said;

    public string Id => "continuity";

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        // Nothing to fold and nothing to say from a backlog. The whole point is the first live
        // moment of a session, and priming is the replay of everything before it.
        if (context.IsPriming)
        {
            yield break;
        }

        if (_firstLiveTick == default)
        {
            _firstLiveTick = context.Now;
            yield break;
        }

        if (_said || context.Now - _firstLiveTick < Settle)
        {
            yield break;
        }

        // Said once per run of d47, whatever happens next.
        _said = true;

        yield return new Announcement(Key, Compose(context.Now))
        {
            // Routine. It is the least urgent thing d47 ever says — the Commander has just sat
            // down — and it stands down for anything that fires on an event.
            Urgency = CalloutUrgency.Routine,

            // Zero, because the once-per-run flag above is the real cooldown and a time-based one
            // would be a second answer to the same question.
            Cooldown = TimeSpan.Zero,
        };
    }

    /// <summary>
    /// The line. Separated from <see cref="Examine"/> so a test can ask for the sentence without
    /// driving eight seconds of ticks past it.
    /// </summary>
    public string Compose(DateTimeOffset now) => $"Good {TimeOfDay(now)}, Commander. Ready to go.";

    /// <summary>
    /// Morning until noon, afternoon until six, evening after — the three a person uses, on the
    /// Commander's own clock. Small hours are an evening: nobody is wished good morning at two.
    /// </summary>
    public static string TimeOfDay(DateTimeOffset now) => now.Hour switch
    {
        >= 5 and < 12 => "morning",
        >= 12 and < 18 => "afternoon",
        _ => "evening",
    };
}
