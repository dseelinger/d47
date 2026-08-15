using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>
/// Folding the community goal board. Everything asserted here is a shape measured against 912
/// real journals — see <c>docs/spikes/community-goals.md</c> — and the two that would fail
/// silently in the game are the stale board and the un-joined goal.
/// </summary>
public class CommunityGoalTests
{
    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static CommunityGoalBoard Fold(params string[] lines)
    {
        var board = CommunityGoalBoard.Empty;

        foreach (var line in lines)
        {
            board = board.Apply(Event(line));
        }

        return board;
    }

    /// <summary>One goal on a board, at a station, expiring when told to.</summary>
    private static string Board(
        int id = 726,
        string title = "Alliance Research Initiative",
        string expiry = "2026-08-20T14:58:14Z",
        string timestamp = "2026-08-15T10:00:00Z",
        long contribution = 562,
        string tier = "Tier 1") =>
        $$"""
          { "timestamp":"{{timestamp}}", "event":"CommunityGoal", "CurrentGoals":[ { "CGID":{{id}},
          "Title":"{{title}}", "SystemName":"Kaushpoos", "MarketName":"Neville Horizons",
          "Expiry":"{{expiry}}", "IsComplete":false, "CurrentTotal":10062,
          "PlayerContribution":{{contribution}}, "NumContributors":101,
          "TopTier":{ "Name":"Tier 5", "Bonus":"" }, "TopRankSize":10, "PlayerInTopRank":false,
          "TierReached":"{{tier}}", "PlayerPercentileBand":50, "Bonus":200000 } ] }
          """;

    [Fact]
    public void TheBoardIsReadIntoTheFieldsTheItemAsksFor()
    {
        var goal = Assert.Single(Fold(Board()).Goals);

        Assert.Equal(726, goal.Id);
        Assert.Equal("Alliance Research Initiative", goal.Title);
        Assert.Equal("Neville Horizons, Kaushpoos", goal.Where);

        // The tiers, which is what list.md asks for by name. Elite writes them as "Tier 1" and
        // "Tier 5"; a Commander plans around the numbers.
        Assert.Equal(1, goal.TierReached);
        Assert.Equal(5, goal.TopTier);

        Assert.Equal(101, goal.NumContributors);
        Assert.Equal(10062, goal.CurrentTotal);
        Assert.Equal(562, goal.PlayerContribution);
        Assert.Equal(50, goal.PlayerPercentileBand);
        Assert.Equal(200000, goal.Bonus);
    }

    [Fact]
    public void AGoalPastItsExpiryIsNotLiveHoweverRecentlyItWasReported()
    {
        // The load-bearing one. The corpus holds a board reported on 21 January for a goal that
        // ended on the 17th, carrying IsComplete: true — and because the event fires on every
        // dock, that is the common case rather than the edge one.
        var board = Fold(Board(expiry: "2026-08-11T16:00:00Z", timestamp: "2026-08-15T10:00:00Z"));

        Assert.Single(board.Goals);
        Assert.Empty(board.Live(DateTimeOffset.Parse("2026-08-15T10:00:00Z")));
    }

    [Fact]
    public void AnExpiryTheJournalDidNotGiveCountsAsLive()
    {
        // Hiding a goal because a field was missing is the wrong direction to fail in: the
        // Commander loses a goal that may well be running, and nothing tells them why.
        var board = Fold(
            """
            { "timestamp":"2026-08-15T10:00:00Z", "event":"CommunityGoal", "CurrentGoals":[
              { "CGID":9, "Title":"No deadline given", "SystemName":"Sol" } ] }
            """);

        Assert.Single(board.Live(DateTimeOffset.Parse("2026-08-15T10:00:00Z")));
    }

    [Fact]
    public void TwoStationsRunningDifferentGoalsKeepBoth()
    {
        // The case the corpus could not settle. A replace would have the second board wipe the
        // first, losing a goal the Commander may be running — the one failure here that is
        // silent.
        var board = Fold(
            Board(id: 726, title: "Alliance Research Initiative"),
            Board(id: 840, title: "Distant Worlds III", timestamp: "2026-08-15T11:00:00Z"));

        Assert.Equal(2, board.Goals.Count);
        Assert.NotNull(board.For(726));
        Assert.NotNull(board.For(840));
    }

    [Fact]
    public void TheSameGoalSeenAgainUpdatesRatherThanDuplicates()
    {
        var board = Fold(
            Board(contribution: 562),
            Board(contribution: 4000, timestamp: "2026-08-15T12:00:00Z"));

        var goal = Assert.Single(board.Goals);

        Assert.Equal(4000, goal.PlayerContribution);
        Assert.Equal(DateTimeOffset.Parse("2026-08-15T12:00:00Z"), goal.SeenAt);
    }

    [Fact]
    public void SigningUpSurvivesTheNextBoardEvent()
    {
        // The board says nothing about signing up, so a fresh read's defaults would un-join the
        // goal every time the Commander docked — and the answer would then read "not signed up"
        // about a goal they are actively flying.
        var board = Fold(
            Board(),
            """
            { "timestamp":"2026-08-15T10:30:00Z", "event":"CommunityGoalJoin", "CGID":726,
              "Name":"Alliance Research Initiative", "System":"Kaushpoos" }
            """,
            Board(timestamp: "2026-08-15T12:00:00Z"));

        Assert.Equal(CommunityGoalSignup.Joined, Assert.Single(board.Goals).Signup);
    }

    [Fact]
    public void OptingOutIsRecordedRatherThanForgotten()
    {
        var board = Fold(
            Board(),
            """
            { "timestamp":"2026-08-15T10:30:00Z", "event":"CommunityGoalDiscard", "CGID":726,
              "Name":"Alliance Research Initiative", "System":"Kaushpoos" }
            """);

        Assert.Equal(CommunityGoalSignup.Left, Assert.Single(board.Goals).Signup);
    }

    [Fact]
    public void AJoinForAGoalNoBoardHasReportedStillCounts()
    {
        // A Commander who joined a goal before d47 was watching is not a Commander who has not
        // joined it.
        var board = Fold(
            """
            { "timestamp":"2026-08-15T10:30:00Z", "event":"CommunityGoalJoin", "CGID":900,
              "Name":"Operation Andronicus", "System":"Pleiades Sector IR-W d1-55" }
            """);

        var goal = Assert.Single(board.Goals);

        Assert.Equal(900, goal.Id);
        Assert.Equal("Operation Andronicus", goal.Title);
        Assert.Equal(CommunityGoalSignup.Joined, goal.Signup);
        Assert.True(board.IsKnown);
    }

    [Fact]
    public void ARewardIsKeptAgainstTheGoalThatPaidIt()
    {
        // Which is also how "where did this module come from" gets answered: the Operations
        // spike found a pre-engineered module appearing in StoredModules at BuyPrice 0, thirty-two
        // seconds after this event.
        var board = Fold(
            Board(),
            """
            { "timestamp":"2026-08-21T09:00:00Z", "event":"CommunityGoalReward", "CGID":726,
              "Name":"Alliance Research Initiative", "System":"Kaushpoos", "Reward":1000000 }
            """);

        Assert.Equal(1000000, Assert.Single(board.Goals).RewardPaid);
    }

    [Fact]
    public void AnEventWithNoIdChangesNothing()
    {
        var board = Fold(
            Board(),
            """
            { "timestamp":"2026-08-15T10:30:00Z", "event":"CommunityGoalJoin", "Name":"Nameless" }
            """);

        Assert.Equal(CommunityGoalSignup.Unsaid, Assert.Single(board.Goals).Signup);
    }

    [Fact]
    public void NothingHeardIsNotTheSameAsNoGoals()
    {
        Assert.False(CommunityGoalBoard.Empty.IsKnown);
        Assert.True(Fold(Board()).IsKnown);
    }

    [Fact]
    public void ATierThatIsNotTheShapeEliteWritesIsDroppedRatherThanGuessedAt()
    {
        // A tier is an ordering a Commander plans around. A wrong one is worse than a missing
        // one, and "no tier reached yet" is what the journal means when it omits the field.
        var goal = Assert.Single(Fold(Board(tier: "nearly there")).Goals);

        Assert.Null(goal.TierReached);
        Assert.Equal(5, goal.TopTier);
    }

    [Fact]
    public void TheStateFoldsItAlongsideEverythingElse()
    {
        var state = new CommanderGameState(new CommanderIdentity("F123", "Jameson"));

        state.Apply(Event(Board()));

        Assert.Single(state.CommunityGoals.Goals);
    }
}
