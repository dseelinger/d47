using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>The model's view of community goals: what the journal reported.</summary>
public class CommunityGoalCapabilityTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-15T10:00:00Z");

    private static CommanderGameState Commander(params string[] lines)
    {
        var state = new CommanderGameState(new CommanderIdentity("F123", "Jameson"));

        foreach (var line in lines)
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            state.Apply(parsed!);
        }

        return state;
    }

    private static string Board(
        int id = 726,
        string title = "Alliance Research Initiative",
        string expiry = "2026-08-20T14:58:14Z") =>
        $$"""
          { "timestamp":"2026-08-15T09:00:00Z", "event":"CommunityGoal", "CurrentGoals":[
            { "CGID":{{id}}, "Title":"{{title}}", "SystemName":"Kaushpoos",
              "MarketName":"Neville Horizons", "Expiry":"{{expiry}}", "IsComplete":false,
              "CurrentTotal":10062, "PlayerContribution":562, "NumContributors":101,
              "TopTier":{ "Name":"Tier 5", "Bonus":"" }, "TopRankSize":10, "PlayerInTopRank":false,
              "TierReached":"Tier 1", "PlayerPercentileBand":50, "Bonus":200000 } ] }
          """;

    private static CapabilityRegistry Build(CommanderGameState? commander) =>
        CapabilityRegistry.Build([CommunityGoalCapability.Create(() => commander, () => Now)]);

    private static async Task<ToolResult> Ask(
        CapabilityRegistry registry,
        params (string Name, string Value)[] arguments) =>
        await registry.InvokeAsync(
            "get_community_goals",
            new ToolArguments(arguments.ToDictionary(a => a.Name, a => a.Value, StringComparer.Ordinal)),
            TestContext.Current.CancellationToken);

    [Fact]
    public async Task TheAnswerSaysItIsOnlyWhatTheJournalReported()
    {
        var registry = Build(Commander(Board()));

        var result = await Ask(registry);

        Assert.False(result.IsError);
        Assert.Contains("Alliance Research Initiative", result.Content, StringComparison.Ordinal);
        Assert.Contains("only what your journal has reported", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AGoalThatHasExpiredIsNotOfferedAsSomethingToFlyFor()
    {
        // The failure this whole capability is shaped around.
        var registry = Build(Commander(Board(expiry: "2026-08-11T16:00:00Z")));

        var result = await Ask(registry);

        Assert.DoesNotContain("Alliance Research Initiative", result.Content, StringComparison.Ordinal);
        Assert.Contains("No community goals are running", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AskingForFinishedGoalsBringsThemBackSayingWhenTheyEnded()
    {
        var registry = Build(Commander(Board(expiry: "2026-08-11T16:00:00Z")));

        var result = await Ask(registry, ("include_finished", "true"));

        Assert.Contains("Alliance Research Initiative", result.Content, StringComparison.Ordinal);
        Assert.Contains("ended", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NothingHeardYetIsNotTheSameAnswerAsNoGoals()
    {
        var registry = Build(Commander());

        var result = await Ask(registry);

        Assert.Contains("no community goals yet", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithNoJournalAtAllItSaysSoRatherThanReportingAnEmptyBoard()
    {
        var registry = Build(commander: null);

        var result = await Ask(registry);

        Assert.True(result.IsError);
    }

    [Fact]
    public async Task AskingForOneGoalByNameNarrowsToIt()
    {
        var registry = Build(
            Commander(Board(), Board(id: 840, title: "Distant Worlds III")));

        var result = await Ask(registry, ("name", "distant"));

        Assert.Contains("Distant Worlds III", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Alliance Research", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheCommandersOwnStandingIsReported()
    {
        var registry = Build(Commander(Board()));

        var result = await Ask(registry);

        Assert.Contains("562", result.Content, StringComparison.Ordinal);
        Assert.Contains("top 50%", result.Content, StringComparison.Ordinal);
        Assert.Contains("tier 1 of 5", result.Content, StringComparison.OrdinalIgnoreCase);
    }
}
