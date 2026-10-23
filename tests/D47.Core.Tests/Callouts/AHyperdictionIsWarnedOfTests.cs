using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>
/// Thargoids pulling the ship out of a hyperspace jump are warned of mid-jump when the route shows it,
/// and on arrival when it does not (#1529).
/// </summary>
public class AHyperdictionIsWarnedOfTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("3311-01-01T00:00:00Z");

    private const string ToMerope =
        """{"timestamp":"3311-01-01T12:31:44Z","event":"StartJump","JumpType":"Hyperspace","StarSystem":"Merope","SystemAddress":224644818084,"StarClass":"B"}""";

    private const string TargetsMerope =
        """{"timestamp":"3311-01-01T12:31:51Z","event":"FSDTarget","Name":"Merope","SystemAddress":224644818084,"StarClass":"B","RemainingJumpsInRoute":1}""";

    private const string LandsShort =
        """{"timestamp":"3311-01-01T12:32:09Z","event":"FSDJump","StarSystem":"Pleiades Sector HR-W d1-36","SystemAddress":1247411145035,"StarPos":[-83.0,-147.0,-341.0]}""";

    private const string Encounter =
        """{"timestamp":"3311-01-01T12:32:10Z","event":"Music","MusicTrack":"Unknown_Encounter"}""";

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static CalloutContext Context(string json, bool priming = false) =>
        new(Start, IsPriming: priming, null, GameStatus.Unknown, NavRoute.None, [Event(json)]);

    /// <summary>Each event on a tick of its own, as the journal delivers them, with what each tick said.</summary>
    private static List<(string Event, Announcement Said)> Fly(bool priming, params string[] events)
    {
        var callout = new DangerCallout();
        var said = new List<(string, Announcement)>();

        foreach (var json in events)
        {
            foreach (var announcement in callout.Examine(Context(json, priming)))
            {
                said.Add((Event(json).Kind, announcement));
            }
        }

        return said;
    }

    private static List<(string Event, Announcement Said)> Fly(params string[] events) => Fly(false, events);

    [Fact]
    public void AMidJumpTargetNamingTheSystemBeingFlownToWarnsOnceAndNotAgainOnArrival()
    {
        var said = Fly(ToMerope, TargetsMerope, LandsShort, Encounter);

        var (at, warning) = Assert.Single(said);
        Assert.Equal("FSDTarget", at);
        Assert.Equal("danger.hyperdiction", warning.Key);
        Assert.Equal("Hyperdiction. Thargoids are pulling us out of the jump.", warning.Text);
        Assert.Equal(CalloutUrgency.Urgent, warning.Urgency);
        Assert.Equal(AlertCue.Interdiction, warning.Cue);
    }

    [Fact]
    public void AllOfItOnOneTickStillWarnsOnce()
    {
        var callout = new DangerCallout();
        var context = new CalloutContext(
            Start,
            IsPriming: false,
            null,
            GameStatus.Unknown,
            NavRoute.None,
            [Event(ToMerope), Event(TargetsMerope), Event(LandsShort)]);

        var warning = Assert.Single(callout.Examine(context));
        Assert.Equal("danger.hyperdiction", warning.Key);
    }

    [Fact]
    public void WithNoRoutePlottedTheArrivalSaysWhereWeWerePulledOutAndWhereWeWereGoing()
    {
        var said = Fly(ToMerope, LandsShort, Encounter);

        var (at, arrival) = Assert.Single(said);
        Assert.Equal("FSDJump", at);
        Assert.Equal("danger.hyperdicted", arrival.Key);
        Assert.Equal("Pulled out of hyperspace in Pleiades Sector HR-W d1-36, short of Merope.", arrival.Text);
        Assert.Equal(CalloutUrgency.Urgent, arrival.Urgency);
        Assert.Equal(AlertCue.Interdiction, arrival.Cue);
    }

    [Fact]
    public void ATaxiJumpIsStillAHyperspaceJump()
    {
        var said = Fly(
            """{"timestamp":"3311-01-01T12:31:44Z","event":"StartJump","JumpType":"Hyperspace","Taxi":true,"StarSystem":"Merope","SystemAddress":224644818084}""",
            LandsShort);

        Assert.Equal("danger.hyperdicted", Assert.Single(said).Said.Key);
    }

    [Fact]
    public void TheLastJumpOfARouteSaysNothing() =>
        Assert.Empty(Fly(
            ToMerope,
            """{"timestamp":"3311-01-01T12:31:51Z","event":"NavRouteClear"}""",
            """{"timestamp":"3311-01-01T12:32:02Z","event":"FSDJump","StarSystem":"Merope","SystemAddress":224644818084}"""));

    [Fact]
    public void AMidJumpTargetNamingTheNextHopSaysNothing() =>
        Assert.Empty(Fly(
            """{"timestamp":"3311-01-01T10:00:00Z","event":"StartJump","JumpType":"Hyperspace","StarSystem":"LP 308-10","SystemAddress":5306532795082}""",
            """{"timestamp":"3311-01-01T10:00:07Z","event":"FSDTarget","Name":"Mahlanja","SystemAddress":2896728951139,"RemainingJumpsInRoute":1}""",
            """{"timestamp":"3311-01-01T10:00:18Z","event":"FSDJump","StarSystem":"LP 308-10","SystemAddress":5306532795082}"""));

    [Fact]
    public void AJumpWithNoRouteThatArrivesWhereItWasGoingSaysNothing() =>
        Assert.Empty(Fly(
            """{"timestamp":"3311-01-01T11:00:00Z","event":"StartJump","JumpType":"Hyperspace","StarSystem":"Asterope","SystemAddress":216054883492}""",
            """{"timestamp":"3311-01-01T11:00:18Z","event":"FSDJump","StarSystem":"Asterope","SystemAddress":216054883492}"""));

    [Fact]
    public void ASupercruiseStartJumpIsNotWatched() =>
        Assert.Empty(Fly(
            """{"timestamp":"3311-01-01T12:31:44Z","event":"StartJump","JumpType":"Supercruise","StarSystem":"Merope","SystemAddress":224644818084}""",
            TargetsMerope,
            LandsShort));

    [Fact]
    public void AStartJumpWithNoAddressSaysNeitherLine() =>
        Assert.Empty(Fly(
            """{"timestamp":"3311-01-01T12:31:44Z","event":"StartJump","JumpType":"Hyperspace","StarSystem":"Merope"}""",
            TargetsMerope,
            LandsShort));

    [Fact]
    public void AMidJumpTargetWithNoAddressSaysNeitherLine() =>
        Assert.Empty(Fly(
            ToMerope,
            """{"timestamp":"3311-01-01T12:31:51Z","event":"FSDTarget","Name":"Merope","RemainingJumpsInRoute":1}""",
            LandsShort));

    [Fact]
    public void AStartJumpAndTargetBothWithNoAddressSayNeitherLine() =>
        Assert.Empty(Fly(
            """{"timestamp":"3311-01-01T12:31:44Z","event":"StartJump","JumpType":"Hyperspace","StarSystem":"Merope"}""",
            """{"timestamp":"3311-01-01T12:31:51Z","event":"FSDTarget","Name":"Merope","RemainingJumpsInRoute":1}""",
            """{"timestamp":"3311-01-01T12:32:09Z","event":"FSDJump","StarSystem":"Pleiades Sector HR-W d1-36"}"""));

    [Fact]
    public void NothingIsSaidWhilePriming()
    {
        Assert.Empty(Fly(priming: true, ToMerope, TargetsMerope, LandsShort));
        Assert.Empty(Fly(priming: true, ToMerope, LandsShort));
    }

    [Theory]
    [InlineData("""{"timestamp":"3311-01-01T12:35:00Z","event":"LoadGame","Commander":"Jameson","FID":"F1"}""")]
    [InlineData("""{"timestamp":"3311-01-01T12:35:00Z","event":"Location","StarSystem":"Maia","SystemAddress":358797513434}""")]
    [InlineData("""{"timestamp":"3311-01-01T12:35:00Z","event":"Shutdown"}""")]
    public void AJumpCutShortByAReloadIsForgotten(string reload) =>
        Assert.Empty(Fly(ToMerope, reload, TargetsMerope));
}
