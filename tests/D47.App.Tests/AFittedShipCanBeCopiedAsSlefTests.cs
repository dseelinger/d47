using D47.App.Panel;
using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Ships;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Copy as SLEF press writes the fitted ship to the clipboard, and says so or says why not.</summary>
public class AFittedShipCanBeCopiedAsSlefTests
{
    private const string Loadout = """
        { "timestamp":"2026-08-16T10:00:00Z", "event":"Loadout", "Ship":"krait_mkii", "ShipID":12,
          "Modules":[ { "Slot":"MainEngines", "Item":"int_engine_size5_class5", "On":true, "Priority":0 } ] }
        """;

    private static (ShipsMode Mode, ShipPlanService Ships, List<string> Copied) Bench(bool seen)
    {
        var paths = new D47.Core.AppPaths(TestSurface.MemoryFolder("d47-copy-slef-tests"));

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(paths.Data, "checklist.json"), new MemoryFileSystem(), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(paths.Data, "checklist-proposals.json"),
                new MemoryFileSystem(),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{ "timestamp":"2026-08-16T08:00:00Z", "event":"Commander", "FID":"F1", "Name":"Jameson" }""",
                     Loadout,
                 }.Take(seen ? 2 : 1))
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        var ships = new ShipPlanService(
            new ShipBuildStore(Path.Combine(paths.Data, "ships.json"), new MemoryFileSystem(), NullLogger<ShipBuildStore>.Instance),
            checklists,
            () => store.Active);

        var copied = new List<string>();

        var mode = new ShipsMode(
            ships,
            checklists,
            () => store.Active,
            setClipboardText: text =>
            {
                copied.Add(text);
                return Task.FromResult(true);
            });

        return (mode, ships, copied);
    }

    [Fact]
    public async Task AnOwnedShipIsCopiedAndTheLineSaysSo()
    {
        var (mode, ships, copied) = Bench(seen: true);
        var build = ships.BuildFor(12, "krait_mkii");

        Assert.Equal("Copy as SLEF", mode.ExportLabel(build.Id));
        Assert.Equal("Copied the ship as SLEF.", await mode.Export(build.Id));
        Assert.Contains("int_engine_size5_class5", Assert.Single(copied));
    }

    [Fact]
    public async Task AShipWhoseModulesHaveNotBeenSeenLeavesTheClipboardAlone()
    {
        var (mode, ships, copied) = Bench(seen: false);
        var build = ships.BuildFor(12, "krait_mkii");

        Assert.StartsWith("I have not seen this ship's modules yet", await mode.Export(build.Id));
        Assert.Empty(copied);
    }

    [Fact]
    public void AnIntendedHullHasNoPress()
    {
        var (mode, ships, _) = Bench(seen: true);

        Assert.Null(mode.ExportLabel(ships.Intend("Python")!.Id));
    }
}
