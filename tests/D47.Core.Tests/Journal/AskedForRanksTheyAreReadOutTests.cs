using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class AskedForRanksTheyAreReadOutTests
{
    private static void Apply(GameStateStore gameState, string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        gameState.Apply(parsed!);
    }

    private static GameStateStore Commander()
    {
        var gameState = new GameStateStore();
        Apply(gameState, """{"timestamp":"2026-09-23T10:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""");
        return gameState;
    }

    private static async Task<string> AskAsync(GameStateStore gameState)
    {
        var result = await CapabilityRegistry.Build([JournalCapability.Create(gameState)])
            .InvokeAsync("get_ranks", ToolArguments.Empty, TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        return result.Content;
    }

    [Fact]
    public async Task EveryCareerAndNavyIsGivenWithItsProgressAndNextRank()
    {
        var gameState = Commander();
        Apply(gameState, """{"timestamp":"2026-09-23T10:00:01Z","event":"Rank","Combat":3,"Trade":4,"Explore":5,"Empire":1,"Federation":2}""");
        Apply(gameState, """{"timestamp":"2026-09-23T10:00:01Z","event":"Progress","Combat":40,"Trade":20,"Explore":30,"Empire":93,"Federation":40}""");

        var answer = await AskAsync(gameState);

        Assert.Contains("Combat Rank 3 of 13, Competent, 40% into it. Next rank: Combat Rank 4 of 13, Expert.", answer, StringComparison.Ordinal);
        Assert.Contains("Trade Rank 4 of 13, Merchant, 20% into it.", answer, StringComparison.Ordinal);
        Assert.Contains("Exploration Rank 5 of 13, Pathfinder, 30% into it.", answer, StringComparison.Ordinal);
        Assert.Contains("Imperial Navy Rank 1 of 14, Outsider, 93% into it. Next rank: Imperial Navy Rank 2 of 14, Serf.", answer, StringComparison.Ordinal);
        Assert.Contains("Federal Navy Rank 2 of 14,", answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnreportedRanksAreNotReportedAsRankZero()
    {
        var answer = await AskAsync(Commander());

        Assert.Contains("has not reported your ranks", answer, StringComparison.Ordinal);
        Assert.DoesNotContain("rank 0", answer, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("what's my rank")]
    [InlineData("what are my ranks")]
    [InlineData("my rank")]
    public void ItIsReachedWithoutTheModel(string said)
    {
        var command = new KeywordRouter(TestSurface.For(new MemoryInstall()).Registry).MatchToolCommand(said);

        Assert.Equal("get_ranks", command?.ToolName);
    }
}
