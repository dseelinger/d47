using System.Text.Json;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>Where the Commander stands in every career.</summary>
public class RankStateTests
{
    private static readonly DateTimeOffset At = new(3311, 4, 2, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void NothingIsKnownBeforeTheGameSays()
    {
        Assert.False(RankState.Empty.IsKnown);
        Assert.Null(RankState.Empty.For("Combat"));
    }

    [Fact]
    public void TheStartupSnapshotEstablishesEveryCareer()
    {
        var state = RankState.Empty.Apply(Event(
            "Rank",
            "\"Combat\":1,\"Trade\":7,\"Explore\":5,\"Soldier\":0,\"Exobiologist\":0,\"Empire\":1,\"Federation\":1,\"CQC\":0"));

        Assert.True(state.IsKnown);
        Assert.Equal(1, state.For("Combat")?.Rank);
        Assert.Equal(7, state.For("Trade")?.Rank);
        Assert.Equal(0, state.For("Soldier")?.Rank);
    }

    [Fact]
    public void TheNavyRanksAreReadWithoutBecomingCareers()
    {
        var state = RankState.Empty.Apply(Event("Rank", "\"Combat\":1,\"Empire\":1,\"Federation\":3"));

        Assert.Equal(1, state.For("Empire")?.Rank);
        Assert.Equal(3, state.For("Federation")?.Rank);
        Assert.Equal(6, RankState.Careers.Count);
        Assert.DoesNotContain("Empire", RankState.Careers);
    }

    [Fact]
    public void ANavyRankPastEightIsNotElite()
    {
        var federation = RankState.Empty.Apply(Event("Rank", "\"Federation\":14")).For("Federation");

        Assert.False(federation?.IsElite);
        Assert.Equal("Admiral", federation?.Describe());
    }

    [Fact]
    public void ProgressPutsThePercentOnTheRankItIsAbout()
    {
        var state = RankState.Empty
            .Apply(Event("Rank", "\"Combat\":1,\"Trade\":7,\"Explore\":5"))
            .Apply(Event("Progress", "\"Combat\":39,\"Trade\":40,\"Explore\":12"));

        Assert.Equal(1, state.For("Combat")?.Rank);
        Assert.Equal(39, state.For("Combat")?.Percent);
        Assert.Equal("Mostly Harmless, 39% into it", state.For("Combat")?.Describe());
    }

    /// <summary>A second snapshot must not blank the percents.</summary>
    [Fact]
    public void ASecondSnapshotCarriesThePercentsItSaysNothingAbout()
    {
        var state = RankState.Empty
            .Apply(Event("Rank", "\"Combat\":1"))
            .Apply(Event("Progress", "\"Combat\":39"))
            .Apply(Event("Rank", "\"Combat\":1"));

        Assert.Equal(39, state.For("Combat")?.Percent);
    }

    /// <summary>The distinction this class exists for.</summary>
    [Fact]
    public void APromotionMovesOneCareerAndLeavesTheRestStanding()
    {
        var state = RankState.Empty
            .Apply(Event("Rank", "\"Combat\":1,\"Trade\":7,\"Explore\":4"))
            .Apply(Event("Progress", "\"Combat\":39,\"Trade\":40,\"Explore\":98"))
            .Apply(Event("Promotion", "\"Explore\":5"));

        Assert.Equal(5, state.For("Explore")?.Rank);
        Assert.Equal(7, state.For("Trade")?.Rank);
        Assert.Equal(40, state.For("Trade")?.Percent);

        // Null rather than zero: the percent it had was about a rank the Commander has left, and zero is a
        // figure Elite did not state.
        Assert.Null(state.For("Explore")?.Percent);
    }

    [Fact]
    public void APromotionBeforeAnySnapshotStillEstablishesThatCareer()
    {
        var state = RankState.Empty.Apply(Event("Promotion", "\"Trade\":3"));

        Assert.Equal(3, state.For("Trade")?.Rank);
        Assert.True(state.IsKnown);
    }

    [Theory]
    [InlineData(0, false, "Harmless")]
    [InlineData(7, false, "Deadly")]
    [InlineData(8, true, "Elite")]
    [InlineData(9, true, "Elite I")]
    [InlineData(10, true, "Elite II")]
    [InlineData(13, true, "Elite V")]
    public void EachRankHasItsOwnName(int rank, bool isElite, string said)
    {
        var standing = new RankStanding("Combat", rank);

        Assert.Equal(isElite, standing.IsElite);
        Assert.Equal(said, standing.Describe());
    }

    [Fact]
    public void ACareerRunsToEliteVRatherThanStoppingAtElite()
    {
        var atElite = new RankStanding("Trade", RankStanding.Elite);
        var atEliteTop = new RankStanding("Trade", RankStanding.EliteTop);

        Assert.False(atElite.IsMaxRank);
        Assert.True(atEliteTop.IsMaxRank);
    }

    [Theory]
    [InlineData("Trade", 12, "Elite IV")]
    [InlineData("Explore", 4, "Trailblazer")]
    [InlineData("Soldier", 0, "Defenceless")]
    [InlineData("Exobiologist", 2, "Compiler")]
    [InlineData("CQC", 5, "Champion")]
    public void EveryCareerHasItsOwnNames(string career, int rank, string said)
    {
        Assert.Equal(said, new RankStanding(career, rank).Describe());
    }

    [Theory]
    [InlineData("Empire", 1, "Outsider")]
    [InlineData("Empire", 14, "King")]
    [InlineData("Federation", 1, "Recruit")]
    [InlineData("Federation", 14, "Admiral")]
    public void ANavyRankIsNamedFromTheJournalValueAsWritten(string navy, int rank, string said)
    {
        Assert.Equal(said, new RankStanding(navy, rank).Describe());
    }

    [Fact]
    public void AnUnrelatedEventChangesNothing()
    {
        var state = RankState.Empty.Apply(Event("Rank", "\"Combat\":4"));

        Assert.Same(state, state.Apply(Event("Music", "\"MusicTrack\":\"NoTrack\"")));
    }

    private static JournalEvent Event(string kind, string fields)
    {
        var text = $"{{ \"timestamp\":\"{At.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}\", \"event\":\"{kind}\", {fields} }}";

        return new JournalEvent(At, kind, JsonDocument.Parse(text).RootElement);
    }
}
