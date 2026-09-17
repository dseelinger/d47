using D47.Core.Journal;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The rescan sentence gives the active commander's ship count, matching the row above it, rather
/// than the total across every FID the journal folder holds (#246).
/// </summary>
public class TheRescanSentenceCountsThisCommandersShipsTests
{
    private static ShipLoadouts Ships(int count)
    {
        var ships = new Dictionary<int, RememberedShip>();

        for (var id = 0; id < count; id++)
        {
            ships[id] = new RememberedShip(
                new ShipLoadout { Type = "Anaconda", ShipId = id },
                DateTimeOffset.Now);
        }

        return new ShipLoadouts { Ships = ships };
    }

    [Fact]
    public void WithOneCommanderTheSentenceHasNoSecondClause()
    {
        var found = new LoadoutRescan(
            979,
            12,
            new Dictionary<string, ShipLoadouts> { ["F735466"] = Ships(12) });

        Assert.Equal(
            "Read 979 journals. 12 ships remembered.",
            AppHost.RescanSentence(found, "F735466"));
    }

    [Fact]
    public void WithOtherCommandersTheSecondClauseNamesThem()
    {
        var found = new LoadoutRescan(
            979,
            21,
            new Dictionary<string, ShipLoadouts>
            {
                ["F735466"] = Ships(12),
                ["F12484034"] = Ships(5),
                ["F12242026"] = Ships(4),
            });

        Assert.Equal(
            "Read 979 journals. 12 ships remembered for this commander, and 9 for 2 other commanders "
            + "on this machine.",
            AppHost.RescanSentence(found, "F735466"));
    }

    [Fact]
    public void SingularFormsReadCorrectly()
    {
        var found = new LoadoutRescan(
            10,
            2,
            new Dictionary<string, ShipLoadouts>
            {
                ["F735466"] = Ships(1),
                ["F12484034"] = Ships(1),
            });

        Assert.Equal(
            "Read 10 journals. 1 ship remembered for this commander, and 1 for 1 other commander "
            + "on this machine.",
            AppHost.RescanSentence(found, "F735466"));
    }

    [Fact]
    public void WithNoActiveCommanderTheTotalHasNoSplit()
    {
        var found = new LoadoutRescan(
            979,
            21,
            new Dictionary<string, ShipLoadouts>
            {
                ["F735466"] = Ships(12),
                ["F12484034"] = Ships(9),
            });

        Assert.Equal("Read 979 journals. 21 ships remembered.", AppHost.RescanSentence(found, null));
    }

    [Fact]
    public void TheActiveCommandersCountIsZeroWhenTheyHaveNoneRemembered()
    {
        var found = new LoadoutRescan(
            10,
            5,
            new Dictionary<string, ShipLoadouts> { ["F12484034"] = Ships(5) });

        Assert.Equal(
            "Read 10 journals. 0 ships remembered for this commander, and 5 for 1 other commander "
            + "on this machine.",
            AppHost.RescanSentence(found, "F735466"));
    }
}
