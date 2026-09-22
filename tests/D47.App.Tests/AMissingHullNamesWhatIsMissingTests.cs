using D47.App.Panel;
using D47.Core.Checklists;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A hull the table has no figures for still names itself and says which figures are missing,
/// rather than reading as though d47 knew nothing about the ship (#387).
/// </summary>
public class AMissingHullNamesWhatIsMissingTests
{
    [Fact]
    public void TheLoadoutPageNamesTheHullAndItsMissingFigures()
    {
        var paths = new D47.Core.AppPaths(TempFolders.Create("d47-missing-hull-tests"));

        paths.EnsureCreated();

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(paths.Data, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(paths.Data, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        var ships = new ShipPlanService(
            new ShipBuildStore(Path.Combine(paths.Data, "ships.json"), NullLogger<ShipBuildStore>.Instance),
            checklists,
            () => null);

        // A hull symbol the shipped table carries no figures for — nothing Frontier has shipped
        // answers to it.
        ships.BuildFor(14, "type12_prototype", "Scout");

        var mode = new ShipsMode(ships, checklists, () => null);
        var item = mode.Items().Single().Key;

        var lines = mode.Details(item).Select(entry => entry.Text).ToArray();

        Assert.Contains(
            "I have no hull figures for the type12_prototype: its speed, boost, armour, shields and "
            + "cost are not in my table.",
            lines);
        Assert.DoesNotContain(lines, text => text.Contains("no figures for this ship", StringComparison.Ordinal));
    }
}
