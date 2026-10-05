using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary><see cref="MissionBoard.Ranked"/>: the three bands, and where an expired mission falls.</summary>
public sealed class TheMissionBoardRanksInOnePlaceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-30T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

    private static readonly JournalLocation Docked = new("Sol", null, true, "Home Port");

    private static Mission Make(long id, string station, double hoursLeft) =>
        new(id, $"Mission{id}") { DestinationStation = station, Expiry = Now.AddHours(hoursLeft) };

    [Fact]
    public void ExpiringSoonThenHandedInHereThenTheRestEachSoonestFirst()
    {
        var board = new MissionBoard
        {
            Missions =
            [
                Make(1, "Far Dock", 9),
                Make(2, "Home Port", 8),
                Make(3, "Far Dock", 3),
                Make(4, "Home Port", 5),
                Make(5, "Far Dock", 0.5),
                Make(6, "Home Port", 0.25),
            ],
        };

        var ranked = board.Ranked(Now, Docked).Select(mission => mission.Id).ToList();

        Assert.Equal([6, 5, 4, 2, 3, 1], ranked);
    }

    [Fact]
    public void AnExpiredMissionSortsAfterLiveOnesInTheLastBand()
    {
        var board = new MissionBoard
        {
            Missions = [Make(1, "Far Dock", -2), Make(2, "Far Dock", 20), Make(3, "Far Dock", 6)],
        };

        var ranked = board.Ranked(Now, Docked).Select(mission => mission.Id).ToList();

        Assert.Equal([3, 2, 1], ranked);
    }
}
