using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>
/// A word when the Commander gets into a game, and one when they leave it
/// (docs/plans/change-requests.md item 29).
/// <para>
/// <b>Not the line <see cref="ContinuityCallout"/> says.</b> That one greets when <em>d47</em>
/// starts and is said once per run of the app. This is about the <em>game</em>, which d47 can be
/// running either side of and which a Commander enters and leaves several times an evening.
/// </para>
/// <para>
/// <b>Leaving is not always visible, and that is measured rather than assumed.</b> Across the
/// 925-journal corpus on 2026-08-23, <c>Shutdown</c> appears in 841 of them — so roughly one
/// session in eleven ends with a crash or a kill and says nothing at all. The departure line is
/// therefore allowed simply not to happen. Nothing here reconstructs a departure from silence: a
/// timeout that guessed at one would eventually say goodbye to a Commander who is still flying.
/// </para>
/// <para>
/// <b>The cooldown is the point of the request, and the corpus picked its default.</b> Of 433
/// consecutive <c>LoadGame</c> pairs, 57% are less than thirty minutes apart and the median gap is
/// 21.2 minutes — so thirty minutes suppresses the majority of re-logs while still answering a gap
/// that is a real return. It is a property rather than a constant for the reason
/// <see cref="AmbientCallout.Interval"/> is one.
/// </para>
/// </summary>
public sealed class SessionCallout : ICallout
{
    public string Id => "session";

    public const string KeyPrefix = "session.";

    public const string ArrivedKey = KeyPrefix + "arrived";

    public const string LeftKey = KeyPrefix + "left";

    /// <summary>Off means neither line, whatever else is enabled.</summary>
    public Func<bool> Enabled { get; set; } = () => true;

    /// <summary>
    /// The shortest gap between two of the same reaction. Zero says both every time, which is the
    /// behaviour the request exists to prevent and is still a Commander's to choose.
    /// </summary>
    public TimeSpan Cooldown { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// When each direction was last said, per Commander.
    /// <para>
    /// <b>Per Commander, because a second Commander logging in is arriving for the first time</b>
    /// rather than re-logging, and sharing one clock between them would silence the one who
    /// happened to come second.
    /// </para>
    /// </summary>
    private readonly Dictionary<string, DateTimeOffset> _arrived = new(StringComparer.Ordinal);

    private readonly Dictionary<string, DateTimeOffset> _left = new(StringComparer.Ordinal);

    /// <summary>Whose game this is, so a <c>Shutdown</c> — which carries no identity — has an owner.</summary>
    private string _who = string.Empty;

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        foreach (var journalEvent in context.Events)
        {
            switch (journalEvent.Kind)
            {
                case "LoadGame":
                    _who = journalEvent.String("FID") ?? _who;
                    if (Due(_arrived, context) is { } arrival)
                    {
                        yield return arrival with { Text = "Welcome back, Commander." };
                    }

                    break;

                // Elite writes this on a clean exit and on no other. It carries no FID, so the
                // owner is whoever last loaded — which is why _who is kept across the switch.
                case "Shutdown":
                    if (Due(_left, context) is { } departure)
                    {
                        yield return departure with
                        {
                            Key = LeftKey,
                            Text = "Ship secured. I will be here.",
                        };
                    }

                    break;
            }
        }
    }

    /// <summary>
    /// The announcement for this direction, or null when it is too soon — and either way the
    /// backlog is folded rather than announced.
    /// <para>
    /// <b>The clock only moves when something is said.</b> Stamping it on a suppressed reaction
    /// would restart the cooldown on every re-log, so a Commander bouncing in and out every ten
    /// minutes would never hear the line again however long they eventually stayed away.
    /// </para>
    /// </summary>
    private Announcement? Due(Dictionary<string, DateTimeOffset> clock, CalloutContext context)
    {
        // Priming replays the whole journal. Without this, launching d47 after an evening's
        // flying announces an arrival that happened four hours ago — the trap this context field
        // exists for.
        if (context.IsPriming)
        {
            clock[_who] = context.Now;
            return null;
        }

        if (!Enabled())
        {
            return null;
        }

        if (Cooldown > TimeSpan.Zero
            && clock.TryGetValue(_who, out var last)
            && context.Now - last < Cooldown)
        {
            return null;
        }

        clock[_who] = context.Now;

        return new Announcement(ArrivedKey, string.Empty)
        {
            // Routine, like the resume line: the Commander has just sat down or is standing up,
            // and anything that fires on an event outranks it.
            Urgency = CalloutUrgency.Routine,

            // Zero, because the per-direction clock above is the real cooldown and a second
            // time-based one would be two answers to one question.
            Cooldown = TimeSpan.Zero,
        };
    }
}
