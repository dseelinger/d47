namespace D47.Core.Callouts;

/// <summary>
/// A marker for each beat of a scene at a settlement, for the people there to react to on their radio. The
/// app has the model write the exchange; the marker's own text is empty.
/// </summary>
public sealed class SceneCallout(SceneTracker tracker) : ICallout
{
    public string Id => "scenes";

    public const string Key = NpcChatter.KeyPrefix + "scene";

    /// <summary>Off means no scene chatter, whatever else is enabled.</summary>
    public Func<bool> Enabled { get; set; } = () => true;

    /// <summary>The Commander's scenario; with none set, no scene exchange is made.</summary>
    public Func<string?> Scenario { get; set; } = () => null;

    /// <summary>The least time between two scene exchanges, except when the Commander is killed.</summary>
    public static readonly TimeSpan Spacing = TimeSpan.FromSeconds(45);

    private int _seenBeats;
    private SceneBeat? _pending;
    private DateTimeOffset? _lastAt;
    private int _scene;
    private int _killsReported;
    private int _picks;

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        var snapshot = tracker.Snapshot;

        if (context.IsPriming)
        {
            _seenBeats = snapshot.Beats;
            _pending = null;
            _scene = snapshot.Last?.Scene ?? 0;
            _killsReported = snapshot.Last?.Kills ?? 0;
            yield break;
        }

        if (snapshot.Beats != _seenBeats && snapshot.Last is { } beat)
        {
            _seenBeats = snapshot.Beats;
            _pending = beat;
        }

        if (!Enabled() || string.IsNullOrWhiteSpace(Scenario()))
        {
            _pending = null;
            yield break;
        }

        if (_pending is not { } next)
        {
            yield break;
        }

        if (next.Kind != SceneBeatKind.CommanderDown)
        {
            // A beat whose scene closed while it waited is dropped, unless it is the escape or the death.
            if (!IsStillHappening(next, snapshot))
            {
                _pending = null;
                yield break;
            }

            if (_lastAt is { } last && context.Now - last < Spacing)
            {
                yield break;
            }
        }

        _pending = null;
        _lastAt = context.Now;

        yield return Marker(Reported(next));
    }

    /// <summary>
    /// Whether a beat is still worth reacting to: its scene is the open one, or it is the Commander's escape
    /// or death, which close the scene.
    /// </summary>
    public static bool IsStillHappening(SceneBeat beat, SceneSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(beat);
        ArgumentNullException.ThrowIfNull(snapshot);

        return beat.Kind is SceneBeatKind.Gone or SceneBeatKind.CommanderDown
            || snapshot.Open && beat.Scene == snapshot.Last?.Scene;
    }

    /// <summary>The beat with the kills since the last exchange of its scene counted in.</summary>
    private SceneBeat Reported(SceneBeat beat)
    {
        if (beat.Scene != _scene)
        {
            _scene = beat.Scene;
            _killsReported = 0;
        }

        var merged = beat.Kills - _killsReported;
        _killsReported = beat.Kills;

        return beat with { Merged = beat.Kind == SceneBeatKind.Down ? merged : 0 };
    }

    // Text empty: the app composes from the marker, and an empty line cannot be spoken by mistake.
    private Announcement Marker(SceneBeat beat) =>
        new(Key, string.Empty)
        {
            Urgency = CalloutUrgency.Routine,
            Variant = _picks++,
            Scene = beat,
        };
}
