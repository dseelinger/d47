using D47.App.Panel;
using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>A ship the Commander is not flying can still be planned.</summary>
public class AParkedShipHasSlotsToPlanTests
{
    /// <summary>
    /// A fleet with one ship in it, parked somewhere else, and nothing planned for it — which is the
    /// state every ship in a fleet is in before it is first planned.
    /// </summary>
    private static ShipsMode Parked()
    {
        var paths = new D47.Core.AppPaths(TempFolders.Create("d47-parked-ship-tests"));

        paths.EnsureCreated();

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(paths.Data, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(paths.Data, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{"timestamp":"2026-08-19T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",

                     // ShipType and ShipType_Localised both, exactly as Elite writes them.
                     """{"timestamp":"2026-08-19T09:00:00Z","event":"StoredShips","StarSystem":"Laksak","StationName":"BNH-T2F","ShipsHere":[],"ShipsRemote":[{"ShipID":12,"ShipType":"cobramkv","ShipType_Localised":"Cobra MkV","Name":"Reaper","Value":15450304,"StarSystem":"Laksak"}]}""",
                 })
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        var live = store.Active!;

        var ships = new ShipPlanService(
            new ShipBuildStore(Path.Combine(paths.Data, "ships.json"), NullLogger<ShipBuildStore>.Instance),
            checklists,
            () => live);

        return new ShipsMode(ships, checklists, () => live);
    }

    [Fact]
    public void AShipNobodyIsFlyingOffersItsSlots()
    {
        var mode = Parked();

        var reaper = Assert.Single(mode.Items());

        Assert.NotEmpty(mode.Slots(reaper.Key));
    }

    /// <summary>And the whole hull, not the handful of slots something happened to mention.</summary>
    [Fact]
    public void EverySlotOfTheHullIsThere()
    {
        var mode = Parked();

        var reaper = Assert.Single(mode.Items());

        Assert.Equal(EliteSpecifications.Slots("cobramkv").Count, mode.Slots(reaper.Key).Count);
    }

    /// <summary>
    /// The page said the right things about the ship the whole time, which is what made the missing
    /// half read as a feature that had been taken away rather than as a lookup that failed.
    /// </summary>
    [Fact]
    public void TheDetailsWereNeverTheProblemAndStillAreNot()
    {
        var mode = Parked();

        var reaper = Assert.Single(mode.Items());

        // Through Title: an owned ship's summary line is empty, and the name is the page's heading.
        Assert.NotNull(mode.Title(reaper.Key));
        Assert.NotEmpty(mode.Details(reaper.Key));
    }

    /// <summary>
    /// The lookup itself, at the level the defect actually lived at: two spellings of one hull, one
    /// answer.
    /// </summary>
    [Fact]
    public void TheLayoutAnswersToTheNameAsWellAsTheSymbol()
    {
        Assert.NotEmpty(EliteSpecifications.Slots("cobramkv"));
        Assert.Equal(
            EliteSpecifications.Slots("cobramkv").Count,
            EliteSpecifications.Slots("Cobra MkV").Count);
    }

    /// <summary>And a hull that is not a hull is still empty rather than something.</summary>
    [Fact]
    public void AHullNobodyKnowsStillHasNoLayout()
    {
        Assert.Empty(EliteSpecifications.Slots("not a ship at all"));
        Assert.Empty(EliteSpecifications.Slots(null));
        Assert.Empty(EliteSpecifications.Slots("  "));
    }
}
