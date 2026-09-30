using D47.Core.Audio;
using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>
/// A marker, during a lull, for the Narrator to tell the Commander's story. The app has the model write the
/// narration; the marker's own text is empty.
/// </summary>
public sealed class NarratorCallout(NearbyFight fight) : ICallout
{
    public string Id => "narrator";

    public const string KeyPrefix = "narrator.";

    public const string Key = KeyPrefix + "story";

    /// <summary>Off means no narration, whatever else is enabled.</summary>
    public Func<bool> Enabled { get; set; } = () => true;

    /// <summary>Whether the character sheet, the backstory or the scenario is set.</summary>
    public Func<bool> HasStory { get; set; } = () => false;

    /// <summary>The shortest gap between two narrations.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>The longest; each gap lands somewhere in [<see cref="Interval"/>, <see cref="Longest"/>].</summary>
    public TimeSpan Longest { get; set; } = TimeSpan.FromMinutes(60);

    /// <summary>How long a situation has to hold before it is narrated.</summary>
    public TimeSpan Settle { get; set; } = TimeSpan.FromSeconds(90);

    private AmbientSituation _situation = AmbientSituation.None;
    private DateTimeOffset _situationSince;
    private DateTimeOffset _lastSpokenAt;
    private int _picks;

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        var situation = AmbientLines.Situate(context.Status);

        if (situation != _situation)
        {
            _situation = situation;
            _situationSince = context.Now;
        }

        // Never in supercruise or hyperspace, which Status.json reports under the same flag, scooping included.
        if (context.IsPriming
            || !Enabled()
            || Interval <= TimeSpan.Zero
            || situation == AmbientSituation.None
            || context.Status.Has(StatusFlags.Supercruise))
        {
            yield break;
        }

        // The first narration of a session waits one full gap.
        if (_lastSpokenAt == default)
        {
            _lastSpokenAt = context.Now;
            yield break;
        }

        if (context.Now - _situationSince < Settle || context.Now - _lastSpokenAt < Gap())
        {
            yield break;
        }

        if (fight.On(context.Now, context.Status)
            || CalloutEngine.ChatterOwesQuiet(context.LastChatter, context.Now, Interval)
            || !HasStory())
        {
            yield break;
        }

        _picks++;
        _lastSpokenAt = context.Now;

        yield return new Announcement(Key, string.Empty)
        {
            Urgency = CalloutUrgency.Routine,
            Cooldown = Interval,
            Chatter = Interval,
            Voice = VoiceRole.Narrator,
            Variant = _picks - 1,
        };
    }

    /// <summary>This cycle's wait, somewhere in [<see cref="Interval"/>, <see cref="Longest"/>].</summary>
    private TimeSpan Gap()
    {
        if (Longest <= Interval)
        {
            return Interval;
        }

        var fraction = unchecked((uint)(_picks + Offset) * 2654435761u) / 4294967296.0;

        return Interval + (Longest - Interval) * fraction;
    }

    /// <summary>Added to the pick count so this callout's gaps differ from the ambient and chatter callouts' gaps.</summary>
    private const int Offset = 2;
}
