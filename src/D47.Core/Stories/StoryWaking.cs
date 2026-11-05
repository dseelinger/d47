using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Persona;

namespace D47.Core.Stories;

/// <summary>
/// The story's part of the adventures tick: beacon scans and stepped beacons queue their wakings, a picked story's
/// opening and narrated scan are said, and each waking is handed on once the line owed ahead of it has been said.
/// Not thread-safe: one caller, the tick.
/// </summary>
public sealed class StoryWaking(
    StoryDirector storyDirector,
    StoryStore storyStore,
    AdventureBook adventureBook,
    GameStateStore gameState,
    IStoryWakingVoice voice)
{
    /// <summary>What a waking held behind a narrated scan line waits on, in place of a chapter key.</summary>
    public const string NarratedScanKey = "story.scan";

    /// <summary>What a waking held behind a story's opening lines waits on, in place of a chapter key.</summary>
    public const string OpeningKey = "story.opening";

    private readonly WakingAfterTheBeat _wakings = new();

    /// <summary>Runs once a tick, after the callouts, with the journal events that arrived on it.</summary>
    public void Tick(IReadOnlyList<JournalEvent> arrived, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(arrived);

        var commander = gameState.Active?.Identity.FrontierId;
        var inGame = gameState.InGame?.Identity.FrontierId;

        foreach (var journalEvent in arrived)
        {
            if (storyDirector.Observe(journalEvent, inGame) is { } waking)
            {
                // The callouts ran earlier on this tick, so a beat the same scan reached is already owed its line.
                var chapter = storyStore.Current(commander)?.CurrentChapter;
                var core = waking == CoreWaking.Cores ? storyDirector.CoreOf(commander) : null;
                _wakings.Add(waking, core, chapter is not null && adventureBook.IsStirring(commander, chapter) ? chapter : null, now);
            }

            // A dock beat whose station has its docks offline is written again on the pool.
            _ = storyDirector.DockOffline(journalEvent, inGame);
        }

        // A dock objective the ship aboard cannot use is said once per ship.
        storyDirector.WarnPadMismatch(inGame, gameState.InGame?.Ship.Type);

        if (storyDirector.TakeSteppedWaking(commander) is { } stepped)
        {
            var chapter = storyStore.Current(commander)?.CurrentChapter;
            var core = stepped == CoreWaking.Cores ? storyDirector.CoreOf(commander) : null;
            _wakings.Add(stepped, core, chapter is not null && adventureBook.IsStirring(commander, chapter) ? chapter : null, now);
        }

        // The opening is said first, then a narrated scan, before the cores it wakes; the story's core comes aboard
        // once the scan line has been said. A remembered scan says no line and boards the core once no opening is
        // being said. It is taken before the opening, so it is never taken a tick ahead of the opening it waits for.
        var remembered = storyDirector.TakeRememberedScan(commander);
        var opening = storyDirector.TakeOpening(commander);
        var narrated = storyDirector.TakeNarratedScan(commander);

        if (opening is not null || narrated is not null)
        {
            voice.NarrateStart(opening, narrated, commander);
        }

        if (narrated is not null)
        {
            _wakings.Add(CoreWaking.Cores, storyDirector.CoreOf(commander), NarratedScanKey, now);
        }

        if (remembered is not null)
        {
            _wakings.Add(CoreWaking.Remembered, storyDirector.CoreOf(commander), OpeningKey, now);
        }

        foreach (var (waking, core) in _wakings.Due(
                     owed => owed switch
                     {
                         NarratedScanKey => voice.IsNarratingScan,
                         OpeningKey => voice.IsSayingOpening,
                         _ => adventureBook.IsStirring(commander, owed),
                     },
                     now))
        {
            voice.CoresWoke(waking, core);
        }

        // A change of Commander changes whose story holds the cores.
        voice.ReadmitIfTheHoldChanged();

        // A finished chapter's successor is written on the pool; the tick only starts it. A refusal is said.
        if (storyDirector.Tick(commander, now) is { } writing)
        {
            _ = writing.ContinueWith(
                written =>
                {
                    if (written.Result is { } refusal)
                    {
                        voice.SayAside(refusal);
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnRanToCompletion,
                TaskScheduler.Default);
        }
        voice.PostEndingIfDue(commander);
    }
}
