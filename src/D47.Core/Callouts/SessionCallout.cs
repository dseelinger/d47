using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>
/// A word when the Commander gets into a game, and one when they leave it
/// (docs/plans/change-requests.md item 29).
/// </summary>
public sealed class SessionCallout : ICallout
{
    public string Id => "session";

    public const string KeyPrefix = "session.";

    public const string ArrivedKey = KeyPrefix + "arrived";

    public const string LeftKey = KeyPrefix + "left";

    /// <summary>Off means neither line, whatever else is enabled.</summary>
    public Func<bool> Enabled { get; set; } = () => true;

    /// <summary>The shortest gap between two of the same reaction.</summary>
    public TimeSpan Cooldown { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>When each direction was last said, per Commander.</summary>
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

                // Elite writes this on a clean exit and on no other.
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
    /// The announcement for this direction, or null when it is too soon — and either way the backlog is
    /// folded rather than announced.
    /// </summary>
    private Announcement? Due(Dictionary<string, DateTimeOffset> clock, CalloutContext context)
    {
        // Priming replays the whole journal.
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
            // Routine, like the resume line: the Commander has just sat down or is standing up, and anything
            // that fires on an event outranks it.
            Urgency = CalloutUrgency.Routine,

            // Zero, because the per-direction clock above is the real cooldown and a second time-based one
            // would be two answers to one question.
            Cooldown = TimeSpan.Zero,
        };
    }
}
