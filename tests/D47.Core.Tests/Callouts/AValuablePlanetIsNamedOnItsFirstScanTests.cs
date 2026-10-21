using D47.Core.Callouts;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>A planet worth mapping is named on its first scan this session.</summary>
public class AValuablePlanetIsNamedOnItsFirstScanTests
{
    private const long SystemAddress = 3274702866819;

    private static JournalEvent Parse(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static string Scan(string planetClass, string terraform = "", string type = "Detailed", int body = 2, double mass = 1.5) =>
        $$"""
          {"timestamp":"2026-10-04T16:00:00Z","event":"Scan","ScanType":"{{type}}","BodyName":"Eorl Aod YK-D c40 {{body}}",
           "BodyID":{{body}},"StarSystem":"Eorl Aod YK-D c40","SystemAddress":{{SystemAddress}},
           "PlanetClass":"{{planetClass}}","TerraformState":"{{terraform}}","MassEM":{{mass}},
           "WasDiscovered":true,"WasMapped":false}
          """;

    private const string Star =
        """
        {"timestamp":"2026-10-04T16:00:00Z","event":"Scan","ScanType":"AutoScan","BodyName":"Eorl Aod YK-D c40",
         "BodyID":0,"SystemAddress":3274702866819,"StarType":"K","StellarMass":0.8}
        """;

    private const string Mapped =
        """
        {"timestamp":"2026-10-04T16:05:00Z","event":"SAAScanComplete","BodyName":"Eorl Aod YK-D c40 2",
         "SystemAddress":3274702866819,"BodyID":2,"ProbesUsed":4,"EfficiencyTarget":5}
        """;

    private static CartographyLedger Ledger(params string[] lines)
    {
        var ledger = new CartographyLedger(null, new MemoryFileSystem(), NullLogger.Instance);
        ledger.Apply([.. lines.Select(Parse)]);
        return ledger;
    }

    private static List<Announcement> Say(BodyValueCallout callout, bool priming, params string[] lines) =>
        [.. callout.Examine(new CalloutContext(
            DateTimeOffset.UnixEpoch,
            IsPriming: priming,
            State: null,
            Status: GameStatus.Unknown,
            Route: NavRoute.None,
            Events: [.. lines.Select(Parse)]))];

    [Fact]
    public void ATerraformablePlanetOverTheThresholdIsNamedWithItsValue()
    {
        var scan = Scan("High metal content body", "Terraformable", mass: 1.7);
        var said = Assert.Single(Say(new BodyValueCallout { Ledger = Ledger(scan) }, false, scan));

        Assert.StartsWith("Eorl Aod YK-D c40 2: a terraformable high metal content world, about ", said.Text);
        Assert.EndsWith(" if mapped.", said.Text);
    }

    [Theory]
    [InlineData("Earthlike body", "an Earth-like world")]
    [InlineData("Water world", "a water world")]
    [InlineData("Ammonia world", "an ammonia world")]
    [InlineData("Metal rich body", "a metal-rich world")]
    public void TheClassIsSaidInWords(string planetClass, string phrase)
    {
        var scan = Scan(planetClass);
        var said = Assert.Single(Say(new BodyValueCallout { Ledger = Ledger(scan), Threshold = () => 0 }, false, scan));

        Assert.StartsWith($"Eorl Aod YK-D c40 2: {phrase}, about ", said.Text);
    }

    [Fact]
    public void AGasGiantAndARockyBodyAreNamedPlainly()
    {
        var giant = Scan("Sudarsky class II gas giant", body: 3, mass: 300);
        var rocky = Scan("Rocky body", body: 4);
        var callout = new BodyValueCallout { Ledger = Ledger(giant, rocky), Threshold = () => 0 };

        var said = Say(callout, false, giant, rocky);

        Assert.Contains("3: a gas giant, about", said[0].Text);
        Assert.Contains("4: a rocky world, about", said[1].Text);
    }

    [Fact]
    public void APlanetUnderTheThresholdSaysNothing()
    {
        var scan = Scan("Rocky body");

        Assert.Empty(Say(new BodyValueCallout { Ledger = Ledger(scan) }, false, scan));
    }

    [Fact]
    public void ASecondScanOfTheSameBodySaysNothing()
    {
        var first = Scan("Water world", type: "AutoScan");
        var second = Scan("Water world");
        var callout = new BodyValueCallout { Ledger = Ledger(first, second) };

        Assert.Single(Say(callout, false, first));
        Assert.Empty(Say(callout, false, second));
    }

    [Fact]
    public void TheDetailedScanAfterMappingNeverSpeaks()
    {
        var detailed = Scan("Water world");
        var callout = new BodyValueCallout { Ledger = Ledger(detailed) };

        Assert.Empty(Say(callout, false, Mapped, detailed));
    }

    [Fact]
    public void StarsSayNothing()
    {
        Assert.Empty(Say(new BodyValueCallout { Ledger = Ledger(Star), Threshold = () => 0 }, false, Star));
    }

    [Fact]
    public void WithNoLedgerNothingIsSaid()
    {
        var scan = Scan("Water world");

        Assert.Empty(Say(new BodyValueCallout(), false, scan));
    }

    [Fact]
    public void APrimedScanIsRecordedAndNotSaid()
    {
        var scan = Scan("Water world");
        var callout = new BodyValueCallout { Ledger = Ledger(scan) };

        Assert.Empty(Say(callout, true, scan));
        Assert.Empty(Say(callout, false, scan));
    }
}
