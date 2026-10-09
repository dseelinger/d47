using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>A scene opens on disembarking on the body of the last settlement approached, and not in a taxi.</summary>
public class ASceneOpensOnlyOnFootAtTheSettlementTests
{
    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static readonly JournalEvent Approach = Event(
        """{"timestamp":"2026-09-30T00:48:23Z","event":"ApproachSettlement","Name":"Archambeau's Serenity","StationFaction":{"Name":"LTT 7786 Silver Partnership"},"StationGovernment":"$government_Anarchy;","StationGovernment_Localised":"Anarchy","BodyName":"LTT 7786 6 a"}""");

    private static JournalEvent Disembark(string body = "LTT 7786 6 a", bool taxi = false, bool onPlanet = true) => Event(
        $$"""{"timestamp":"2026-09-30T00:49:45Z","event":"Disembark","SRV":false,"Taxi":{{(taxi ? "true" : "false")}},"Body":"{{body}}","OnStation":false,"OnPlanet":{{(onPlanet ? "true" : "false")}}}""");

    private static SceneSnapshot After(params JournalEvent[] events)
    {
        var tracker = new SceneTracker();

        foreach (var journalEvent in events)
        {
            tracker.Fold(journalEvent);
        }

        return tracker.Snapshot;
    }

    [Fact]
    public void DisembarkingAtTheSettlementOpensAScene()
    {
        var scene = After(Approach, Disembark());

        Assert.True(scene.Open);
        Assert.Equal(SceneBeatKind.Arrived, scene.Last?.Kind);
    }

    [Fact]
    public void DisembarkingWithNoSettlementApproachedOpensNone() =>
        Assert.False(After(Disembark()).Open);

    [Fact]
    public void DisembarkingOnAnotherBodyOpensNone() =>
        Assert.False(After(Approach, Disembark(body: "LTT 7786 4 a")).Open);

    [Fact]
    public void DisembarkingFromATaxiOpensNone() =>
        Assert.False(After(Approach, Disembark(taxi: true)).Open);

    [Fact]
    public void DisembarkingAtAStationOpensNone() =>
        Assert.False(After(Approach, Disembark(onPlanet: false)).Open);

    [Fact]
    public void SupercruiseClosesTheSceneAndForgetsTheApproach()
    {
        var entry = Event("""{"timestamp":"2026-09-30T00:54:10Z","event":"SupercruiseEntry","StarSystem":"LTT 7786"}""");

        Assert.False(After(Approach, Disembark(), entry).Open);
        Assert.False(After(Approach, entry, Disembark()).Open);
    }

    [Fact]
    public void ApproachingAnotherSettlementClosesTheSceneAndTheNextOpensAtIt()
    {
        var other = Event(
            """{"timestamp":"2026-09-30T00:55:00Z","event":"ApproachSettlement","Name":"Wada Horticultural Plantation","BodyName":"LTT 7786 6 a"}""");

        Assert.False(After(Approach, Disembark(), other).Open);
        Assert.Equal("Wada Horticultural Plantation", After(Approach, Disembark(), other, Disembark()).Last?.Settlement);
        Assert.True(After(Approach, Disembark(), Approach).Open);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void AnEscapeWaitingOutTheSpacingStillPlaysAfterSupercruise()
    {
        var tracker = new SceneTracker();
        var callout = new SceneCallout(tracker) { Scenario = () => "A raid." };
        var t0 = new DateTimeOffset(2026, 9, 30, 0, 50, 0, TimeSpan.Zero);
        var heard = new List<SceneBeatKind>();

        void At(int seconds, params string[] lines)
        {
            var events = lines.Select(Event).ToList();

            foreach (var journalEvent in events)
            {
                tracker.Fold(journalEvent);
            }

            heard.AddRange(callout.Examine(new CalloutContext(
                t0.AddSeconds(seconds), false, null, GameStatus.Unknown, NavRoute.None, events)).Select(marker => marker.Scene!.Kind));
        }

        tracker.Fold(Approach);
        At(0, """{"timestamp":"2026-09-30T00:50:00Z","event":"Disembark","Taxi":false,"Body":"LTT 7786 6 a","OnPlanet":true}""");
        At(10, """{"timestamp":"2026-09-30T00:50:10Z","event":"CommitCrime","CrimeType":"onFoot_murder","Victim":"Miles Lehner"}""");
        At(20, """{"timestamp":"2026-09-30T00:50:20Z","event":"Embark","Taxi":false,"Body":"LTT 7786 6 a","OnPlanet":true}""");
        At(30, """{"timestamp":"2026-09-30T00:50:30Z","event":"SupercruiseEntry","StarSystem":"LTT 7786"}""");
        At(45);

        Assert.Equal([SceneBeatKind.Arrived, SceneBeatKind.Gone], heard);
    }

    [Fact]
    public void LeavingUnseenAndUnbloodiedIsNotAnEscape()
    {
        var embark = Event("""{"timestamp":"2026-09-30T00:53:19Z","event":"Embark","SRV":false,"Taxi":false,"Body":"LTT 7786 6 a","OnPlanet":true}""");

        Assert.Equal(SceneBeatKind.Arrived, After(Approach, Disembark(), embark).Last?.Kind);
    }
}
