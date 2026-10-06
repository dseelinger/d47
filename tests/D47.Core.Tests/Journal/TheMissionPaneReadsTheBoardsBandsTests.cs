using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary><see cref="MissionBoard.BandOf"/>, <see cref="MissionBoard.HandsInHere"/>, <see cref="MissionBoard.Rewards"/> and <see cref="MissionCountdown"/>.</summary>
public sealed class TheMissionPaneReadsTheBoardsBandsTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-30T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

    private static readonly JournalLocation Docked = new("Sol", null, true, "Home Port");

    private static Mission Make(long id, string station, double hoursLeft, long? reward = null) =>
        new(id, $"Mission{id}") { DestinationStation = station, Expiry = Now.AddHours(hoursLeft), Reward = reward };

    [Fact]
    public void RankedGroupedByBandIsContiguousInBandOrder()
    {
        var board = new MissionBoard
        {
            Missions =
            [
                Make(1, "Far Dock", 9),
                Make(2, "Home Port", 8),
                Make(3, "Far Dock", -2),
                Make(4, "Home Port", 0.25),
                Make(5, "Far Dock", 0.5),
                Make(6, "Far Dock", 20),
            ],
        };

        var bands = board.Ranked(Now, Docked).Select(mission => MissionBoard.BandOf(mission, Now, Docked)).ToList();

        Assert.Equal(
            [MissionBand.ExpiringSoon, MissionBand.ExpiringSoon, MissionBand.HandInHere, MissionBand.Rest, MissionBand.Rest, MissionBand.Rest],
            bands);
    }

    [Fact]
    public void AMissionExpiringSoonThatHandsInHereIsMarkedAsHandingInHere()
    {
        var mission = Make(1, "Home Port", 0.5);

        Assert.Equal(MissionBand.ExpiringSoon, MissionBoard.BandOf(mission, Now, Docked));
        Assert.True(MissionBoard.HandsInHere(mission, Docked));
    }

    [Fact]
    public void NothingHandsInHereWhileUndocked()
    {
        Assert.False(MissionBoard.HandsInHere(Make(1, "Home Port", 5), JournalLocation.Unknown));
    }

    [Fact]
    public void RewardsSumTheKnownOnesAndSayWhereSomeAreMissing()
    {
        var board = new MissionBoard { Missions = [Make(1, "A", 5, 1000), Make(2, "B", 5, 250), Make(3, "C", 5)] };

        Assert.Equal(new MissionRewards(1250, true), board.Rewards());
        Assert.Equal(new MissionRewards(1000, false), (board with { Missions = [Make(1, "A", 5, 1000)] }).Rewards());
        Assert.Null((board with { Missions = [Make(3, "C", 5)] }).Rewards());
        Assert.Null(MissionBoard.Empty.Rewards());
    }

    [Theory]
    [InlineData(38 * 60, "0h 38m")]
    [InlineData(3 * 3600 + 12 * 60 + 59, "3h 12m")]
    [InlineData(23 * 3600 + 59 * 60, "23h 59m")]
    [InlineData(28 * 3600, "1d 4h")]
    [InlineData(0, "EXPIRED")]
    [InlineData(-90, "EXPIRED")]
    public void TheCountdownReadsInWholeUnitsRoundedDown(int secondsLeft, string expected)
    {
        Assert.Equal(expected, MissionCountdown.Format(Now.AddSeconds(secondsLeft), Now));
    }
}
