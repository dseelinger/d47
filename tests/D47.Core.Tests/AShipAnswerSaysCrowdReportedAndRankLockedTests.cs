using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests;

public sealed class AShipAnswerSaysCrowdReportedAndRankLockedTests
{
    private const string CrowdReported = "as other Commanders' tools reported them";

    private static StationSearchResult Stations() => new(
        "Sol",
        1,
        [new StationSummary { Name = "Arissa's Wall", SystemName = "Sol" }]);

    private static StationQuery Query(string? module, string? ship)
    {
        Assert.True(StationQuery.TryParse("Sol", module, module is null ? null : "5", module is null ? null : "A", ship, false, 50, 25, out var query, out var failure), failure);
        return query;
    }

    private static RankState Empire(int rank) => new()
    {
        TakenAt = DateTimeOffset.UnixEpoch,
        Standings = [new RankStanding("Empire", rank)],
    };

    [Fact]
    public void ARankLockedHullNamesTheRankNeededAndHeld()
    {
        var said = GalaxyCapability.Describe(Stations(), Query(null, "Imperial Clipper"), Empire(0));

        Assert.Contains("The Imperial Clipper needs Empire rank Baron. You hold no Empire rank.", said);
        Assert.Contains(CrowdReported, said);
        Assert.Contains("Arissa's Wall", said);
        Assert.True(said.IndexOf("needs Empire", StringComparison.Ordinal) < said.IndexOf("Arissa's Wall", StringComparison.Ordinal));
    }

    [Fact]
    public void AHeldRankSaysNoRankSentence()
    {
        var said = GalaxyCapability.Describe(Stations(), Query(null, "Imperial Clipper"), Empire(7));

        Assert.DoesNotContain("needs Empire", said);
        Assert.Contains(CrowdReported, said);
    }

    [Fact]
    public void UnknownRanksGiveTheRequirementWithoutWhatIsHeld()
    {
        var said = GalaxyCapability.Describe(Stations(), Query(null, "Imperial Clipper"), RankState.Empty);

        Assert.Contains("The Imperial Clipper needs Empire rank Baron.", said);
        Assert.DoesNotContain("You hold", said);
    }

    [Fact]
    public void AHullWithNoRankRequirementHasOnlyTheCrowdReportedNote()
    {
        var said = GalaxyCapability.Describe(Stations(), Query(null, "Cobra Mk V"), Empire(0));

        Assert.Contains(CrowdReported, said);
        Assert.DoesNotContain("needs", said);
    }

    [Fact]
    public void AModuleAndShipSearchHasTheCrowdReportedNote() =>
        Assert.Contains(CrowdReported, GalaxyCapability.Describe(Stations(), Query("Frame Shift Drive", "Cobra Mk V"), Empire(0)));

    [Fact]
    public void AModuleOnlySearchHasNeitherSentence()
    {
        var said = GalaxyCapability.Describe(Stations(), Query("Frame Shift Drive", null), Empire(0));

        Assert.DoesNotContain(CrowdReported, said);
        Assert.DoesNotContain("needs", said);
    }
}
