using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// "What is left for X" answers from <see cref="D47.Core.Engineers.EngineerAccess.CriteriaFor"/> with
/// no model turn, for every engineer in the directory (#266).
/// </summary>
public class WhatIsLeftForAnEngineerTests
{
    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static CapabilityRegistry BuildRegistry(GameStateStore gameState) =>
        CapabilityRegistry.Build([EngineerCapability.Create(() => gameState.Active)]);

    [Theory]
    [InlineData("what is left for Liz Ryder")]
    [InlineData("what's left for Liz Ryder")]
    [InlineData("what does Liz Ryder still need")]
    public void AllThreePhrasingsReachTheToolWithNoModel(string phrase)
    {
        var gameState = new GameStateStore();
        var router = new KeywordRouter(BuildRegistry(gameState));

        var match = router.MatchToolCommand(phrase);

        Assert.NotNull(match);
        Assert.Equal("get_engineer_prerequisites", match.ToolName);
        Assert.True(match.Arguments.TryGetString("engineer", out var name));
        Assert.Equal("Liz Ryder", name);
    }

    /// <summary>
    /// A phrase per engineer, so a name with a nickname folded into quotes routes the same way
    /// everybody else's does.
    /// </summary>
    [Fact]
    public void AnEngineerWithAQuotedNicknameStillRoutes()
    {
        var mcQuinn = EngineerDirectory.ByName("Tod McQuinn")!;
        var gameState = new GameStateStore();
        var router = new KeywordRouter(BuildRegistry(gameState));

        var match = router.MatchToolCommand($"what is left for {mcQuinn.Name}");

        Assert.NotNull(match);
        Assert.Equal("get_engineer_prerequisites", match.ToolName);
        Assert.True(match.Arguments.TryGetString("engineer", out var name));
        Assert.Equal(mcQuinn.Name, name);
    }

    /// <summary>Reached, routed and answered end to end, for a Commander with one line met and one not.</summary>
    [Fact]
    public async Task TheAnswerListsOnlyWhatIsStillUnmetWithItsReading()
    {
        var gameState = new GameStateStore();
        gameState.Apply(Event("""{"timestamp":"2026-09-01T09:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}"""));

        // Cordial with the Blue Mafia meets Liz Ryder's invitation task.
        gameState.Apply(Event(
            """{"timestamp":"2026-09-01T09:01:00Z","event":"Location","StarSystem":"Eurybia","Factions":[{"Name":"Eurybia Blue Mafia","MyReputation":10}]}"""));

        // 120 of the 200 Landmines her tribute asks for.
        gameState.Apply(Event(
            """{"timestamp":"2026-09-01T09:02:00Z","event":"EngineerContribution","EngineerID":300080,"Type":"Commodity","Commodity":"landmines","TotalQuantity":120}"""));

        var registry = BuildRegistry(gameState);
        var router = new KeywordRouter(registry);

        var match = router.MatchToolCommand("what's left for Liz Ryder");
        Assert.NotNull(match);

        var result = await registry.InvokeAsync(
            match.ToolName, match.Arguments, TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Contains("Provide 200 units of Landmines: 120 of 200 handed over.", result.Content, StringComparison.Ordinal);

        // The met line — the reputation task — is not repeated as something still owed.
        Assert.DoesNotContain("Cordial or Friendly", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnlockedEngineerAnswersThatNothingIsLeft()
    {
        var gameState = new GameStateStore();
        gameState.Apply(Event("""{"timestamp":"2026-09-01T09:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}"""));
        gameState.Apply(Event(
            """
            {"timestamp":"2026-09-01T09:01:00Z","event":"EngineerProgress","Engineers":[
              {"Engineer":"Liz Ryder","EngineerID":300080,"Progress":"Unlocked","Rank":1}]}
            """));

        var registry = BuildRegistry(gameState);
        var router = new KeywordRouter(registry);

        var match = router.MatchToolCommand("what is left for Liz Ryder");
        Assert.NotNull(match);

        var result = await registry.InvokeAsync(
            match.ToolName, match.Arguments, TestContext.Current.CancellationToken);

        Assert.Contains("Liz Ryder is unlocked", result.Content, StringComparison.Ordinal);
        Assert.Contains("Nothing is left", result.Content, StringComparison.Ordinal);
    }
}
