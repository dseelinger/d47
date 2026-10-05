using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class AMiningRunIsFoldedFromTheJournalTests
{
    private static string Line(string time, string kind, string fields = "") =>
        "{ \"timestamp\":\"2025-07-10T" + time + "Z\", \"event\":\"" + kind + "\"" + (fields.Length > 0 ? ", " + fields : "") + " }";

    private static string Launch(string time, string type) => Line(time, "LaunchDrone", $"\"Type\":\"{type}\"");

    private static string Rock(string time, string extra = "") =>
        Line(time, "ProspectedAsteroid", "\"Materials\":[ { \"Name\":\"Platinum\", \"Proportion\":58.2 } ], \"Content\":\"$AsteroidMaterialContent_Low;\"" + extra);

    private static string Refine(string time, string type, string? spoken = null) =>
        Line(time, "MiningRefined", $"\"Type\":\"{type}\"" + (spoken is null ? "" : $", \"Type_Localised\":\"{spoken}\""));

    private static CommanderGameState StateFrom(IEnumerable<string> lines)
    {
        var store = new GameStateStore();
        store.Apply(Event("""{ "timestamp":"2025-07-10T06:23:44Z", "event":"Commander", "FID":"F1", "Name":"Jameson" }"""));

        foreach (var line in lines)
        {
            store.Apply(Event(line));
        }

        return store.Active!;
    }

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static IEnumerable<string> Repeat(int times, Func<int, string> line) => Enumerable.Range(0, times).Select(line);

    [Fact]
    public void ARunIsCountedFromItsFirstEventToTheDocking()
    {
        var lines = new List<string> { Launch("10:48:06", "Collection") };
        lines.AddRange(Repeat(31, _ => Rock("10:49:21")));
        lines.AddRange(Repeat(33, _ => Launch("10:49:30", "Prospector")));
        lines.AddRange(Repeat(18, _ => Launch("10:49:40", "Collection")));
        lines.AddRange(Repeat(90, _ => Refine("10:50:59", "$platinum_name;", "Platinum")));
        lines.AddRange(Repeat(12, _ => Refine("11:10:00", "$samarium_name;", "Samarium")));
        lines.AddRange(Repeat(6, _ => Refine("11:59:31", "$praseodymium_name;", "Praseodymium")));
        lines.Add(Line("12:14:28", "Docked", "\"StationName\":\"Jameson Memorial\""));

        var mining = StateFrom(lines).Mining;
        var run = mining.Last!;

        Assert.Null(mining.Open);
        Assert.Equal(DateTimeOffset.Parse("2025-07-10T10:48:06Z"), run.OpenedAt);
        Assert.Equal(DateTimeOffset.Parse("2025-07-10T12:14:28Z"), run.ClosedAt);
        Assert.Equal("Docked", run.ClosedBy);
        Assert.Equal(90, run.Refined["platinum"].Tonnes);
        Assert.Equal(12, run.Refined["samarium"].Tonnes);
        Assert.Equal(6, run.Refined["praseodymium"].Tonnes);
        Assert.Equal("Platinum", run.Refined["platinum"].Name);
        Assert.Equal(33, run.ProspectorsLaunched);
        Assert.Equal(19, run.CollectorsLaunched);
        Assert.Equal(31, run.RocksProspected);
        Assert.Equal(DateTimeOffset.Parse("2025-07-10T11:59:31Z"), run.LastRefinedAt);
    }

    [Fact]
    public void ARockWithACoreIsCountedAsOne()
    {
        var mining = StateFrom([Rock("10:00:00"), Rock("10:01:00", ", \"MotherlodeMaterial\":\"Painite\"")]).Mining;

        Assert.Equal(2, mining.Open!.RocksProspected);
        Assert.Equal(1, mining.Open.CoresFound);
    }

    [Fact]
    public void ADeathClosesTheRun()
    {
        var mining = StateFrom([Launch("10:00:00", "Prospector"), Line("10:05:00", "Died")]).Mining;

        Assert.Null(mining.Open);
        Assert.Equal("Died", mining.Last!.ClosedBy);
    }

    [Theory]
    [InlineData("Hatchbreaker")]
    [InlineData("Repair")]
    public void ALaunchOfAnotherDroneOpensNothing(string type)
    {
        var mining = StateFrom([Launch("10:00:00", type)]).Mining;

        Assert.Null(mining.Open);
        Assert.Null(mining.Last);
    }

    [Fact]
    public void ACollectorAloneMakesARunWithNoTonnes()
    {
        var mining = StateFrom([Launch("10:00:00", "Collection"), Line("10:30:00", "Docked")]).Mining;

        Assert.Equal(0, mining.Last!.TonnesRefined);
        Assert.Equal(1, mining.Last.CollectorsLaunched);
    }

    [Fact]
    public void BothSpellingsOfALowTemperatureDiamondAreOneMaterial()
    {
        var mining = StateFrom(
        [
            Refine("10:00:00", "$lowtemperaturediamond_name;", "Low Temperature Diamonds"),
            Refine("10:01:00", "LowTemperatureDiamond", "Low Temperature Diamonds"),
        ]).Mining;

        Assert.Single(mining.Open!.Refined);
        Assert.Equal(2, mining.Open.TonnesRefined);
    }

    [Fact]
    public void ADockingWithNoRunOpenLeavesTheLastRunAlone()
    {
        var mining = StateFrom([Launch("10:00:00", "Prospector"), Line("10:05:00", "Docked"), Line("11:00:00", "Docked")]).Mining;

        Assert.Equal(DateTimeOffset.Parse("2025-07-10T10:05:00Z"), mining.Last!.ClosedAt);
    }
}
