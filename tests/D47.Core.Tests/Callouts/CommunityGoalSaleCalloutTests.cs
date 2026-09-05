using D47.Core.Callouts;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>The running session total after a sale of the Community Goal commodity (#296).</summary>
public class CommunityGoalSaleCalloutTests
{
    private const string Fid = "F1234";

    private static readonly DateTimeOffset Noon = new(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);

    private static JournalEvent Event(string json) =>
        JournalEvent.TryParse(json, NullLogger.Instance, out var parsed) && parsed is not null ? parsed : throw new InvalidOperationException(json);

    private static JournalEvent LoadGame() =>
        Event($$"""{ "timestamp":"{{Noon:yyyy-MM-ddTHH:mm:ssZ}}", "event":"LoadGame", "FID":"{{Fid}}", "Commander":"Doug", "Credits":1000 }""");

    private static JournalEvent Sell(int count, int price, int paid, string type = "palladium", int market = 2) =>
        Event($$"""{ "timestamp":"{{Noon.AddMinutes(market):yyyy-MM-ddTHH:mm:ssZ}}", "event":"MarketSell", "MarketID":{{market}}, "Type":"{{type}}", "Count":{{count}}, "SellPrice":{{price}}, "TotalSale":{{count * price}}, "AvgPricePaid":{{paid}} }""");

    private static CommanderGameState State()
    {
        var state = new CommanderGameState(new CommanderIdentity(Fid, "Doug"));
        state.Apply(LoadGame());
        return state;
    }

    private static CalloutContext Context(IReadOnlyList<JournalEvent> events, bool priming = false) =>
        new(Noon, priming, State(), new GameStatus(), new NavRoute(), events);

    /// <summary>The host's arrangement: the ledger is folded first, then the callout looks.</summary>
    private static IReadOnlyList<Announcement> Tick(
        CommodityLedger ledger,
        CommunityGoalSaleCallout callout,
        IReadOnlyList<JournalEvent> events,
        bool priming = false)
    {
        ledger.Apply(events);
        return [.. callout.Examine(Context(events, priming))];
    }

    [Fact]
    public void ASaleOfTheCommoditySpeaksTheSessionsRunningTotal()
    {
        var ledger = new CommodityLedger();
        var callout = new CommunityGoalSaleCallout(ledger, new CommunityGoalSearch());

        Tick(ledger, callout, [LoadGame()]);

        var first = Assert.Single(Tick(ledger, callout, [Sell(100, 51_000, 48_200)]));

        Assert.Equal("That's 280,000 up this session.", first.Text);
        Assert.Equal(CommunityGoalSaleCallout.Key, first.Key);
        Assert.Equal(CalloutUrgency.Routine, first.Urgency);

        // The total, not the sale: the second line carries both sales.
        var second = Assert.Single(Tick(ledger, callout, [Sell(400, 52_000, 47_000, market: 3)]));

        Assert.Equal("That's 2.3 million up this session.", second.Text);
    }

    [Fact]
    public void ALossIsSaidAsDown()
    {
        var ledger = new CommodityLedger();
        var callout = new CommunityGoalSaleCallout(ledger, new CommunityGoalSearch());

        Tick(ledger, callout, [LoadGame()]);

        var said = Assert.Single(Tick(ledger, callout, [Sell(100, 40_000, 48_200)]));

        Assert.Equal("That's 820,000 down this session.", said.Text);
    }

    [Fact]
    public void NothingIsSaidDuringPriming()
    {
        var ledger = new CommodityLedger();
        var callout = new CommunityGoalSaleCallout(ledger, new CommunityGoalSearch());

        Assert.Empty(Tick(ledger, callout, [LoadGame(), Sell(100, 51_000, 48_200)], priming: true));

        // But the backlog was folded, so the first live sale reports the whole session.
        var live = Assert.Single(Tick(ledger, callout, [Sell(100, 51_000, 48_200, market: 3)]));

        Assert.Equal("That's 560,000 up this session.", live.Text);
    }

    [Fact]
    public void ASaleOfSomethingElseIsSilent()
    {
        var ledger = new CommodityLedger();
        var callout = new CommunityGoalSaleCallout(ledger, new CommunityGoalSearch());

        Tick(ledger, callout, [LoadGame()]);

        Assert.Empty(Tick(ledger, callout, [Sell(100, 9_000, 1_000, type: "gold")]));
    }

    [Fact]
    public void TheCommodityFollowsTheSavedSearch()
    {
        var ledger = new CommodityLedger();
        var search = new CommunityGoalSearch { Commodity = "Gold" };
        var callout = new CommunityGoalSaleCallout(ledger, search);

        Tick(ledger, callout, [LoadGame()]);

        var said = Assert.Single(Tick(ledger, callout, [Sell(100, 9_000, 1_000, type: "gold")]));

        Assert.Equal("That's 800,000 up this session.", said.Text);
    }

    [Fact]
    public void TheRowSwitchesItOffThroughTheEngine()
    {
        var ledger = new CommodityLedger();
        var callout = new CommunityGoalSaleCallout(ledger, new CommunityGoalSearch());
        var engine = new CalloutEngine(NullLogger<CalloutEngine>.Instance).Add(callout);

        ledger.Apply([LoadGame(), Sell(100, 51_000, 48_200)]);

        engine.SetEnabled(callout.Id, false);
        engine.Tick(Context([Sell(100, 51_000, 48_200)]));

        Assert.Empty(engine.Drain());

        engine.SetEnabled(callout.Id, true);
        engine.Tick(Context([Sell(100, 51_000, 48_200)]));

        Assert.Single(engine.Drain());
    }

    [Fact]
    public void TheRowExistsAndDefaultsOn()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var row = surface.Registry.All
            .SelectMany(capability => capability.Descriptor.Settings)
            .Single(row => row.Key == CalloutCapability.CommunityGoalSalesKey);

        Assert.Equal(SettingKind.Toggle, row.Kind);
        Assert.True(row.Protected);
        Assert.True(new CalloutSettings().CommunityGoalSales);
        Assert.Equal("true", row.Binding!.Read(D47Settings.Defaults));

        var off = row.Binding!.Write!(D47Settings.Defaults, "false");

        Assert.False(off!.Callouts.CommunityGoalSales);
    }
}
