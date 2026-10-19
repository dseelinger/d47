using D47.Core.Adventures;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

public class ADiscoverObjectiveCountsEachNewBodyOnceTests
{
    private const string Earthlike = """{ "timestamp":"@", "event":"Scan", "ScanType":"Detailed", "SystemAddress":1, "BodyID":4, "PlanetClass":"Earthlike body", "WasDiscovered":false }""";

    private static AdventureStanding Fold(string? filter, int count, params string[] scans)
    {
        var adventure = LanternRoute(Accepted) with
        {
            Beats =
            [
                Beat("The Lantern", "setup", new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = Lantern, System = "Ossen's Lantern" }, "Go."),
                Beat("The Find", "catalyst", new AdventureTrigger { Kind = TriggerKind.Discover, Count = count, Filter = filter }, "Done."),
            ],
        };

        var standing = AdventureFold.Apply(
            AdventureFold.Start(adventure),
            Event($$"""{ "timestamp":"{{Stamp(Accepted.AddMinutes(1))}}", "event":"FSDJump", "SystemAddress":{{Lantern}}, "StarSystem":"Ossen's Lantern" }"""));

        foreach (var scan in scans)
        {
            standing = AdventureFold.Apply(standing, Event(scan.Replace("@", Stamp(Accepted.AddMinutes(2)), StringComparison.Ordinal)));
        }

        return standing;
    }

    [Theory]
    [InlineData("Earthlike body", true)]
    [InlineData("earthlikebody", true)]
    [InlineData(null, true)]
    [InlineData("Water world", false)]
    public void AnUndiscoveredScanCountsOnlyForItsClassOrNoFilter(string? filter, bool counts) =>
        Assert.Equal(counts, Fold(filter, 1, Earthlike).IsDone);

    [Fact]
    public void ADiscoveredScanCountsForNothing() =>
        Assert.False(Fold(null, 1, Earthlike.Replace("false", "true", StringComparison.Ordinal)).IsDone);

    [Fact]
    public void ANeutronStarIsMatchedByItsStarType() =>
        Assert.True(Fold("N", 1, """{ "timestamp":"@", "event":"Scan", "SystemAddress":1, "BodyID":0, "StarType":"N", "WasDiscovered":false }""").IsDone);

    [Fact]
    public void AnAutoScanAndADetailedScanOfOneBodyCountOnce()
    {
        var auto = Earthlike.Replace("Detailed", "AutoScan", StringComparison.Ordinal);

        Assert.False(Fold(null, 2, auto, Earthlike).IsDone);
        Assert.True(Fold(null, 2, auto, Earthlike, Earthlike.Replace("\"BodyID\":4", "\"BodyID\":5", StringComparison.Ordinal)).IsDone);
    }

    [Theory]
    [InlineData("Gas giant with water based life", 0)]
    [InlineData("N", 0)]
    [InlineData(null, 0)]
    [InlineData("Rocky body", 1)]
    public void ADiscoverObjectiveRefusesAnyOtherFilter(string? filter, int problems) =>
        Assert.Equal(problems, AdventureValidation.CountedProblems("Beat 2", new AdventureTrigger { Kind = TriggerKind.Discover, Count = 1, Filter = filter }).Count());

    [Fact]
    public void ADiscoverObjectiveSaysWhatItAsks()
    {
        var trigger = new AdventureTrigger { Kind = TriggerKind.Discover, Count = 1, Filter = "Earthlike body" };

        Assert.Equal("Undiscovered Earth-like worlds scanned: 0 of 1", trigger.Progress(0));
    }
}
