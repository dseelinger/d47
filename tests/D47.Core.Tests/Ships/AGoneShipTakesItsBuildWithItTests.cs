using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Ships;

/// <summary>
/// A build whose ship is gone is deleted with the checklist lines it put there, and nothing is said.
/// </summary>
public class AGoneShipTakesItsBuildWithItTests
{
    private const int Gone = 12;

    private const int Kept = 14;

    private sealed record Bench(GameStateStore Game, ChecklistService Checklists, ShipPlanService Ships);

    private static Bench Set(TempInstall install)
    {
        var game = new GameStateStore();

        game.Apply(Event("""{"timestamp":"2026-08-18T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}"""));
        game.Apply(Loadout(Gone, "python"));

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(install.Root, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(install.Root, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => game.Active);

        var ships = new ShipPlanService(
            new ShipBuildStore(Path.Combine(install.Root, "ships.json"), NullLogger<ShipBuildStore>.Instance),
            checklists,
            () => game.Active);

        foreach (var (id, hull) in new[] { (Gone, "python"), (Kept, "anaconda") })
        {
            var build = ships.BuildFor(id, hull);

            ships.Plan(build.Id, new SlotPlan("MainEngines", "Dirty Drive Tuning", 5, "Felicity Farseer"));
            ships.Promote(build.Id);
        }

        checklists.AddNote(ChecklistScope.Ship(Gone), "Buy a paint job");

        Assert.NotEmpty(Derived(checklists.Document, Gone));
        Assert.NotEmpty(Derived(checklists.Document, Kept));

        return new Bench(game, checklists, ships);
    }

    [Fact]
    public void ASaleDeletesTheBuildAndItsLinesAndSaysNothing()
    {
        using var install = new TempInstall();
        var (_, checklists, ships) = Set(install);

        ships.DropGone([Sell(Gone)]);

        Assert.Null(ships.ForShip(Gone));
        Assert.Empty(Derived(checklists.Document, Gone));
        Assert.Empty(checklists.Drain());

        // The other ship's build and lines, and the Commander's own note, are untouched.
        Assert.NotNull(ships.ForShip(Kept));
        Assert.NotEmpty(Derived(checklists.Document, Kept));
        Assert.Single(checklists.Document.In(ChecklistScope.Ship(Gone)));
    }

    [Fact]
    public void AnIdReportingAnotherHullDeletesTheBuildOnTheNextPoll()
    {
        using var install = new TempInstall();
        var (game, checklists, ships) = Set(install);

        game.Apply(Loadout(Gone, "cutter"));
        ships.DropGone([]);

        Assert.Null(ships.ForShip(Gone));
        Assert.Empty(Derived(checklists.Document, Gone));
        Assert.Single(checklists.Document.In(ChecklistScope.Ship(Gone)));
        Assert.NotNull(ships.ForShip(Kept));
    }

    /// <summary>A ship d47 has never seen a loadout for is not gone, and neither is one reporting its own hull.</summary>
    [Fact]
    public void AShipStillReportingItsHullKeepsItsBuild()
    {
        using var install = new TempInstall();
        var (game, checklists, ships) = Set(install);

        game.Apply(Loadout(Gone, "Python"));
        ships.DropGone([]);

        Assert.NotNull(ships.ForShip(Gone));
        Assert.NotEmpty(Derived(checklists.Document, Gone));
        Assert.NotNull(ships.ForShip(Kept));
    }

    /// <summary>Revision revives a tombstone whose key comes back, so none may be left in the file.</summary>
    [Fact]
    public void NothingDerivedFromTheBuildIsLeftInTheFile()
    {
        using var install = new TempInstall();
        var (_, checklists, ships) = Set(install);

        // A revision that drops a line leaves a tombstone behind.
        checklists.Revise(
            ChecklistScope.Ship(Gone),
            ChecklistSource.EngineeringPlan,
            [.. Derived(checklists.Document, Gone).Take(1)]);

        Assert.Contains(checklists.Document.Items, item => !item.IsLive && item.Scope.Same(ChecklistScope.Ship(Gone)));

        ships.DropGone([Sell(Gone)]);

        var reopened = new ChecklistStore(Path.Combine(install.Root, "checklist.json"), NullLogger<ChecklistStore>.Instance);

        reopened.Poll();

        Assert.Empty(Derived(reopened.For("F1"), Gone));
        Assert.NotEmpty(Derived(reopened.For("F1"), Kept));
    }

    [Fact]
    public void AWaitingPlanForTheSoldShipIsWithdrawn()
    {
        using var install = new TempInstall();
        var (_, checklists, ships) = Set(install);

        ships.Plan(ships.ForShip(Gone)!.Id, new SlotPlan("PowerPlant", "Overcharged", 5));

        Assert.NotNull(new ShipDriftWatch(ships, checklists).Observe([Loadout(Gone, "python")]));
        Assert.NotEmpty(checklists.Proposals.PendingFor("F1"));

        ships.DropGone([Sell(Gone)]);

        Assert.Empty(checklists.Proposals.PendingFor("F1"));
    }

    private static IReadOnlyList<ChecklistItem> Derived(ChecklistDocument document, int ship) =>
        [.. document.Items.Where(item =>
            item.Kind == ChecklistItemKind.Derived && item.Scope.Same(ChecklistScope.Ship(ship)))];

    private static JournalEvent Loadout(int ship, string hull) => Event(
        $$"""{"timestamp":"2026-08-18T09:05:00Z","event":"Loadout","Ship":"{{hull}}","ShipID":{{ship}},"Modules":[{"Slot":"MainEngines","Item":"int_engine_size5_class5","On":true}]}""");

    private static JournalEvent Sell(int ship) => Event(
        $$"""{"timestamp":"2026-08-18T09:10:00Z","event":"ShipyardSell","ShipType":"python","SellShipID":{{ship}},"ShipPrice":1000,"MarketID":1}""");

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }
}
