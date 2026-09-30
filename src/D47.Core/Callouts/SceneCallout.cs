using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>
/// A marker for each beat of a scene, at a settlement or in a ship fight, and for each merged mission beat, for
/// the people there or of the mission's faction to react to on their radio. The app has the model write the exchange; the marker's own text is empty.
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
    private readonly MissionBeats _missions = new();

    /// <summary>Whether a ship scene is open and would be heard, so no timed combat exchange is made.</summary>
    public bool HoldsTheFight => tracker.Snapshot.InShip && Enabled() && !string.IsNullOrWhiteSpace(Scenario());

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        var snapshot = tracker.Snapshot;

        if (context.IsPriming)
        {
            _seenBeats = snapshot.Beats;
            _pending = null;
            _scene = snapshot.Last?.Scene ?? 0;
            _killsReported = snapshot.Last?.Kills ?? 0;
            _missions.Prime(context.State?.Missions ?? MissionBoard.Empty);
            yield break;
        }

        var missionBeats = _missions.Due(context.Now);
        _missions.Fold(context.Now, context.Events, context.State?.Missions ?? MissionBoard.Empty);

        if (snapshot.Beats != _seenBeats && snapshot.Last is { } beat)
        {
            _seenBeats = snapshot.Beats;
            _pending = beat;
        }

        if (!Enabled() || string.IsNullOrWhiteSpace(Scenario()))
        {
            _pending = null;
            _missions.Clear();
            yield break;
        }

        foreach (var missionBeat in missionBeats)
        {
            yield return Marker(missionBeat);
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

        yield return Marker(Reported(next, context.State?.Missions));
    }

    /// <summary>
    /// Whether a beat is still worth reacting to: its scene is the open one, it is the Commander's escape
    /// or death, which close the scene, or it is a mission beat, which has no scene.
    /// </summary>
    public static bool IsStillHappening(SceneBeat beat, SceneSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(beat);
        ArgumentNullException.ThrowIfNull(snapshot);

        return beat.Place == ScenePlace.Mission
            || beat.Kind is SceneBeatKind.Gone or SceneBeatKind.CommanderDown
            || snapshot.Open && beat.Scene == snapshot.Last?.Scene;
    }

    /// <summary>
    /// The beat with the kills since the last exchange of its scene counted in, and at a settlement the live
    /// missions concerning it.
    /// </summary>
    private SceneBeat Reported(SceneBeat beat, MissionBoard? board)
    {
        if (beat.Scene != _scene)
        {
            _scene = beat.Scene;
            _killsReported = 0;
        }

        var merged = beat.Kills - _killsReported;
        _killsReported = beat.Kills;

        var missions = beat.Place == ScenePlace.Settlement && board is not null
            ? board.Concerning(beat.Settlement, beat.Faction)
            : [];

        return beat with
        {
            Merged = beat.Kind is SceneBeatKind.Down or SceneBeatKind.ShipDown ? merged : 0,
            Missions = missions.Count > 0 ? missions : null,
        };
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
