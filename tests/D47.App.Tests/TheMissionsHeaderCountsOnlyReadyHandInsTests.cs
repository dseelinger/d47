using D47.App.Panel;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

public class TheMissionsHeaderCountsOnlyReadyHandInsTests
{
    private const string Station = "Crater Outpost";

    private static MissionBoard Board(string name, string? commodity = null, int? count = null)
    {
        var payload = $$"""{"timestamp":"2026-10-05T18:00:00Z","event":"MissionAccepted","MissionID":1,"Name":"{{name}}","DestinationSystem":"Sol","DestinationStation":"{{Station}}"{{(commodity is null ? "" : $$""","Commodity":"${{commodity}}_Name;","Count":{{count}}""")}}}""";
        Assert.True(JournalEvent.TryParse(payload, NullLogger.Instance, out var accepted));
        return MissionBoard.Empty.Apply(accepted!);
    }

    private static JournalLocation Docked() => JournalLocation.Unknown with { Docked = true, StationName = Station };

    [Fact]
    public void ACollectMissionWithAnEmptyHoldIsNotCounted()
    {
        var hold = new CargoHold { Vessel = "Ship", ReadAt = DateTimeOffset.UnixEpoch };

        Assert.Equal("1 mission.", MissionsPage.Summarise(Board("Mission_Collect", "grain", 8), Docked(), hold));
    }

    [Fact]
    public void ACollectMissionWithItsCargoAboardIsCounted()
    {
        var hold = new CargoHold { Vessel = "Ship", Items = [new CargoItem("grain", 8)], Count = 8, ReadAt = DateTimeOffset.UnixEpoch };

        Assert.Equal("1 mission. 1 hands in here.", MissionsPage.Summarise(Board("Mission_Collect", "grain", 8), Docked(), hold));
    }
}
