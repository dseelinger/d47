using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Checklists;

/// <summary>
/// A delivery or collect mission becomes a universal checklist line on accept, is settled by
/// <c>CargoDepot</c>, and leaves with the mission (#665).
/// </summary>
public sealed class ADeliveryMissionTicksItselfTests
{
    private const long Diamonds = 1041612601;
    private const long Second = 1041612602;
    private const long Courier = 1066795391;

    private sealed class Bench(TempInstall install, GameStateStore game, ChecklistService checklists) : IDisposable
    {
        public GameStateStore Game => game;

        public ChecklistService Checklists => checklists;

        public IReadOnlyList<ChecklistItem> MissionLines() =>
            [.. checklists.Document.Items.Where(item => item.Source == ChecklistSource.Mission)];

        public ChecklistItem Line() => Assert.Single(MissionLines());

        public void Apply(string json)
        {
            game.Apply(Event(json));
            checklists.Poll();
        }

        public void Dispose() => install.Dispose();
    }

    private static Bench Set(int diamondsInHold = 0, bool removeFulfilled = false)
    {
        var install = new TempInstall();

        File.WriteAllLines(
            Path.Combine(install.Root, "Journal.2026-09-22T100000.01.log"),
            ["""{"timestamp":"2026-09-22T10:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}"""]);

        File.WriteAllText(
            Path.Combine(install.Root, CargoManifestReader.ManifestFile),
            $$"""
            { "timestamp":"2026-09-22T10:00:01Z", "event":"Cargo", "Vessel":"Ship", "Count":{{diamondsInHold}},
              "Inventory":[ { "Name":"lowtemperaturediamond", "Name_Localised":"Low Temperature Diamonds", "Count":{{diamondsInHold}}, "Stolen":0 } ] }
            """);

        var game = new GameStateStore();
        new JournalSpine(install.Root, game, NullLoggerFactory.Instance).Poll();

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(install.Root, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(install.Root, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => game.Active,
            removeFulfilled: () => removeFulfilled);

        return new Bench(install, game, checklists);
    }

    private static string Accept(long id, string name = "Mission_Delivery_Boom") =>
        $$"""
        {"timestamp":"2026-09-22T10:10:00Z","event":"MissionAccepted","Faction":"Party of Yoru","Name":"{{name}}",
         "LocalisedName":"Boom time delivery of 99 units of Low Temperature Diamonds",
         "Commodity":"$LowTemperatureDiamond_Name;","Commodity_Localised":"Low Temperature Diamonds","Count":99,
         "DestinationSystem":"Wadjuk","DestinationStation":"Crown Barracks","Expiry":"2026-09-27T13:43:06Z",
         "Wing":false,"Reward":2799184,"MissionID":{{id}}}
        """;

    private static string Depot(int delivered) =>
        $$"""
        {"timestamp":"2026-09-22T10:30:00Z","event":"CargoDepot","MissionID":{{Diamonds}},"UpdateType":"Deliver",
         "CargoType":"LowTemperatureDiamond","Count":{{delivered}},"ItemsCollected":99,"ItemsDelivered":{{delivered}},
         "TotalItemsToDeliver":99,"Progress":0.0}
        """;

    [Fact]
    public void DeliveriesMoveTheLineFromNoneToAllAndMarkItDone()
    {
        using var bench = Set();

        bench.Apply(Accept(Diamonds));

        var line = bench.Line();
        Assert.Equal("Deliver 99 Low Temperature Diamonds to Crown Barracks, Wadjuk", line.Text);
        Assert.Equal(ChecklistScope.Universal, line.Scope);
        Assert.Equal(ChecklistItemKind.Derived, line.Kind);
        Assert.Equal(ChecklistState.Open, line.State);
        Assert.Equal("None delivered yet. 0 of 99 Low Temperature Diamonds in the hold.", bench.Checklists.Verdict(line)?.Says);

        bench.Apply(Depot(40));
        Assert.Equal("40 of 99 Low Temperature Diamonds delivered.", bench.Checklists.Verdict(bench.Line())?.Says);
        Assert.Equal(ChecklistState.Open, bench.Line().State);

        bench.Apply(Depot(99));
        Assert.Equal("All 99 Low Temperature Diamonds delivered.", bench.Checklists.Verdict(bench.Line())?.Says);
        Assert.Equal(ChecklistState.Done, bench.Line().State);
    }

    [Fact]
    public void TheLineCannotBeTickedByHand()
    {
        using var bench = Set();
        bench.Apply(Accept(Diamonds));

        var line = bench.Line();
        var change = bench.Checklists.Complete(line.Id);

        Assert.False(line.TicksByHand);
        Assert.False(change.Changed);
        Assert.Equal(ChecklistState.Open, bench.Line().State);
    }

    [Fact]
    public void TheHoldIsJoinedOnTheSymbolAndTheMarketOnTheDisplayName()
    {
        using var bench = Set(diamondsInHold: 30);
        bench.Apply(Accept(Diamonds));

        Assert.Equal("None delivered yet. 30 of 99 Low Temperature Diamonds in the hold.", bench.Checklists.Verdict(bench.Line())?.Says);

        var state = bench.Game.Active!;
        var mission = state.Missions.For(Diamonds)!;

        Assert.Equal(30, state.Hold.Of(mission.Commodity));
        Assert.Equal(0, state.Hold.Of(mission.CommodityLocalised));

        var market = new MarketSnapshot
        {
            Station = "Crown Barracks",
            System = "Wadjuk",
            Quotes = new Dictionary<string, MarketQuote>(StringComparer.OrdinalIgnoreCase)
            {
                ["Low Temperature Diamonds"] = new MarketQuote("Low Temperature Diamonds") { Supply = 10 },
            },
        };

        Assert.NotNull(market.Quote(mission.CommodityLocalised!));
        Assert.Null(market.Quote(mission.Commodity!));
    }

    [Fact]
    public void AbandoningTheMissionRemovesTheLine()
    {
        using var bench = Set();
        bench.Apply(Accept(Diamonds));
        Assert.Single(bench.MissionLines());

        bench.Apply($$"""{"timestamp":"2026-09-22T11:00:00Z","event":"MissionAbandoned","Name":"Mission_Delivery_Boom","MissionID":{{Diamonds}}}""");

        Assert.Empty(bench.MissionLines());
    }

    [Fact]
    public void ACourierMissionAddsNoLine()
    {
        using var bench = Set();

        bench.Apply(
            $$"""
            {"timestamp":"2026-09-22T10:20:00Z","event":"MissionAccepted","Faction":"League of HR 6649 Front",
             "Name":"Mission_Courier","LocalisedName":"Courier Job Available","DestinationSystem":"Kweleutahe",
             "DestinationStation":"Merchiston Dock","Expiry":"2026-09-25T01:43:11Z","Wing":false,"Reward":79014,
             "MissionID":{{Courier}}}
            """);

        Assert.Empty(bench.MissionLines());
    }

    [Fact]
    public void TwoMissionsForTheSameCommodityAreTwoLines()
    {
        using var bench = Set();

        bench.Apply(Accept(Diamonds));
        bench.Apply(Accept(Second, "Mission_Collect"));

        Assert.Equal(2, bench.MissionLines().Count);
    }

    [Fact]
    public void ALineRemovedOnCompletionIsNotWrittenBack()
    {
        using var bench = Set(removeFulfilled: true);

        bench.Apply(Accept(Diamonds));
        bench.Apply(Depot(99));
        Assert.Empty(bench.MissionLines());

        bench.Checklists.Poll();
        Assert.Empty(bench.MissionLines());
    }

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json.ReplaceLineEndings(" "), NullLogger.Instance, out var parsed));
        return parsed!;
    }
}
