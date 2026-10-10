using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>The Merc Coin ledger Elite writes into <c>Statistics</c>, reported in Merc Coins (#33).</summary>
public class AskedForMercCoinsTheBalanceIsReadOutTests
{
    private static GameStateStore Ledger()
    {
        var gameState = new GameStateStore();
        Apply(gameState, """{"timestamp":"2026-09-26T10:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""");
        Apply(
            gameState,
            """
            {"timestamp":"2026-09-26T10:00:30Z","event":"Statistics",
             "Bank_Account":{"Current_Wealth":1234567,"Spent_On_Ships":500000,
               "MercCoins_Current":46,"MercCoins_Total_Earned":1046,"MercCoins_Total_Spent":1000,
               "MercCoins_Spent_On_MercGear":750,"MercCoins_Spent_On_Engineering":250}}
            """);
        return gameState;
    }

    private static void Apply(GameStateStore gameState, string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        gameState.Apply(parsed!);
    }

    [Fact]
    public async Task EachMercCoinFigureIsInMercCoinsAndCreditsStayInCredits()
    {
        var result = await CapabilityRegistry.Build([JournalCapability.Create(Ledger())]).InvokeAsync(
            "get_commander_statistics",
            ToolArguments.FromJson("""{"section":"Bank_Account"}"""),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Contains("Merc Coins Current: 46 Merc Coins", result.Content, StringComparison.Ordinal);
        Assert.Contains("Merc Coins Total Earned: 1,046 Merc Coins", result.Content, StringComparison.Ordinal);
        Assert.Contains("Merc Coins Total Spent: 1,000 Merc Coins", result.Content, StringComparison.Ordinal);
        Assert.Contains("Merc Coins Spent On Merc Gear: 750 Merc Coins", result.Content, StringComparison.Ordinal);
        Assert.Contains("Merc Coins Spent On Engineering: 250 Merc Coins", result.Content, StringComparison.Ordinal);
        Assert.Contains("Current Wealth: 1,234,567 cr", result.Content, StringComparison.Ordinal);
        Assert.Contains("Spent On Ships: 500,000 cr", result.Content, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("how many merc coins do I have")]
    [InlineData("what's my merc coin balance")]
    [InlineData("my merc coins")]
    public void ItIsReachedWithoutTheModel(string said)
    {
        var install = new MemoryInstall();
        var command = new KeywordRouter(TestSurface.For(install).Registry).MatchToolCommand(said);

        Assert.Equal("get_commander_statistics", command?.ToolName);
        Assert.Equal("Bank_Account", command!.Arguments.TryGetString("section", out var section) ? section : null);
    }
}
