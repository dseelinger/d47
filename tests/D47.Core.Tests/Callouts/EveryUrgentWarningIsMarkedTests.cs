using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>
/// Every urgent danger warning carries an alarm, and no routine one does
/// (<a href="https://github.com/dseelinger/d47/issues/136">#136</a>).
/// <para>
/// <b>The cue was on the warning that an attack might happen and not on the one saying it is.</b>
/// Four announcements in the whole app set one, and all four are warnings about something that has
/// not happened yet — so a pirate <em>announcing</em> an interdiction got a sound before the
/// sentence and being shot did not. That is backwards, and it is the whole of the request.
/// </para>
/// <para>
/// Phase 15's argument for a cue was that it says which warning this is <em>before the sentence has
/// arrived</em>, which is worth a second or two when a second or two is what the warning is for.
/// Every line in <see cref="DangerCallout"/> is urgent for precisely that reason.
/// </para>
/// </summary>
public class EveryUrgentWarningIsMarkedTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("3311-01-01T00:00:00Z");

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static CommanderGameState StateFrom(params string[] lines)
    {
        var store = new GameStateStore();
        store.Apply(Event("""{"timestamp":"3311-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}"""));

        foreach (var line in lines)
        {
            store.Apply(Event(line));
        }

        return store.Active!;
    }

    private static GameStatus Status(StatusFlags flags, double? cargo = null) =>
        new() { Flags = flags | StatusFlags.InMainShip, Cargo = cargo, ReadAt = Start };

    private static CalloutContext Context(
        GameStatus? status = null,
        IEnumerable<string>? events = null,
        CommanderGameState? state = null,
        int atSecond = 0) =>
        new(
            Start.AddSeconds(atSecond),
            IsPriming: false,
            state,
            status ?? GameStatus.Unknown,
            NavRoute.None,
            [.. (events ?? []).Select(Event)]);

    /// <summary>
    /// Every warning the event path can raise, driven for real rather than asserted from a list —
    /// so a line added later without a cue fails here rather than shipping silent.
    /// </summary>
    private static IReadOnlyList<Announcement> FromEvents() =>
    [
        .. new DangerCallout().Examine(Context(events:
        [
            """{"timestamp":"3311-01-01T00:00:00Z","event":"Interdicted","Interdictor":"Kaiser Grendel","IsPlayer":false,"Submitted":false}""",
            """{"timestamp":"3311-01-01T00:00:00Z","event":"HullDamage","Health":0.62,"PlayerPilot":true,"Fighter":false}""",
            """{"timestamp":"3311-01-01T00:00:00Z","event":"HeatDamage"}""",
            """{"timestamp":"3311-01-01T00:00:00Z","event":"ShieldState","ShieldsUp":false}""",
            """{"timestamp":"3311-01-01T00:00:00Z","event":"UnderAttack","Target":"You"}""",
            """{"timestamp":"3311-01-01T00:00:00Z","event":"Died"}""",
        ])),
    ];

    /// <summary>
    /// The two the status path raises on its own edges, which the event path cannot produce:
    /// overheating and the interdiction that is still in progress.
    /// </summary>
    private static IReadOnlyList<Announcement> FromStatus()
    {
        var callout = new DangerCallout();

        // The first tick establishes the edge rather than crossing it.
        callout.Examine(Context(Status(StatusFlags.ShieldsUp))).ToList();

        // Shields deliberately left up: this path can also raise a shields-down edge, and the
        // event path above already covers that line. Two warnings out of here, and they are the two
        // nothing else can produce.
        return
        [
            .. callout.Examine(Context(
                Status(StatusFlags.ShieldsUp | StatusFlags.Overheating | StatusFlags.BeingInterdicted),
                atSecond: 1)),
        ];
    }

    /// <summary>
    /// <b>The acceptance criterion, stated exactly.</b> Every urgent line carries a cue and every
    /// routine one does not — the existing urgency distinction doing the work, rather than a second
    /// judgement about which dangers are frightening.
    /// </summary>
    [Fact]
    public void EveryUrgentLineCarriesACueAndEveryRoutineOneDoesNot()
    {
        var said = FromEvents().Concat(FromStatus()).ToList();

        // All eight, so a silently missing warning cannot make this pass by absence.
        Assert.Equal(8, said.Count);

        foreach (var announcement in said)
        {
            if (announcement.Urgency == CalloutUrgency.Urgent)
            {
                Assert.True(
                    announcement.Cue is not null,
                    $"{announcement.Key} is urgent and carries no alert cue.");
            }
            else
            {
                Assert.True(
                    announcement.Cue is null,
                    $"{announcement.Key} is routine and should not sound an alarm.");
            }
        }
    }

    /// <summary>
    /// <b>And the routine ones are named</b>, so this cannot pass by every line quietly becoming
    /// urgent. A full cargo hold and the rebuy screen are not emergencies.
    /// </summary>
    [Fact]
    public void AFullHoldAndTheRebuyScreenSoundNoAlarm()
    {
        var died = Assert.Single(FromEvents(), said => said.Key == "danger.died");

        Assert.Equal(CalloutUrgency.Routine, died.Urgency);
        Assert.Null(died.Cue);

        var callout = new DangerCallout();
        var state = StateFrom(
            """{"timestamp":"3311-01-01T00:00:00Z","event":"Loadout","Ship":"python","ShipID":7,"CargoCapacity":64,"Modules":[]}""");

        callout.Examine(Context(Status(StatusFlags.ShieldsUp, cargo: 0), state: state)).ToList();

        var full = Assert.Single(
            callout.Examine(Context(Status(StatusFlags.ShieldsUp, cargo: 64), state: state, atSecond: 1)),
            said => said.Key == "danger.cargo");

        Assert.Equal(CalloutUrgency.Routine, full.Urgency);
        Assert.Null(full.Cue);
    }

    /// <summary>
    /// <b>The number of distinct cues is a decision, not a count that grew.</b> Three across the
    /// urgent set, and the test exists so that a fourth has to be argued for rather than added.
    /// <list type="bullet">
    /// <item><b>Interdiction</b>, shared with the announced warning the Commander has already
    /// learned that sound from — being pulled is the same situation one step later.</item>
    /// <item><b>UnderFire</b> for shields, hull and under-attack: one situation reported by three
    /// sensors, and the answer to all of them is fight, run or high-wake.</item>
    /// <item><b>Overheating</b> for heat, which is the split that earned itself — nothing is
    /// shooting and the answer is the throttle.</item>
    /// </list>
    /// </summary>
    [Fact]
    public void TheUrgentSetUsesThreeCuesAndTheyAreTheseThree()
    {
        var cues = FromEvents()
            .Concat(FromStatus())
            .Where(said => said.Urgency == CalloutUrgency.Urgent)
            .Select(said => said.Cue!.Value)
            .Distinct()
            .ToHashSet();

        Assert.Equal(
            new HashSet<AlertCue> { AlertCue.Interdiction, AlertCue.UnderFire, AlertCue.Overheating },
            cues);
    }

    /// <summary>Which line got which, named individually so a swap is visible.</summary>
    [Theory]
    [InlineData("danger.interdicted", AlertCue.Interdiction)]
    [InlineData("danger.hull", AlertCue.UnderFire)]
    [InlineData("danger.shields", AlertCue.UnderFire)]
    [InlineData("danger.attack", AlertCue.UnderFire)]
    [InlineData("danger.heat", AlertCue.Overheating)]
    public void EachWarningSoundsLikeTheKindOfTroubleItIs(string key, AlertCue expected) =>
        Assert.Equal(expected, Assert.Single(FromEvents(), said => said.Key == key).Cue);

    /// <summary>
    /// <b>Nothing outside the danger callout gained a cue as a side effect.</b> Phase 15 was
    /// explicit that a cue per announcement "would make the common ones into an alarm and leave the
    /// Commander no way to tell the four that matter apart", so the routine callouts that were
    /// silent stay silent.
    /// </summary>
    [Theory]
    [InlineData("fuel.low")]
    [InlineData("route.progress")]
    [InlineData("arrival.home")]
    [InlineData("materials.full")]
    [InlineData("ambient.docked")]
    public void AnOrdinaryCalloutStillHasNoAlarm(string key) =>
        Assert.Null(new Announcement(key, "As written.").Cue);

    // ---- Not stacking -------------------------------------------------------------------

    /// <summary>A callout that says exactly what the test hands it, one line per tick.</summary>
    private sealed class Scripted(Queue<Announcement> lines) : ICallout
    {
        public string Id => "scripted";

        public IEnumerable<Announcement> Examine(CalloutContext context)
        {
            if (lines.Count > 0)
            {
                yield return lines.Dequeue();
            }
        }
    }

    private static CalloutEngine Saying(params Announcement[] lines) =>
        new CalloutEngine(NullLogger<CalloutEngine>.Instance).Add(new Scripted(new Queue<Announcement>(lines)));

    private static Announcement Warning(string key, string text, AlertCue cue) =>
        new(key, text, CalloutUrgency.Urgent) { Cue = cue };

    /// <summary>
    /// <b>An announced interdiction followed by the interdiction itself is two warnings and one
    /// alarm.</b> The keys differ — correctly, they are different warnings and both are worth
    /// saying — so no cooldown could have caught this; it is the sound that repeats, not the line.
    /// <para>
    /// Six seconds apart, which is the corpus median between an announced attack and the shooting.
    /// </para>
    /// </summary>
    [Fact]
    public void TwoAlarmsOfTheSameKindSecondsApartSoundOnce()
    {
        var engine = Saying(
            Warning("attack.interdiction", "Pirate lining up an interdiction.", AlertCue.Interdiction),
            Warning("danger.interdiction", "We are being interdicted.", AlertCue.Interdiction));

        engine.Tick(Context());
        engine.Tick(Context(atSecond: 6));

        var said = engine.Drain();

        Assert.Equal(2, said.Count);
        Assert.Equal(AlertCue.Interdiction, said[0].Cue);

        // The marker is dropped and the words are not. What was said is still said.
        Assert.Null(said[1].Cue);
        Assert.Equal("We are being interdicted.", said[1].Text);
        Assert.Equal(CalloutUrgency.Urgent, said[1].Urgency);
    }

    /// <summary>
    /// A <em>different</em> alarm in the same window still sounds, because it carries something the
    /// first did not: this is a new kind of trouble rather than the same one again.
    /// </summary>
    [Fact]
    public void ADifferentAlarmInTheSameWindowStillSounds()
    {
        var engine = Saying(
            Warning("danger.interdiction", "We are being interdicted.", AlertCue.Interdiction),
            Warning("danger.attack", "We are under attack.", AlertCue.UnderFire));

        engine.Tick(Context());
        engine.Tick(Context(atSecond: 3));

        var said = engine.Drain();

        Assert.Equal(AlertCue.Interdiction, said[0].Cue);
        Assert.Equal(AlertCue.UnderFire, said[1].Cue);
    }

    /// <summary>
    /// And a second engagement well after the first is marked again — the spacing suppresses a
    /// double-strike, not the next fight.
    /// </summary>
    [Fact]
    public void TheSameAlarmSoundsAgainOnceTheWindowHasPassed()
    {
        var engine = Saying(
            Warning("danger.attack", "We are under attack.", AlertCue.UnderFire),
            Warning("danger.hull", "Hull damage.", AlertCue.UnderFire));

        engine.Tick(Context());
        engine.Tick(Context(atSecond: 30));

        var said = engine.Drain();

        Assert.Equal(AlertCue.UnderFire, said[0].Cue);
        Assert.Equal(AlertCue.UnderFire, said[1].Cue);
    }

    /// <summary>
    /// <b>A warning suppressed by its own key does not spend the alarm.</b> The spacing is applied
    /// after the cooldown for this reason: a repeat that is never said must not silence the marker
    /// on the next warning that is.
    /// </summary>
    [Fact]
    public void ASuppressedRepeatDoesNotSilenceTheNextAlarm()
    {
        var engine = Saying(
            new Announcement("danger.attack", "We are under attack.", CalloutUrgency.Urgent)
            {
                Cue = AlertCue.UnderFire,
                Cooldown = TimeSpan.FromSeconds(30),
            },
            new Announcement("danger.attack", "We are under attack.", CalloutUrgency.Urgent)
            {
                Cue = AlertCue.UnderFire,
                Cooldown = TimeSpan.FromSeconds(30),
            },
            Warning("danger.hull", "Hull damage.", AlertCue.UnderFire));

        engine.Tick(Context());
        engine.Tick(Context(atSecond: 5));   // swallowed by its own key's cooldown
        engine.Tick(Context(atSecond: 40));  // past both windows

        var said = engine.Drain();

        Assert.Equal(2, said.Count);
        Assert.Equal(AlertCue.UnderFire, said[1].Cue);
    }
}
