using D47.Core.Callouts;
using D47.Core.Journal;
using D47.Core.Persona;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>The domain callout naming a community goal the Commander has not joined (#612).</summary>
public class QuartermasterNamesGoalsNotYetJoinedTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);

    private static JournalEvent Event(string json) =>
        JournalEvent.TryParse(json, NullLogger.Instance, out var parsed) && parsed is not null
            ? parsed
            : throw new InvalidOperationException(json);

    private static JournalEvent LoadGame() =>
        Event($$"""{ "timestamp":"{{Noon:yyyy-MM-ddTHH:mm:ssZ}}", "event":"LoadGame", "FID":"F1", "Commander":"Doug", "Credits":1000 }""");

    private static JournalEvent Board(int id, TimeSpan? left, string? bonus, long contribution = 0, bool complete = false)
    {
        var expiry = left is { } span ? $""", "Expiry":"{(Noon + span):yyyy-MM-ddTHH:mm:ssZ}" """ : "";
        var tier = bonus is null ? "" : $$""", "TopTier":{ "Name":"Tier 8", "Bonus":"{{bonus}}" }""";

        return Event($$"""
            { "timestamp":"{{Noon:yyyy-MM-ddTHH:mm:ssZ}}", "event":"CommunityGoal", "CurrentGoals":[
              { "CGID":{{id}}, "Title":"Rescue Goal {{id}}", "SystemName":"Sol", "MarketName":"Galileo",
                "IsComplete":{{(complete ? "true" : "false")}}, "PlayerContribution":{{contribution}}{{expiry}}{{tier}} } ] }
            """);
    }

    private static (DomainCallout Callout, CommanderGameState State) Ready(PersonaDomain domain = PersonaDomain.Earnings, bool enabled = true)
    {
        var state = new CommanderGameState(new CommanderIdentity("F1", "Doug"));
        state.Apply(LoadGame());

        return (new DomainCallout { Domain = () => domain, Enabled = () => enabled }, state);
    }

    private static List<Announcement> Tick(DomainCallout callout, CommanderGameState state, DateTimeOffset now, bool priming = false, params JournalEvent[] events)
    {
        foreach (var journalEvent in events)
        {
            state.Apply(journalEvent);
        }

        return [.. callout.Examine(new CalloutContext(now, priming, state, new GameStatus(), new NavRoute(), events))];
    }

    [Fact]
    public void AnUnjoinedGoalIsNamedOnceWithItsPlaceTimeAndReward()
    {
        var (callout, state) = Ready();

        var said = Assert.Single(Tick(callout, state, Noon, false, LoadGame(), Board(5, TimeSpan.FromHours(30), "Bonus credits")));

        Assert.Equal(DomainCallout.CommunityGoalKey, said.Key);
        Assert.Equal(
            "The Rescue Goal 5 community goal at Galileo, Sol has 30 hours left. The top tier pays: Bonus credits. You have not joined it.",
            said.Text);
        Assert.Empty(Tick(callout, state, Noon.AddMinutes(5)));
    }

    [Fact]
    public void LongerThanTwoDaysIsSaidInDays()
    {
        var (callout, state) = Ready();

        var said = Assert.Single(Tick(callout, state, Noon, false, Board(5, TimeSpan.FromDays(4), null)));

        Assert.Contains("has 4 days left.", said.Text);
    }

    [Fact]
    public void AGoalJoinedCompleteClosedOrNearlyOverIsSilent()
    {
        var (callout, state) = Ready();

        Assert.Empty(Tick(callout, state, Noon, false, Board(1, TimeSpan.FromHours(30), "x", contribution: 10)));
        Assert.Empty(Tick(callout, state, Noon, false, Board(2, TimeSpan.FromHours(30), "x", complete: true)));
        Assert.Empty(Tick(callout, state, Noon, false, Board(3, TimeSpan.FromHours(-2), "x")));
        Assert.Empty(Tick(callout, state, Noon, false, Board(4, TimeSpan.FromHours(23), "x")));
    }

    [Fact]
    public void NoKnownTimeAndNoRewardIsSilent()
    {
        var (callout, state) = Ready();

        Assert.Empty(Tick(callout, state, Noon, false, Board(6, null, null)));
    }

    [Fact]
    public void ARewardWithNoKnownTimeIsStillNamed()
    {
        var (callout, state) = Ready();

        var said = Assert.Single(Tick(callout, state, Noon, false, Board(6, null, "A bonus.")));

        Assert.Equal(
            "The Rescue Goal 6 community goal at Galileo, Sol is open. The top tier pays: A bonus. You have not joined it.",
            said.Text);
    }

    [Fact]
    public void ACoreWithoutTheSubjectOrWithTheSettingOffOrPrimingIsSilent()
    {
        var goal = Board(5, TimeSpan.FromHours(30), "x");

        var (none, noneState) = Ready(PersonaDomain.None);
        Assert.Empty(Tick(none, noneState, Noon, false, goal));

        var (off, offState) = Ready(enabled: false);
        Assert.Empty(Tick(off, offState, Noon, false, goal));

        var (priming, primingState) = Ready();
        Assert.Empty(Tick(priming, primingState, Noon, true, goal));
        Assert.Single(Tick(priming, primingState, Noon.AddSeconds(1)));
    }

    [Fact]
    public void ANewSessionNamesTheGoalAgain()
    {
        var (callout, state) = Ready();

        Assert.Single(Tick(callout, state, Noon, false, Board(5, TimeSpan.FromHours(30), "x")));
        Assert.Single(Tick(callout, state, Noon.AddHours(1), false, LoadGame()));
    }
}
