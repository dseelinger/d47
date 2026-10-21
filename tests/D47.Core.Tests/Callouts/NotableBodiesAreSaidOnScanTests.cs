using D47.Core.Callouts;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

public class NotableBodiesAreSaidOnScanTests
{
    private const long SystemAddress = 1_000_100;

    private static JournalEvent Parse(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static string Scan(int id, string name, string extra, string scanType = "Detailed") => $$"""
        {"timestamp":"3311-04-02T09:00:00Z","event":"Scan","ScanType":"{{scanType}}","SystemAddress":{{SystemAddress}},
         "BodyID":{{id}},"BodyName":"S {{name}}"{{extra}}}
        """;

    private const string Eccentric = """"
        ,"Parents":[{"Planet":2},{"Planet":1},{"Star":0}],"Eccentricity":0.9
        """";

    private static readonly string Ringed = Scan(
        5, "5", """
        ,"Rings":[{"Name":"S 5 A Ring","RingClass":"eRingClass_Rocky","InnerRad":1000.0,"OuterRad":5000.0}],"Radius":1000.0
        """);

    private static readonly string Moon = Scan(6, "5 a", """
        ,"Parents":[{"Planet":5},{"Star":0}],"SemiMajorAxis":2000.0
        """);

    private static readonly string LandableAir = Scan(
        7, "7", """
        ,"Landable":true,"Atmosphere":"thin argon atmosphere"
        """);

    private static CommanderGameState Commander(params string[] events)
    {
        var store = new GameStateStore();
        store.Apply(Parse("""{"timestamp":"2026-08-16T09:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}"""));

        foreach (var json in events)
        {
            store.Apply(Parse(json));
        }

        return store.Active!;
    }

    private static List<string> Said(NotableBodyCallout callout, CommanderGameState state, string last, bool priming = false) =>
        [.. callout.Examine(new CalloutContext(
                DateTimeOffset.UnixEpoch, priming, state, GameStatus.Unknown, NavRoute.None, [Parse(last)]))
            .Select(announcement => announcement.Text)];

    [Fact]
    public void ATraitWhoseCriterionIsOnIsSaid()
    {
        var scan = Scan(3, "AB 1 b", Eccentric);

        Assert.Equal(
            ["S AB 1 b: a moon of a moon, on a highly eccentric orbit."],
            Said(new NotableBodyCallout(), Commander(scan), scan));
    }

    [Fact]
    public void ATraitWhoseCriterionIsOffIsNotSaid()
    {
        var scan = Scan(3, "AB 1 b", Eccentric);
        var callout = new NotableBodyCallout { Criterion = kind => kind != NotableKind.EccentricOrbit };

        Assert.Equal(["S AB 1 b: a moon of a moon."], Said(callout, Commander(scan), scan));
    }

    [Fact]
    public void AMoonScannedBeforeItsRingedParentIsSaidWhenTheParentIsScanned()
    {
        var callout = new NotableBodyCallout();

        Assert.Empty(Said(callout, Commander(Moon), Moon));
        Assert.Equal(
            ["S 5 a: orbiting inside its parent's rings."],
            Said(callout, Commander(Moon, Ringed), Ringed));
    }

    [Fact]
    public void ABodyAlreadySaidIsNotSaidOnItsDetailedScan()
    {
        var first = Scan(3, "AB 1 b", Eccentric, "AutoScan");
        var detailed = Scan(3, "AB 1 b", Eccentric);
        var callout = new NotableBodyCallout();
        var state = Commander(first);

        Assert.Single(Said(callout, state, first));
        Assert.Empty(Said(callout, Commander(first, detailed), detailed));
    }

    [Fact]
    public void ABodyThatGainsATraitSaysOnlyTheNewOne()
    {
        var plain = Scan(7, "7", """
            ,"Landable":true
            """);
        var callout = new NotableBodyCallout();

        Assert.Empty(Said(callout, Commander(plain), plain));
        Assert.Equal(["S 7: landable, with an atmosphere."], Said(callout, Commander(plain, LandableAir), LandableAir));
    }

    [Fact]
    public void NothingIsSaidFromTheBacklogAndItsTraitsAreNotSaidLater()
    {
        var scan = Scan(3, "AB 1 b", Eccentric);
        var callout = new NotableBodyCallout();
        var state = Commander(scan);

        Assert.Empty(Said(callout, state, scan, priming: true));
        Assert.Empty(Said(callout, state, scan));
    }

    [Fact]
    public void ABodyWithMoreThanThreeTraitsNamesThree()
    {
        var scan = Scan(3, "AB 1 b", """
            ,"Parents":[{"Planet":2},{"Planet":1},{"Star":0}],"Eccentricity":0.9,"RotationPeriod":7200.0,"Radius":100.0
            ,"Rings":[{"Name":"S AB 1 b A Ring","RingClass":"eRingClass_Rocky","InnerRad":1000.0,"OuterRad":100000.0}]
            """);

        var said = Assert.Single(Said(new NotableBodyCallout(), Commander(scan), scan));

        Assert.Equal(
            "S AB 1 b: a moon of a moon, on a highly eccentric orbit, turning once every 2 hours.",
            said);
    }
}
