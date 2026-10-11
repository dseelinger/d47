using D47.Core.Checklists;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Checklists;

/// <summary>An authored line with a watch ticks when a matching journal event arrives after the watch was set (#1056).</summary>
public class AWatchedLineTicksFromTheJournalTests
{
    private static readonly DateTimeOffset Set = DateTimeOffset.Parse("2026-10-07T19:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

    private const string Station = "Archambeau's Serenity +";

    private const long StationMarket = 4252074499;

    [Fact]
    public void AMatchingDockTicksTheLineAndSaysSo()
    {
        var (game, checklists) = Started(new MemoryInstall());
        var line = Save(checklists, "Dock at Jameson Memorial", new ChecklistWatch(ChecklistWatchKind.Dock, "Jameson Memorial")
        {
            Place = "Shinrarta Dezhra",
            Since = Set,
        });

        var docked = Docked("2026-10-07T19:10:00Z", "Jameson Memorial", "Shinrarta Dezhra", 128666762);
        game.Apply(docked);
        checklists.Poll(events: [docked]);

        Assert.Equal(ChecklistState.Done, Find(checklists, line).State);
        var news = Assert.Single(checklists.Drain());
        Assert.Equal($"checklist.done.{line.Id}", news.Key);
        Assert.Equal("Ticked \"Dock at Jameson Memorial\": you docked at Jameson Memorial.", news.Text);
    }

    [Fact]
    public void ADockInAnotherSystemOfTheSameNameDoesNotCount()
    {
        var (game, checklists) = Started(new MemoryInstall());
        var line = Save(checklists, "Dock at Jameson Memorial", new ChecklistWatch(ChecklistWatchKind.Dock, "Jameson Memorial")
        {
            Place = "Shinrarta Dezhra",
            Since = Set,
        });

        var docked = Docked("2026-10-07T19:10:00Z", "Jameson Memorial", "Sol", 1);
        game.Apply(docked);
        checklists.Poll(events: [docked]);

        Assert.Equal(ChecklistState.Open, Find(checklists, line).State);
    }

    [Fact]
    public void AnEventOlderThanTheWatchDoesNotCount()
    {
        var (game, checklists) = Started(new MemoryInstall());
        var line = Save(checklists, "Go to LTT 7786", new ChecklistWatch(ChecklistWatchKind.Arrive, "LTT 7786") { Since = Set });

        var jump = Jump("2026-10-07T18:59:59Z", "LTT 7786");
        game.Apply(jump);
        checklists.Poll(events: [jump]);

        Assert.Equal(ChecklistState.Open, Find(checklists, line).State);
        Assert.Empty(checklists.Drain());
    }

    [Fact]
    public void ACarrierJumpCountsAsAnArrival()
    {
        var (game, checklists) = Started(new MemoryInstall());
        var line = Save(checklists, "Take the carrier to LTT 7786", new ChecklistWatch(ChecklistWatchKind.Arrive, "LTT 7786") { Since = Set });

        var jump = Event(
            """
            {"timestamp":"2026-10-07T19:30:00Z","event":"CarrierJump","Docked":true,"StationName":"BNH-T2F",
             "StationType":"FleetCarrier","MarketID":3715429376,"StarSystem":"LTT 7786","SystemAddress":633608311522}
            """);
        game.Apply(jump);
        checklists.Poll(events: [jump]);

        Assert.Equal(ChecklistState.Done, Find(checklists, line).State);
    }

    [Fact]
    public void TwoPurchasesReachTheQuantityTogether()
    {
        var (game, checklists) = Started(new MemoryInstall());
        var line = Save(checklists, "Buy 5 Hydrogen Fuel", Buy(quantity: 5, place: Station));
        DockAt(game, checklists, "2026-10-07T19:05:00Z");

        var first = MarketBuy("2026-10-07T19:08:25Z", count: 2);
        checklists.Poll(events: [first]);

        var partway = Find(checklists, line);
        Assert.Equal(ChecklistState.Open, partway.State);
        Assert.Equal(2, partway.Watch!.Progress);
        Assert.Equal(
            $"Ticks when you buy 5 Hydrogen Fuel at {Station} · 2 of 5 so far",
            ChecklistWatching.Describe(partway.Watch));

        var second = MarketBuy("2026-10-07T19:09:00Z", count: 3);
        checklists.Poll(events: [second]);

        Assert.Equal(ChecklistState.Done, Find(checklists, line).State);
        var news = Assert.Single(checklists.Drain());
        Assert.Equal("Ticked \"Buy 5 Hydrogen Fuel\": you bought 5 Hydrogen Fuel.", news.Text);
    }

    [Fact]
    public void TwoPurchasesInTheSameSecondBothCount()
    {
        var (game, checklists) = Started(new MemoryInstall());
        var line = Save(checklists, "Buy 5 Hydrogen Fuel", Buy(quantity: 5, place: Station));
        DockAt(game, checklists, "2026-10-07T19:05:00Z");

        checklists.Poll(events: [MarketBuy("2026-10-07T19:08:25Z", count: 2), MarketBuy("2026-10-07T19:08:25Z", count: 3)]);

        Assert.Equal(ChecklistState.Done, Find(checklists, line).State);
    }

    [Fact]
    public void APurchaseReplayedAfterARestartIsNotCountedTwice()
    {
        var install = new MemoryInstall();
        var (game, checklists) = Started(install);
        var line = Save(checklists, "Buy 5 Hydrogen Fuel", Buy(quantity: 5, place: Station));
        DockAt(game, checklists, "2026-10-07T19:05:00Z");

        var docked = Docked("2026-10-07T19:05:00Z", Station, "LTT 7786", StationMarket);
        var bought = MarketBuy("2026-10-07T19:08:25Z", count: 3);
        checklists.Poll(events: [bought]);
        Assert.Equal(3, Find(checklists, line).Watch!.Progress);

        // The priming tick after a restart replays the whole newest journal file.
        var restarted = TestSurface.Checklists(install.Paths, game);
        restarted.Poll(announce: false, events: [docked, bought]);

        var after = Find(restarted, line);
        Assert.Equal(ChecklistState.Open, after.State);
        Assert.Equal(3, after.Watch!.Progress);
    }

    [Fact]
    public void APurchaseAtAnotherStationDoesNotCount()
    {
        var (game, checklists) = Started(new MemoryInstall());
        var line = Save(checklists, "Buy 5 Hydrogen Fuel", Buy(quantity: 5, place: Station));

        var elsewhere = Docked("2026-10-07T19:05:00Z", "Jameson Memorial", "Shinrarta Dezhra", 128666762);
        game.Apply(elsewhere);
        var bought = MarketBuy("2026-10-07T19:08:25Z", count: 5, market: 128666762);
        checklists.Poll(events: [elsewhere, bought]);

        var after = Find(checklists, line);
        Assert.Equal(ChecklistState.Open, after.State);
        Assert.Equal(0, after.Watch!.Progress);
    }

    [Fact]
    public void APurchaseIsPlacedByTheDockInTheSameBatchRatherThanWhereTheCommanderEndedUp()
    {
        var (game, checklists) = Started(new MemoryInstall());
        var line = Save(checklists, "Buy 5 Hydrogen Fuel", Buy(quantity: 5, place: Station));

        var docked = Docked("2026-10-07T19:05:00Z", Station, "LTT 7786", StationMarket);
        var bought = MarketBuy("2026-10-07T19:08:25Z", count: 5);
        var away = Docked("2026-10-07T19:20:54Z", "BNH-T2F", "LTT 7786", 3715429376);
        game.Apply(docked);
        game.Apply(away);
        checklists.Poll(announce: false, events: [docked, bought, away]);

        Assert.Equal(ChecklistState.Done, Find(checklists, line).State);
        Assert.Empty(checklists.Drain());
    }

    [Fact]
    public void ASaleCountsTheSoldCommodity()
    {
        var (game, checklists) = Started(new MemoryInstall());
        var line = Save(checklists, "Sell the fuel", new ChecklistWatch(ChecklistWatchKind.Sell, "Hydrogen Fuel") { Since = Set });

        var sold = Event(
            """
            {"timestamp":"2026-10-07T19:21:18Z","event":"MarketSell","MarketID":3715429376,"Type":"hydrogenfuel",
             "Count":5,"SellPrice":154,"TotalSale":770,"AvgPricePaid":85}
            """);
        checklists.Poll(events: [sold]);

        Assert.Equal(ChecklistState.Done, Find(checklists, line).State);
        Assert.Equal("Ticked \"Sell the fuel\": you sold 5 Hydrogen Fuel.", Assert.Single(checklists.Drain()).Text);
    }

    [Fact]
    public void ABuyOrderTicksOnceItIsLargeEnough()
    {
        var (_, checklists) = Started(new MemoryInstall());
        var line = Save(checklists, "Put up a fuel order", new ChecklistWatch(ChecklistWatchKind.BuyOrder, "hydrogenfuel")
        {
            Quantity = 5,
            Since = Set,
        });

        checklists.Poll(events: [Order("2026-10-07T19:16:00Z", 4)]);
        Assert.Equal(ChecklistState.Open, Find(checklists, line).State);

        checklists.Poll(events: [Order("2026-10-07T19:16:28Z", 5)]);
        Assert.Equal(ChecklistState.Done, Find(checklists, line).State);
        Assert.Equal(
            "Ticked \"Put up a fuel order\": your carrier has a buy order for 5 Hydrogen Fuel.",
            Assert.Single(checklists.Drain()).Text);
    }

    [Fact]
    public void UntickingByHandRearmsTheWatch()
    {
        var clock = DateTimeOffset.Parse("2026-10-07T20:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        var install = new MemoryInstall();
        var game = Commander();
        var checklists = TestSurface.Checklists(install.Paths, game, now: () => clock);
        checklists.Poll(announce: false);
        var line = Save(checklists, "Buy 5 Hydrogen Fuel", Buy(quantity: 5, place: null));

        var bought = MarketBuy("2026-10-07T19:08:25Z", count: 5);
        checklists.Poll(events: [bought]);
        Assert.Equal(ChecklistState.Done, Find(checklists, line).State);

        Assert.True(checklists.Uncomplete(line.Id).Changed);

        var rearmed = Find(checklists, line);
        Assert.Equal(ChecklistState.Open, rearmed.State);
        Assert.Equal(0, rearmed.Watch!.Progress);
        Assert.Null(rearmed.Watch.CountedTo);
        Assert.Equal(clock, rearmed.Watch.Since);

        // The purchase that ticked it before the untick does not tick it again.
        checklists.Poll(announce: false, events: [bought]);
        Assert.Equal(ChecklistState.Open, Find(checklists, line).State);
    }

    [Fact]
    public void TickingByHandLeavesTheWatchAsItIs()
    {
        var (_, checklists) = Started(new MemoryInstall());
        var line = Save(checklists, "Buy 5 Hydrogen Fuel", Buy(quantity: 5, place: null));
        checklists.Poll(events: [MarketBuy("2026-10-07T19:08:25Z", count: 2)]);
        var before = Find(checklists, line).Watch;

        Assert.True(checklists.Complete(line.Id).Changed);

        Assert.Equal(before, Find(checklists, line).Watch);
    }

    [Fact]
    public void AnOffDutyTickTicksNothing()
    {
        var (_, checklists) = Started(new MemoryInstall());
        var line = Save(checklists, "Go to LTT 7786", new ChecklistWatch(ChecklistWatchKind.Arrive, "LTT 7786") { Since = Set });

        checklists.Poll(events: []);

        Assert.Equal(ChecklistState.Open, Find(checklists, line).State);
        Assert.Null(Find(checklists, line).Watch!.CountedTo);
    }

    [Fact]
    public void ThePrimingTickTicksALineSilently()
    {
        var install = new MemoryInstall();
        var game = Commander();
        var checklists = TestSurface.Checklists(install.Paths, game);
        var line = Save(checklists, "Go to LTT 7786", new ChecklistWatch(ChecklistWatchKind.Arrive, "LTT 7786") { Since = Set });

        var jump = Jump("2026-10-07T19:18:31Z", "LTT 7786");
        game.Apply(jump);
        checklists.Poll(announce: false, events: [jump]);

        Assert.Equal(ChecklistState.Done, Find(checklists, line).State);
        Assert.Empty(checklists.Drain());
    }

    [Fact]
    public void TheReportSaysWhatTheLineIsWaitingFor()
    {
        var (_, checklists) = Started(new MemoryInstall());
        Save(checklists, "Buy 5 Hydrogen Fuel", Buy(quantity: 5, place: Station));

        Assert.Contains(
            $"[ ] Buy 5 Hydrogen Fuel — Ticks when you buy 5 Hydrogen Fuel at {Station}",
            checklists.Report(),
            StringComparison.Ordinal);
    }

    private static ChecklistWatch Buy(int quantity, string? place) =>
        new(ChecklistWatchKind.Buy, "Hydrogen Fuel") { Quantity = quantity, Place = place, Since = Set };

    private static GameStateStore Commander()
    {
        var game = new GameStateStore();
        game.Apply(Event("""{"timestamp":"2026-10-07T18:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}"""));
        return game;
    }

    private static (GameStateStore Game, ChecklistService Checklists) Started(MemoryInstall install)
    {
        var game = Commander();
        var checklists = TestSurface.Checklists(install.Paths, game);
        checklists.Poll(announce: false);
        return (game, checklists);
    }

    private static void DockAt(GameStateStore game, ChecklistService checklists, string when)
    {
        var docked = Docked(when, Station, "LTT 7786", StationMarket);
        game.Apply(docked);
        checklists.Poll(events: [docked]);
    }

    private static ChecklistItem Save(ChecklistService checklists, string text, ChecklistWatch watch)
    {
        var item = new ChecklistItem
        {
            Key = "note-1",
            Scope = ChecklistScope.Universal,
            Kind = ChecklistItemKind.Authored,
            Text = text,
            Watch = watch,
        };

        checklists.List.Save([ChecklistDocument.For("F1", "Jameson") with { Items = [item] }]);
        return item;
    }

    private static ChecklistItem Find(ChecklistService checklists, ChecklistItem item) =>
        Assert.Single(checklists.Document.Items, candidate => candidate.Id.Same(item.Id));

    private static JournalEvent Docked(string when, string station, string system, long market) => Event(
        $$"""
        {"timestamp":"{{when}}","event":"Docked","StationName":"{{station}}","StationType":"Coriolis",
         "StarSystem":"{{system}}","SystemAddress":1,"MarketID":{{market}}}
        """);

    private static JournalEvent Jump(string when, string system) => Event(
        $$"""
        {"timestamp":"{{when}}","event":"FSDJump","StarSystem":"{{system}}","SystemAddress":633608311522,
         "StarPos":[7.96875,-41.90625,77.875]}
        """);

    private static JournalEvent MarketBuy(string when, int count, long market = StationMarket) => Event(
        $$"""
        {"timestamp":"{{when}}","event":"MarketBuy","MarketID":{{market}},"Type":"hydrogenfuel",
         "Type_Localised":"Hydrogen Fuel","Count":{{count}},"BuyPrice":85,"TotalCost":{{count * 85}}}
        """);

    private static JournalEvent Order(string when, int order) => Event(
        $$"""
        {"timestamp":"{{when}}","event":"CarrierTradeOrder","CarrierID":3715429376,"CarrierType":"FleetCarrier",
         "BlackMarket":false,"Commodity":"hydrogenfuel","Commodity_Localised":"Hydrogen Fuel",
         "PurchaseOrder":{{order}},"Price":154}
        """);

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json.ReplaceLineEndings(" "), NullLogger.Instance, out var parsed));
        return parsed!;
    }
}
