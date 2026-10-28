using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Ships;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Ships;

/// <summary>A build for a hull the Commander does not own reaches the checklist as a list of its own (#1830).</summary>
public class APlannedShipHasItsOwnListTests
{
    private const string FiveA = "int_engine_size5_class5";

    private static (ShipPlanService Ships, ChecklistService Checklists) Services(Func<CommanderGameState?>? state = null)
    {
        var install = new MemoryInstall();
        state ??= () => null;

        var checklists = new ChecklistService(
            new ChecklistStore(
                Path.Combine(install.Root, "checklist.json"),
                new MemoryFileSystem(),
                NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(install.Root, "checklist-proposals.json"),
                new MemoryFileSystem(),
                NullLogger<ChecklistProposalStore>.Instance),
            state);

        var store = new ShipBuildStore(
            Path.Combine(install.Root, "ships.json"), install.Files, NullLogger<ShipBuildStore>.Instance);

        return (new ShipPlanService(store, checklists, state), checklists);
    }

    /// <summary>A planned Python with a module-only slot, a module with engineering, and engineering alone.</summary>
    private static ShipBuild Planned(ShipPlanService ships, string? name = null)
    {
        var build = ships.Intend("python", name)!;

        ships.Plan(build.Id, new SlotPlan("Hardpoint1", Module: "Pulse Laser"));
        ships.Plan(build.Id, new SlotPlan("MainEngines", Module: "Thrusters", Blueprint: "Dirty Drive Tuning", Grade: 5) { Variant = FiveA });
        ships.Plan(build.Id, new SlotPlan("PowerPlant", Blueprint: "Overcharged", Grade: 3));

        return build;
    }

    private static IReadOnlyList<ChecklistItem> Listed(ChecklistService checklists, ShipBuild build) =>
        [.. checklists.Document.Items.Where(item => item.Scope.Same(ChecklistScope.Build(build.Id)))];

    private static CommanderGameState State(params string[] lines)
    {
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{ "timestamp":"2026-08-16T08:00:00Z", "event":"Commander", "FID":"F1", "Name":"Jameson" }""",
                 }.Concat(lines))
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store.Active!;
    }

    private const string StoredThrusters =
        """
        { "timestamp":"2026-08-16T10:30:00Z", "event":"StoredModules", "StarSystem":"Deciat",
          "MarketID":1, "StationName":"Garay Terminal",
          "Items":[ { "Name":"$int_engine_size5_class5_name;", "Name_Localised":"Thrusters",
                      "StarSystem":"Deciat", "TransferCost":21000 } ] }
        """;

    private const string FlyingAKrait =
        """
        { "timestamp":"2026-08-16T10:00:00Z", "event":"Loadout", "Ship":"krait_mkii", "ShipID":12,
          "Modules":[ { "Slot":"MainEngines", "Item":"int_engine_size5_class1", "On":true } ] }
        """;

    private static ChecklistItem OnPlannedShip(ChecklistIntent intent) => new()
    {
        Key = ChecklistKeys.For(intent),
        Scope = ChecklistScope.Build("ship-3"),
        Kind = ChecklistItemKind.Derived,
        Source = ChecklistSource.EngineeringPlan,
        Text = "a planned line",
        Intent = intent,
        Hull = "python",
    };

    [Fact]
    public void AnUnownedBuildIsScopedToItsBuildAndAnOwnedOneToItsShip()
    {
        var (ships, _) = Services();

        var intended = ships.Intend("python")!;
        var owned = ships.BuildFor(12, "krait_mkii");

        Assert.Equal(ChecklistScope.Build(intended.Id), intended.Scope);
        Assert.Equal(ChecklistScope.Ship(12), owned.Scope);
    }

    [Fact]
    public void ABuildListIsWrittenAndReadBack()
    {
        var files = new MemoryFileSystem();
        var path = Path.Combine(@"C:\d47\data", "checklist.json");
        var intent = new ChecklistIntent(ChecklistIntentKind.Hull, "python");
        var line = OnPlannedShip(intent) with { Text = "Buy a Python", BuildName = "Bad Idea" };

        Assert.Null(ChecklistValidation.Problem(line));

        new ChecklistStore(path, files, NullLogger<ChecklistStore>.Instance)
            .Save([ChecklistDocument.For("F1", "Jameson") with { Items = [line] }]);

        var reread = new ChecklistStore(path, files, NullLogger<ChecklistStore>.Instance);

        Assert.True(reread.Poll());
        Assert.Empty(reread.Problems);

        var item = Assert.Single(reread.For("F1").Items);

        Assert.Equal(ChecklistScope.Build("ship-3"), item.Scope);
        Assert.Equal(ChecklistIntentKind.Hull, item.Intent!.Kind);
        Assert.Equal("python", item.Intent.Subject);
        Assert.Equal("Bad Idea", item.BuildName);
    }

    [Fact]
    public void AHullLineHasItsOwnKeyWordAndNothingInTheJournalMovesIt()
    {
        var intent = new ChecklistIntent(ChecklistIntentKind.Hull, "python");
        var state = State(FlyingAKrait, StoredThrusters);

        Assert.Equal("hull/python", ChecklistKeys.For(intent));
        Assert.Null(ChecklistEvaluator.Evaluate(OnPlannedShip(intent), state));
        Assert.Null(ChecklistEvaluator.Evaluate(OnPlannedShip(intent) with { Scope = ChecklistScope.Ship(12) }, state));
    }

    [Fact]
    public void PromotingAnUnownedBuildWritesTheHullModuleAndEngineeringLinesAndSaysHowMany()
    {
        var (ships, checklists) = Services();
        var build = Planned(ships);

        var said = ships.Promote(build.Id);
        var listed = Listed(checklists, build);

        Assert.Contains("5 items are on your checklist", said, StringComparison.Ordinal);
        Assert.Equal(5, listed.Count);

        var hull = listed[0];

        Assert.Equal("Buy a Python", hull.Text);
        Assert.Equal(ChecklistIntentKind.Hull, hull.Intent!.Kind);
        Assert.Equal("python", hull.Intent.Subject);
        Assert.Equal(2, listed.Count(item => item.Intent!.Kind == ChecklistIntentKind.Module));
        Assert.Equal(2, listed.Count(item => item.Intent!.Kind == ChecklistIntentKind.Blueprint));
        Assert.All(listed, item => Assert.Equal("python", item.Hull));
    }

    [Fact]
    public void PromotingTwiceKeepsOneHullLine()
    {
        var (ships, checklists) = Services();
        var build = Planned(ships);

        ships.Promote(build.Id);
        ships.Promote(build.Id);

        Assert.Single(Listed(checklists, build), item => item.Intent!.Kind == ChecklistIntentKind.Hull);
    }

    [Fact]
    public void AModuleLineOnAPlannedShipIsDoneWhenAMatchingModuleIsInStorage()
    {
        var intent = new ChecklistIntent(ChecklistIntentKind.Module, "MainEngines") { Module = "Thrusters", Variant = FiveA };

        var verdict = ChecklistEvaluator.Evaluate(OnPlannedShip(intent), State(FlyingAKrait, StoredThrusters));

        Assert.Equal(ChecklistState.Done, verdict!.Value.State);
        Assert.Equal("5A Thrusters is bought and stored at Garay Terminal in Deciat.", verdict.Value.Reason);
    }

    [Fact]
    public void AModuleLineOnAPlannedShipIsOpenUntilBoughtAndSaysItCanBeStoredFirst()
    {
        var intent = new ChecklistIntent(ChecklistIntentKind.Module, "MainEngines") { Module = "Thrusters", Variant = FiveA };

        // The ship being flown has thrusters in that slot, and they are not the planned ship's.
        var verdict = ChecklistEvaluator.Evaluate(OnPlannedShip(intent), State(FlyingAKrait));

        Assert.Equal(ChecklistState.Open, verdict!.Value.State);
        Assert.Equal(
            "5A Thrusters is not bought yet. You can buy it into storage before you buy the ship.",
            verdict.Value.Reason);
    }

    [Fact]
    public void AnEngineeringLineOnAPlannedShipGetsNoVerdictAndNoLoadout()
    {
        var intent = new ChecklistIntent(ChecklistIntentKind.Blueprint, "MainEngines") { Detail = "Dirty Drive Tuning", Grade = 5 };
        var line = OnPlannedShip(intent);
        var state = State(FlyingAKrait);

        Assert.Null(ChecklistEvaluator.Evaluate(line, state));
        Assert.Equal(ChecklistState.Open, line.State);

        // The Krait being flown has thrusters in MainEngines; the planned Python's line must not read them.
        Assert.Null(FittedModule.Of(line, state));
        Assert.NotNull(FittedModule.Of(line with { Scope = ChecklistScope.Ship(12) }, state));
    }

    [Fact]
    public void ThePlannedListIsNamedForTheHull()
    {
        var (ships, checklists) = Services();
        var build = Planned(ships);

        ships.Promote(build.Id);

        var list = Assert.Single(ChecklistLists.Build(ChecklistOrdering.Arrange(checklists.Document, null), null));

        Assert.Equal("Python (planned)", list.Name);
        Assert.Equal(ChecklistListGroup.Ships, list.Home);
        Assert.All(Listed(checklists, build), item => Assert.Equal("Python (planned)", ChecklistWording.Where(item, null)));
        Assert.Equal("Python (planned)", ChecklistWording.Where(ChecklistScope.Build(build.Id), "python", null));
        Assert.Equal("planned ship", ChecklistScope.Word(ChecklistGroup.Build));
    }

    [Fact]
    public void ANamedBuildsListIsNamedForTheBuild()
    {
        var (ships, checklists) = Services();
        var build = Planned(ships, "Bad Idea");

        ships.Promote(build.Id);

        var list = Assert.Single(ChecklistLists.Build(ChecklistOrdering.Arrange(checklists.Document, null), null));

        Assert.Equal("Bad Idea (planned)", list.Name);
        Assert.All(Listed(checklists, build), item => Assert.Equal("Bad Idea (planned)", ChecklistWording.Where(item, null)));
    }

    [Fact]
    public void ANoteOnAPlannedShipsListKeepsTheListsNameWhenMovedToTheTop()
    {
        var (ships, checklists) = Services();
        var build = Planned(ships);

        ships.Promote(build.Id);

        var added = checklists.AddNote(build.Scope, "Ask about a discount");
        var note = added.Document.Find(added.Subject!.Value)!;

        Assert.Contains("to the Python (planned) list", added.Report, StringComparison.Ordinal);

        for (var step = 0; step < 10; step++)
        {
            checklists.Move(note.Id, -1);
        }

        var listed = Listed(checklists, build);
        var list = Assert.Single(ChecklistLists.Build(ChecklistOrdering.Arrange(checklists.Document, null), null));

        Assert.Equal(note.Id, listed[0].Id);
        Assert.Equal("Python (planned)", ChecklistWording.Where(listed[0], null));
        Assert.Equal("Python (planned)", list.Name);
    }

    [Fact]
    public void ASoldShipLeavesPlannedShipListsAlone()
    {
        var (ships, checklists) = Services();
        var build = Planned(ships);

        ships.Promote(build.Id);
        var before = Listed(checklists, build).Count;

        Assert.True(JournalEvent.TryParse(
            """{ "timestamp":"2026-08-16T11:00:00Z", "event":"ShipyardSell", "ShipType":"python", "SellShipID":3 }""",
            NullLogger.Instance,
            out var sold));

        ships.DropGone([sold!]);
        checklists.ForgetShip(3, _ => true);

        Assert.Equal(before, Listed(checklists, build).Count);
    }

    [Fact]
    public void DeletingAnUnownedBuildRemovesItsList()
    {
        var (ships, checklists) = Services();
        var build = Planned(ships);
        var other = Planned(ships);

        ships.Promote(build.Id);
        ships.Promote(other.Id);
        checklists.AddNote(build.Scope, "Ask about a discount");

        var said = ships.Delete(build.Id);

        Assert.Equal("Dropped the plan for a Python, and its list on the Checklist tab.", said);
        Assert.Empty(Listed(checklists, build));
        Assert.Equal(5, Listed(checklists, other).Count);
    }
}
