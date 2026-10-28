using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Ships;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Ships;

/// <summary>Buying a planned hull ticks its "Buy a Python" line and moves the planned list onto the ship (#1831).</summary>
public class ABoughtHullTakesItsPlannedListTests
{
    private const string FiveA = "int_engine_size5_class5";

    private const string Commander =
        """{ "timestamp":"2026-08-18T08:00:00Z", "event":"Commander", "FID":"F1", "Name":"Ada Quill" }""";

    private const string StoredThrusters =
        """
        { "timestamp":"2026-08-18T08:30:00Z", "event":"StoredModules", "StarSystem":"Deciat",
          "MarketID":1, "StationName":"Garay Terminal",
          "Items":[ { "Name":"$int_engine_size5_class5_name;", "Name_Localised":"Thrusters",
                      "StarSystem":"Deciat", "TransferCost":21000 } ] }
        """;

    private const string BoughtAPython =
        """{ "timestamp":"2026-08-18T09:00:00Z", "event":"ShipyardNew", "ShipType":"python", "NewShipID":21 }""";

    private const string BoardedThePython =
        """
        { "timestamp":"2026-08-18T09:00:05Z", "event":"Loadout", "Ship":"python", "ShipID":21,
          "Modules":[ { "Slot":"MainEngines", "Item":"int_engine_size5_class1", "On":true } ] }
        """;

    /// <summary>Journal state, the ship plans and the checklist, fed the same events as the app feeds them.</summary>
    private sealed class Rig
    {
        public GameStateStore Game { get; } = new();

        public ShipPlanService Ships { get; }

        public ChecklistService Checklists { get; }

        public ShipBuildStore Builds { get; }

        public Rig(bool removeFulfilled = false)
        {
            var install = new MemoryInstall();

            Checklists = new ChecklistService(
                new ChecklistStore(
                    Path.Combine(install.Root, "checklist.json"),
                    new MemoryFileSystem(),
                    NullLogger<ChecklistStore>.Instance),
                new ChecklistProposalStore(
                    Path.Combine(install.Root, "checklist-proposals.json"),
                    new MemoryFileSystem(),
                    NullLogger<ChecklistProposalStore>.Instance),
                () => Game.Active,
                removeFulfilled: () => removeFulfilled);

            Builds = new ShipBuildStore(
                Path.Combine(install.Root, "ships.json"), install.Files, NullLogger<ShipBuildStore>.Instance);

            Ships = new ShipPlanService(Builds, Checklists, () => Game.Active);

            Play(Commander);
        }

        public IReadOnlyList<string> Play(params string[] lines)
        {
            var events = new List<JournalEvent>();

            foreach (var line in lines)
            {
                Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
                Game.Apply(parsed!);
                events.Add(parsed!);
            }

            var said = Ships.Observe(events);
            Checklists.Poll(events: events);

            return said;
        }

        /// <summary>A promoted Python plan: a module-only slot, a module with engineering, and engineering alone.</summary>
        public ShipBuild Planned(string? name = null)
        {
            var build = Ships.Intend("python", name)!;

            Ships.Plan(build.Id, new SlotPlan("Hardpoint1", Module: "Pulse Laser"));
            Ships.Plan(build.Id, new SlotPlan("MainEngines", Module: "Thrusters", Blueprint: "Dirty Drive Tuning", Grade: 5) { Variant = FiveA });
            Ships.Plan(build.Id, new SlotPlan("PowerPlant", Blueprint: "Overcharged", Grade: 3));
            Ships.Promote(build.Id);

            return build;
        }

        public IReadOnlyList<ChecklistItem> In(ChecklistScope scope) =>
            [.. Checklists.Document.Items.Where(item => item.Scope.Same(scope))];
    }

    private static bool IsHull(ChecklistItem item) => item.Intent?.Kind == ChecklistIntentKind.Hull;

    [Fact]
    public void BuyingThePlannedHullTicksItsBuyLineAndSaysSo()
    {
        var rig = new Rig();
        rig.Planned();

        var said = rig.Play(BoughtAPython);

        Assert.Equal("That Python is yours now, and the plan you had for one is pointed at it.", Assert.Single(said));

        var hull = Assert.Single(rig.In(ChecklistScope.Ship(21)), IsHull);

        Assert.Equal("Buy a Python", hull.Text);
        Assert.Equal(ChecklistState.Done, hull.State);
        Assert.Contains(rig.Checklists.Drain(), news => news.Text == "Ticked \"Buy a Python\".");
    }

    [Fact]
    public void WithFulfilledLinesRemovedTheBuyLineGoesAndIsStillSaid()
    {
        var rig = new Rig(removeFulfilled: true);
        var build = rig.Planned();

        rig.Play(BoughtAPython);

        Assert.DoesNotContain(rig.Checklists.Document.Items, IsHull);
        Assert.Empty(rig.In(build.Scope));
        Assert.Equal(4, rig.In(ChecklistScope.Ship(21)).Count);
        Assert.Contains(rig.Checklists.Drain(), news => news.Text == "Ticked \"Buy a Python\".");
    }

    [Fact]
    public void TheOtherLinesMoveToTheShipKeepingTheirKeysAndState()
    {
        var rig = new Rig();
        var build = rig.Planned("Bad Idea");
        var planned = rig.In(build.Scope).Where(item => !IsHull(item)).ToList();

        rig.Play(BoughtAPython);

        var moved = rig.In(ChecklistScope.Ship(21)).Where(item => !IsHull(item)).ToList();

        Assert.Equal(planned.Select(item => (item.Key, item.State, item.Text)), moved.Select(item => (item.Key, item.State, item.Text)));
        Assert.All(moved, item => Assert.Equal("python", item.Hull));
        Assert.All(moved, item => Assert.Null(item.BuildName));

        // The planned list is gone, and no list is shown for it.
        Assert.Empty(rig.In(build.Scope));
        Assert.DoesNotContain(rig.Checklists.Lists(), list => list.Name.EndsWith("(planned)", StringComparison.Ordinal));
    }

    [Fact]
    public void AModuleBoughtIntoStorageStaysDoneOnTheBoughtShip()
    {
        var rig = new Rig();
        var build = rig.Planned();

        rig.Play(StoredThrusters);

        static bool Thrusters(ChecklistItem item) =>
            item.Intent is { Kind: ChecklistIntentKind.Module, Subject: "MainEngines" };

        Assert.Equal(ChecklistState.Done, Assert.Single(rig.In(build.Scope), Thrusters).State);

        // Bought, and not boarded yet: the ship has an id and no loadout.
        rig.Play(BoughtAPython);
        Assert.Equal(ChecklistState.Done, Assert.Single(rig.In(ChecklistScope.Ship(21)), Thrusters).State);

        // Boarded with the stock thrusters fitted and the planned ones still in storage.
        rig.Play(BoardedThePython);
        Assert.Equal(ChecklistState.Done, Assert.Single(rig.In(ChecklistScope.Ship(21)), Thrusters).State);
    }

    [Fact]
    public void TwoPlannedHullsOfOneTypeKeepTheirListsWhenOneIsBought()
    {
        var rig = new Rig();
        var one = rig.Planned("one");
        var two = rig.Planned("two");

        var said = rig.Play(BoughtAPython);

        Assert.Empty(said);
        Assert.Equal(5, rig.In(one.Scope).Count);
        Assert.Equal(5, rig.In(two.Scope).Count);
        Assert.All(rig.In(one.Scope).Concat(rig.In(two.Scope)), item => Assert.Equal(ChecklistState.Open, item.State));
        Assert.Empty(rig.In(ChecklistScope.Ship(21)));
    }

    [Fact]
    public void ANoteMovesWithItsListAndTakesAFreshKeyWhereTheShipsListHasItsOwn()
    {
        var rig = new Rig();
        var build = rig.Planned();

        rig.Checklists.AddNote(ChecklistScope.Ship(21), "Polish the canopy");
        var note = rig.Checklists.AddNote(build.Scope, "Ask about a discount");
        var key = note.Document.Find(note.Subject!.Value)!.Key;

        rig.Play(BoardedThePython);

        var notes = rig.In(ChecklistScope.Ship(21)).Where(item => item.Kind == ChecklistItemKind.Authored).ToList();

        Assert.Equal(["Polish the canopy", "Ask about a discount"], notes.Select(item => item.Text));
        Assert.Equal(2, notes.Select(item => item.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains(notes, item => item.Key == key);
        Assert.Empty(rig.In(build.Scope));
    }

    [Fact]
    public void AnOfferWaitingForThePlannedShipIsPointedAtTheBoughtOne()
    {
        var rig = new Rig();
        var build = rig.Planned();

        rig.Checklists.ProposeAdd(build.Scope, ["Fit a docking computer"]);

        rig.Play(BoughtAPython);

        var waiting = Assert.Single(rig.Checklists.Proposals.PendingFor("F1"));

        Assert.Equal(ChecklistScope.Ship(21), waiting.Scope);
        Assert.All(waiting.Items, item => Assert.Equal(ChecklistScope.Ship(21), item.Scope));
    }

    [Fact]
    public void TheSelectedLineIsStillSelectedAfterTheMove()
    {
        var rig = new Rig();
        var build = rig.Planned();
        var engineering = rig.In(build.Scope).First(item => item.Intent?.Kind == ChecklistIntentKind.Blueprint);

        rig.Checklists.Select(engineering.Id);
        rig.Play(BoughtAPython);

        Assert.Equal(new ChecklistItemId(ChecklistScope.Ship(21), engineering.Key), rig.Checklists.Selected);
    }

    [Fact]
    public void APurchaseSeenOnASilentTickIsNotSaidOnTheNextOne()
    {
        var rig = new Rig();
        rig.Planned();

        Assert.True(JournalEvent.TryParse(BoughtAPython, NullLogger.Instance, out var bought));
        rig.Game.Apply(bought!);
        rig.Ships.Observe([bought!], announce: false);
        rig.Checklists.Poll();

        Assert.Equal(ChecklistState.Done, Assert.Single(rig.In(ChecklistScope.Ship(21)), IsHull).State);
        Assert.DoesNotContain(rig.Checklists.Drain(), news => news.Text.StartsWith("Ticked", StringComparison.Ordinal));
    }

    [Fact]
    public void ASelectedNoteThatTakesAFreshKeyIsStillTheOneSelected()
    {
        var rig = new Rig();
        var build = rig.Planned();

        rig.Checklists.AddNote(ChecklistScope.Ship(21), "Polish the canopy");
        rig.Checklists.AddNote(build.Scope, "Ask about a discount");

        rig.Play(BoardedThePython);

        var selected = rig.Checklists.Document.Find(rig.Checklists.Selected!.Value);

        Assert.Equal("Ask about a discount", selected?.Text);
        Assert.Equal(ChecklistScope.Ship(21), selected?.Scope);
    }

    [Fact]
    public void AReplayedPurchaseAndBoardingLeaveThePlanOnTheShipsList()
    {
        const string root = @"C:\d47-test\journals";
        const string file = "Journal.2026-08-18T090000.01.log";

        var files = new MemoryFileSystem();
        var game = new GameStateStore();
        var spine = new JournalSpine(root, files, game, NullLoggerFactory.Instance);
        var install = new MemoryInstall();

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(install.Root, "checklist.json"), new MemoryFileSystem(), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(install.Root, "checklist-proposals.json"), new MemoryFileSystem(), NullLogger<ChecklistProposalStore>.Instance),
            () => game.Active);

        var ships = new ShipPlanService(
            new ShipBuildStore(Path.Combine(install.Root, "ships.json"), install.Files, NullLogger<ShipBuildStore>.Instance),
            checklists,
            () => game.Active);

        void Tick()
        {
            var events = spine.Poll();

            ships.Observe(events);
            checklists.Poll(events: events);
        }

        // The session up to the shipyard, then the plan, then the purchase and boarding.
        var journal = EmbeddedFixture.Lines("ships." + file);
        var bought = journal.ToList().FindIndex(line => line.Contains("\"ShipyardBuy\"", StringComparison.Ordinal));

        files.WriteText(Path.Combine(root, file), string.Join("\n", journal.Take(bought)) + "\n");
        Tick();

        var build = ships.Intend("python")!;

        ships.Plan(build.Id, new SlotPlan("MainEngines", Module: "Thrusters", Blueprint: "Dirty Drive Tuning", Grade: 5) { Variant = FiveA });
        ships.Plan(build.Id, new SlotPlan("PowerPlant", Blueprint: "Overcharged", Grade: 3));
        ships.Promote(build.Id);

        Assert.Equal(4, checklists.Document.Items.Count(item => item.Scope.Same(build.Scope)));

        files.WriteText(Path.Combine(root, file), string.Join("\n", journal) + "\n");
        Tick();

        var ship = checklists.Document.Items.Where(item => item.Scope.Same(ChecklistScope.Ship(7))).ToList();

        Assert.DoesNotContain(checklists.Document.Items, item => item.Scope.Same(build.Scope));
        Assert.Equal(4, ship.Count);
        Assert.Equal(ChecklistState.Done, Assert.Single(ship, IsHull).State);
        Assert.Equal(
            ChecklistState.Done,
            Assert.Single(ship, item => item.Intent is { Kind: ChecklistIntentKind.Module }).State);
    }
}
