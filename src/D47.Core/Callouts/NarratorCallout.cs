using D47.Core.Adventures;
using D47.Core.Audio;
using D47.Core.Journal;
using D47.Core.Stories;

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

    /// <summary>A narration that leans toward a stalled adventure's next beat; the adventure's key follows.</summary>
    public const string NudgePrefix = KeyPrefix + "nudge.";

    /// <summary>Off means no narration, whatever else is enabled.</summary>
    public Func<bool> Enabled { get; set; } = () => true;

    /// <summary>Whether the character sheet, the backstory or the scenario is set.</summary>
    public Func<bool> HasStory { get; set; } = () => false;

    /// <summary>Whether a stock story is running and switched on for the active Commander.</summary>
    public Func<bool> StoryRunning { get; set; } = () => false;

    /// <summary>The running story's asides, or null when stories are not wired.</summary>
    public StoryMissionAsides? StoryAsides { get; set; }

    /// <summary>The adventures under way, checked for one stalled at its next beat.</summary>
    public Func<IReadOnlyList<AdventureStanding>> Adventures { get; set; } = () => [];

    /// <summary>The shortest gap between two narrations; zero silences the Narrator, stock core or not.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>The longest; each gap lands somewhere in [<see cref="Interval"/>, <see cref="Longest"/>].</summary>
    public TimeSpan Longest { get; set; } = TimeSpan.FromMinutes(60);

    /// <summary>Whether the core aboard is a stock core. The Narrator then narrates whether or not there is a story.</summary>
    public Func<bool> StockCoreAboard { get; set; } = () => false;

    /// <summary>How long a situation has to hold before it is narrated.</summary>
    public TimeSpan Settle { get; set; } = TimeSpan.FromSeconds(90);

    private AmbientSituation _situation = AmbientSituation.None;
    private DateTimeOffset _situationSince;
    private DateTimeOffset _lastSpokenAt;
    private int _picks;
    private readonly HashSet<string> _nudged = new(StringComparer.OrdinalIgnoreCase);

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
            || CalloutEngine.ChatterOwesQuiet(context.LastChatter, context.Now, Interval))
        {
            yield break;
        }

        var stalled = Adventures().FirstOrDefault(standing =>
            !_nudged.Contains(standing.Adventure.Key) && AdventureNudge.IsDue(standing, context.Now));

        if (stalled is null && !StockCoreAboard() && !HasStory() && !StoryRunning())
        {
            yield break;
        }

        _picks++;
        _lastSpokenAt = context.Now;

        if (stalled is not null)
        {
            _nudged.Add(stalled.Adventure.Key);
        }

        yield return new Announcement(
            stalled is null ? Key : NudgePrefix + stalled.Adventure.Key,
            stalled is null ? string.Empty : AdventureNudge.Facts(stalled, lightYears: null))
        {
            StoryAside = stalled is null ? Untold(context.State?.Missions) : null,
            Urgency = CalloutUrgency.Routine,
            Cooldown = Interval,
            Chatter = Interval,
            Voice = VoiceRole.Narrator,
            Variant = _picks - 1,
        };
    }

    /// <summary>The story's aside for the newest mission on the board that has not had one, or null.</summary>
    private MissionAside? Untold(MissionBoard? board) =>
        StoryAsides is { } asides && board?.Missions
            .Where(mission => !asides.Told(mission.Id))
            .MaxBy(mission => mission.AcceptedAt) is { } newest
            ? asides.Take(newest)
            : null;

    /// <summary>The adventure a nudge is about, or null when the key is not a nudge.</summary>
    public static string? Nudged(string? key) =>
        key is not null && key.StartsWith(NudgePrefix, StringComparison.Ordinal) && key.Length > NudgePrefix.Length
            ? key[NudgePrefix.Length..]
            : null;

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
