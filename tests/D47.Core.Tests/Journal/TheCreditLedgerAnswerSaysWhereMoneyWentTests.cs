using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class TheCreditLedgerAnswerSaysWhereMoneyWentTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-18T12:00:00Z");

    private const string Commander =
        """{"timestamp":"2026-09-01T09:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""";

    private static string Line(string time, string kind, string rest) =>
        "{\"timestamp\":\"" + time + "\",\"event\":\"" + kind + "\"," + rest + "}";

    private static string Login(string time, long credits) => Line(time, "LoadGame", "\"Credits\":" + credits);

    private static string Sell(string time, long total) => Line(time, "MarketSell", "\"TotalSale\":" + total);

    private static string Buy(string time, long total) => Line(time, "MarketBuy", "\"TotalCost\":" + total);

    private static string Explore(string time, long total) => Line(time, "SellExplorationData", "\"TotalEarnings\":" + total);

    private static GameStateStore Store(params string[] events)
    {
        var store = new GameStateStore();

        foreach (var line in new[] { Commander, Login("2026-09-01T09:01:00Z", 1_000_000_000) }.Concat(events))
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store;
    }

    private static async Task<string> Ask(
        GameStateStore store, string? json = null, HistoryState history = HistoryState.Done)
    {
        var capability = JournalCapability.Create(store, () => history, now: () => Now);
        var result = await CapabilityRegistry.Build([capability]).InvokeAsync(
            "get_credit_ledger", ToolArguments.FromJson(json), TestContext.Current.CancellationToken);

        Assert.False(result.IsError);

        return result.Spoken;
    }

    [Fact]
    public async Task TheFirstLineNamesThePeriodTheTotalsAndTheBalance()
    {
        var said = await Ask(Store(Sell("2026-10-17T10:00:00Z", 400_000_000), Buy("2026-10-17T11:00:00Z", 100_000_000)));

        Assert.StartsWith("In the last 7 days: 400 million in, 100 million out. Balance 1.3 billion.", said);
        Assert.Contains("Most in: trade 400 million.", said);
        Assert.Contains("Most out: trade 100 million.", said);
    }

    [Fact]
    public async Task AWeekLeavesOutATransactionEightDaysOldAndAllKeepsIt()
    {
        var store = Store(Sell("2026-10-17T10:00:00Z", 1000), Sell("2026-10-10T10:00:00Z", 5000));

        Assert.StartsWith("In the last 7 days: 1,000 in, 0 out.", await Ask(store));
        Assert.StartsWith("On record since 1 September 2026: 6,000 in, 0 out.", await Ask(store, """{"period":"all"}"""));
    }

    [Fact]
    public async Task FiveKindsInNameThreeAndCountTwoMore()
    {
        var said = await Ask(Store(
            Sell("2026-10-17T10:00:00Z", 5000),
            Explore("2026-10-17T10:01:00Z", 4000),
            Line("2026-10-17T10:02:00Z", "MissionCompleted", "\"Reward\":3000"),
            Line("2026-10-17T10:03:00Z", "SearchAndRescue", "\"Reward\":2000"),
            Line("2026-10-17T10:04:00Z", "CommunityGoalReward", "\"Reward\":1000")));

        Assert.Contains("Most in: trade 5,000, exploration 4,000, missions 3,000, and 2 more.", said);
    }

    [Fact]
    public async Task ABountyVoucherIsSpokenAsRedeemed()
    {
        var said = await Ask(Store(Line("2026-10-17T10:00:00Z", "RedeemVoucher", "\"Type\":\"bounty\",\"Amount\":2000")));

        Assert.Contains("Most in: bounties and bonds redeemed 2,000.", said);
    }

    [Theory]
    [InlineData(999_999, false)]
    [InlineData(1_000_000, true)]
    public async Task AGapIsSpokenOnlyFromAMillion(long gap, bool spoken)
    {
        var said = await Ask(Store(Sell("2026-10-17T10:00:00Z", 1000), Login("2026-10-17T11:00:00Z", 1_000_000_000 + 1000 + gap)));

        Assert.Equal(spoken, said.Contains("than the journal accounts for", StringComparison.Ordinal));
        Assert.Equal(spoken, said.Contains("more than", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ACarrierDepositIsMovedAndNotSpent()
    {
        var said = await Ask(Store(
            Sell("2026-10-17T10:00:00Z", 1000),
            Line("2026-10-17T10:05:00Z", "CarrierBankTransfer", "\"Deposit\":1000000000,\"Withdraw\":0,\"PlayerBalance\":1000"),
            Line("2026-10-17T10:06:00Z", "CarrierBankTransfer", "\"Deposit\":0,\"Withdraw\":200000000,\"PlayerBalance\":200001000")));

        Assert.Contains("You moved 1 billion to your carrier and 200 million back.", said);
        Assert.Contains("1,000 in, 0 out.", said);
    }

    [Fact]
    public async Task AKindGivesOnlyThatKindsLine()
    {
        var said = await Ask(
            Store(Explore("2026-10-01T10:00:00Z", 80_000_000), Sell("2026-10-17T10:00:00Z", 5000)),
            """{"kind":"exploration","period":"month"}""");

        Assert.Equal("Exploration in the last 30 days: 80 million in, 0 out, from 1 transaction.", said);
    }

    [Fact]
    public async Task NothingInThePeriodSaysSo()
    {
        var said = await Ask(Store(Sell("2026-09-01T10:00:00Z", 5000)));

        Assert.Equal("No credits moved in the last 7 days.", said);
    }

    [Fact]
    public async Task ARunningHistoryGivesNoFigures()
    {
        var said = await Ask(Store(Sell("2026-10-17T10:00:00Z", 5000)), history: HistoryState.Running);

        Assert.Equal("I have not finished reading the journal history yet.", said);
    }

    [Fact]
    public async Task AStoppedHistoryAnswersAndSaysItDidNotFinish()
    {
        var said = await Ask(Store(Sell("2026-10-17T10:00:00Z", 5000)), history: HistoryState.Stopped);

        Assert.StartsWith("In the last 7 days: 5,000 in", said);
        Assert.EndsWith("did not finish, so this is only what the live journal shows.", said);
    }

    [Fact]
    public async Task NoCommanderSaysSo()
    {
        Assert.Equal("I do not know which Commander is flying yet.", await Ask(new GameStateStore()));
    }
}
