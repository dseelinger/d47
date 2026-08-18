using System.Text.Json;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>
/// Where the Commander stands in every career (list.md Phase 34).
/// <para>
/// Three events that look alike and are not interchangeable, which is the whole of what this has to
/// get right. The shapes here are copied off the real journal rather than invented: a
/// <c>Rank</c> and a <c>Progress</c> arrive a line apart at startup, and a <c>Promotion</c> names
/// one career and carries no percent.
/// </para>
/// </summary>
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

        // Empire and Federation are navy ranks with no Elite at the top, so they are not careers
        // this reads at all.
        Assert.Null(state.For("Empire"));
    }

    [Fact]
    public void ProgressPutsThePercentOnTheRankItIsAbout()
    {
        var state = RankState.Empty
            .Apply(Event("Rank", "\"Combat\":1,\"Trade\":7,\"Explore\":5"))
            .Apply(Event("Progress", "\"Combat\":39,\"Trade\":40,\"Explore\":12"));

        Assert.Equal(1, state.For("Combat")?.Rank);
        Assert.Equal(39, state.For("Combat")?.Percent);
        Assert.Equal("rank 1 of 8, 39% into it", state.For("Combat")?.Describe());
    }

    /// <summary>
    /// A second snapshot must not blank the percents. Elite writes the pair one line apart and a
    /// snapshot that reset them would leave every rank reading as a bare number for as long as the
    /// session lasted.
    /// </summary>
    [Fact]
    public void ASecondSnapshotCarriesThePercentsItSaysNothingAbout()
    {
        var state = RankState.Empty
            .Apply(Event("Rank", "\"Combat\":1"))
            .Apply(Event("Progress", "\"Combat\":39"))
            .Apply(Event("Rank", "\"Combat\":1"));

        Assert.Equal(39, state.For("Combat")?.Percent);
    }

    /// <summary>
    /// The distinction this class exists for. A promotion names one career; treating it as a
    /// snapshot would wipe the other four the first time somebody ranked up.
    /// </summary>
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

        // Null rather than zero: the percent it had was about a rank the Commander has left, and
        // zero is a figure Elite did not state. The next Progress event will state one.
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
    [InlineData(7, false, "rank 7 of 8")]
    [InlineData(8, true, "Elite")]
    [InlineData(10, true, "Elite, 2 grades past it")]
    public void OnlyEliteIsNamed(int rank, bool isElite, string said)
    {
        var standing = new RankStanding("Combat", rank);

        Assert.Equal(isElite, standing.IsElite);
        Assert.Equal(said, standing.Describe());
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
