namespace D47.Core.Callouts;

/// <summary>The opening line of a session (Phase 31, "Picking up where you left off").</summary>
public sealed class ContinuityCallout : ICallout
{
    public const string Key = "continuity.resume";

    /// <summary>How long after the first live tick the line waits.</summary>
    public TimeSpan Settle { get; set; } = TimeSpan.FromSeconds(8);

    private DateTimeOffset _firstLiveTick;
    private bool _said;

    /// <summary>
    /// Whether the line to come is answering a login rather than a launch, and so names the Commander.
    /// </summary>
    private bool _switched;

    public string Id => "continuity";

    /// <summary>
    /// Makes the line due again, for a Commander who has just logged in where another was (Phase 44,
    /// "Welcome back, Commander").
    /// </summary>
    public void Rearm()
    {
        _said = false;
        _switched = true;
        _firstLiveTick = default;
    }

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        // Nothing to fold and nothing to say from a backlog.
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

        // Said once per session, whatever happens next — and a session starts at launch and at a login by
        // somebody else, which is what Rearm is.
        _said = true;

        yield return new Announcement(Key, Compose(context.Now, _switched ? context.State?.Identity.Name : null))
        {
            // Routine.
            Urgency = CalloutUrgency.Routine,

            // Zero, because the once-per-run flag above is the real cooldown and a time-based one would be a
            // second answer to the same question.
            Cooldown = TimeSpan.Zero,
        };
    }

    /// <summary>The line.</summary>
    public string Compose(DateTimeOffset now) => Compose(now, null);

    /// <summary>
    /// The line, naming the Commander where there is one to name — after a login, not at launch (see
    /// <see cref="Rearm"/>).
    /// </summary>
    public string Compose(DateTimeOffset now, string? name) =>
        $"Good {TimeOfDay(now)}, {Journal.CommanderAddress.Said(name)}. Ready to go.";

    /// <summary>
    /// Morning until noon, afternoon until six, evening after — the three a person uses, on the
    /// Commander's own clock.
    /// </summary>
    public static string TimeOfDay(DateTimeOffset now) => now.Hour switch
    {
        >= 5 and < 12 => "morning",
        >= 12 and < 18 => "afternoon",
        _ => "evening",
    };
}
