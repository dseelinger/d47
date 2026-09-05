using Microsoft.Extensions.Logging.Abstractions;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>"How have I done today", and this week, answered from the ledger (#296).</summary>
public class HowHaveIDoneTests
{
    private const string Fid = "F1234";

    private static readonly DateTimeOffset Noon = new(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);

    private static JournalEvent Event(string json) =>
        JournalEvent.TryParse(json, NullLogger.Instance, out var parsed) && parsed is not null ? parsed : throw new InvalidOperationException(json);

    private static JournalEvent LoadGame(DateTimeOffset at) =>
        Event($$"""{ "timestamp":"{{at:yyyy-MM-ddTHH:mm:ssZ}}", "event":"LoadGame", "FID":"{{Fid}}", "Commander":"Doug", "Credits":1000 }""");

    private static JournalEvent Sell(DateTimeOffset at, int count, int price, int paid, long market) =>
        Event($$"""{ "timestamp":"{{at:yyyy-MM-ddTHH:mm:ssZ}}", "event":"MarketSell", "MarketID":{{market}}, "Type":"palladium", "Count":{{count}}, "SellPrice":{{price}}, "TotalSale":{{count * price}}, "AvgPricePaid":{{paid}} }""");

    private static JournalEvent Goal(DateTimeOffset at, DateTimeOffset expiry) =>
        Event($$"""{ "timestamp":"{{at:yyyy-MM-ddTHH:mm:ssZ}}", "event":"CommunityGoal", "CurrentGoals":[ { "CGID":901, "Title":"Palladium Drive", "SystemName":"Ega", "MarketName":"Port", "Expiry":"{{expiry:yyyy-MM-ddTHH:mm:ssZ}}", "IsComplete":false } ] }""");

    private static (CapabilityRegistry Registry, CommodityLedger Ledger) Registry(TempInstall install)
    {
        var surface = TestSurface.For(install);
        var state = new CommanderGameState(new CommanderIdentity(Fid, "Doug"));
        var ledger = new CommodityLedger();

        // Yesterday's session, then today's, inside a goal window that opened three days ago.
        ledger.Apply(
        [
            Goal(Noon.AddDays(-3), Noon.AddDays(4)),
            LoadGame(Noon.AddDays(-1)),
            Sell(Noon.AddDays(-1), 100, 50_000, 40_000, 1),
            LoadGame(Noon.AddHours(-1)),
            Sell(Noon.AddMinutes(-30), 100, 50_000, 40_000, 2),
            Sell(Noon.AddMinutes(-10), 100, 50_000, 40_000, 3),
        ]);

        var registry = CapabilityRegistry.Build(
        [
            CommunityGoalCapability.Create(() => state, null, () => Noon, ledger, new CommunityGoalSearch()),
            .. surface.Registry.All.Where(c => c.Descriptor.Id != CommunityGoalCapability.Id).Select(c => c.Descriptor),
        ]);

        return (registry, ledger);
    }

    private static async Task<string> AskAsync(CapabilityRegistry registry, string? range)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        if (range is not null)
        {
            values["range"] = range;
        }

        var result = await registry.InvokeAsync(
            "get_community_goal_earnings", new ToolArguments(values), TestContext.Current.CancellationToken);

        Assert.False(result.IsError, result.Content);

        return result.Content;
    }

    [Fact]
    public async Task TheSessionIsSinceTheLastLoadGame()
    {
        using var install = new TempInstall();
        var (registry, _) = Registry(install);

        var said = await AskAsync(registry, null);

        Assert.StartsWith("Palladium: 2 million up this session", said, StringComparison.Ordinal);
        Assert.Contains("2 sales, 200 tonnes", said, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TodayAndTheWeekReachAcrossSessions()
    {
        using var install = new TempInstall();
        var (registry, _) = Registry(install);

        Assert.StartsWith("Palladium: 2 million up today", await AskAsync(registry, "today"), StringComparison.Ordinal);

        var week = await AskAsync(registry, "week");

        Assert.StartsWith("Palladium: 3 million up over Palladium Drive", week, StringComparison.Ordinal);
        Assert.Contains("the goal's own window", week, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithNoSalesItSaysSo()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);
        var state = new CommanderGameState(new CommanderIdentity(Fid, "Doug"));

        var registry = CapabilityRegistry.Build(
        [
            CommunityGoalCapability.Create(() => state, null, () => Noon, new CommodityLedger(), new CommunityGoalSearch()),
        ]);

        Assert.Equal("No Palladium sold today.", await AskAsync(registry, "today"));
    }

    [Fact]
    public async Task WithoutALedgerTheToolSaysNothingIsKeepingOne()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var result = await surface.Registry.InvokeAsync(
            "get_community_goal_earnings", new ToolArguments(new Dictionary<string, string>(StringComparer.Ordinal)), TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Contains("ledger", result.Content, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("how have I done today", "today")]
    [InlineData("How have I done this week?", "week")]
    [InlineData("how have i done this session", "session")]
    public void ThePhrasesReachTheToolWithoutAModel(string sentence, string range)
    {
        using var install = new TempInstall();
        var (registry, _) = Registry(install);
        var router = new KeywordRouter(registry);

        var match = router.MatchToolCommand(sentence);

        Assert.NotNull(match);
        Assert.Equal("get_community_goal_earnings", match.ToolName);
        Assert.True(match.Arguments.TryGetString("range", out var asked));
        Assert.Equal(range, asked);
    }
}
