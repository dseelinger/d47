using D47.App.Panel;
using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Loadout;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Who can roll a slot's plan, under the Planned block of a slot page (#195).</summary>
public class EngineersListedOnASlotPageTests
{
    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static (ShipsMode Mode, ShipPlanService Ships) Ship(bool progressKnown)
    {
        var paths = new D47.Core.AppPaths(TempFolders.Create("d47-slot-engineers-tests"));

        paths.EnsureCreated();

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(paths.Data, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(paths.Data, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        var store = new GameStateStore();

        var lines = new List<string>
        {
            """{"timestamp":"2026-08-20T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
            """{"timestamp":"2026-08-20T09:00:00Z","event":"Location","StarSystem":"Sol","StarPos":[0.0,0.0,0.0],"Docked":true,"StationName":"Abraham Lincoln"}""",
        };

        // Elvira Martuuk unlocked at grade 5 — the worked example from the issue, only without the
        // Commander actually being in Epsilon Eridani, which the distance assertions do not depend on.
        if (progressKnown)
        {
            lines.Add(
                """{"timestamp":"2026-08-20T09:00:00Z","event":"EngineerProgress","Engineers":[{"Engineer":"Elvira Martuuk","EngineerID":300160,"Progress":"Unlocked","Rank":5}]}""");
        }

        foreach (var line in lines)
        {
            store.Apply(Event(line));
        }

        var live = store.Active!;

        var ships = new ShipPlanService(
            new ShipBuildStore(Path.Combine(paths.Data, "ships.json"), NullLogger<ShipBuildStore>.Instance),
            checklists,
            () => live);

        var build = ships.BuildFor(12, "cobramkv", "Reaper");

        ships.Plan(build.Id, new SlotPlan("FrameShiftDrive", "Increased FSD Range", 5));

        return (new ShipsMode(ships, checklists, () => live), ships);
    }

    private static IReadOnlyList<string> Texts(IReadOnlyList<LoadoutLine> lines) =>
        [.. lines.Select(line => line.Text)];

    [Fact]
    public void UnlockedAndLockedAreDrawnUnderTheirOwnHeadings()
    {
        var (mode, ships) = Ship(progressKnown: true);
        var item = ships.Store.Builds[0].Id;

        var texts = Texts(mode.Engineers(item, "FrameShiftDrive"));

        Assert.Equal("Engineers", texts[0]);
        Assert.Contains("Unlocked", texts);
        Assert.Contains("Locked", texts);
        Assert.Contains(texts, text => text.StartsWith("Elvira Martuuk, grade 5", StringComparison.Ordinal));
        Assert.Contains(texts, text => text.StartsWith("Felicity Farseer", StringComparison.Ordinal));
        Assert.Contains(texts, text => text.StartsWith("Mel Brandon", StringComparison.Ordinal));
    }

    /// <summary>Each row ends with the system, and carries it as the thing the copy glyph copies.</summary>
    [Fact]
    public void EachRowCopiesItsOwnSystem()
    {
        var (mode, ships) = Ship(progressKnown: true);
        var item = ships.Store.Builds[0].Id;

        var lines = mode.Engineers(item, "FrameShiftDrive");
        var elvira = lines.Single(line => line.Text.Contains("Elvira Martuuk", StringComparison.Ordinal));

        Assert.Equal("Khun", elvira.Copy?.Value);
        Assert.EndsWith("Khun", elvira.Text, StringComparison.Ordinal);
    }

    /// <summary>Before EngineerProgress has been read, the list is flat and says so.</summary>
    [Fact]
    public void UnknownProgressListsOnceWithoutGroups()
    {
        var (mode, ships) = Ship(progressKnown: false);
        var item = ships.Store.Builds[0].Id;

        var texts = Texts(mode.Engineers(item, "FrameShiftDrive"));

        Assert.Equal("Engineers", texts[0]);
        Assert.DoesNotContain("Unlocked", texts);
        Assert.DoesNotContain("Locked", texts);
        Assert.Contains(texts, text => text.StartsWith("Elvira Martuuk", StringComparison.Ordinal));

        // Held is never shown without a read progress to back it.
        Assert.DoesNotContain(texts, text => text.Contains("grade", StringComparison.Ordinal));
    }

    [Fact]
    public void ASlotWithNoPlanHasNoEngineersBlock()
    {
        var (mode, ships) = Ship(progressKnown: true);
        var item = ships.Store.Builds[0].Id;

        Assert.Empty(mode.Engineers(item, "PowerPlant"));
    }

    private static (OnFootMode Mode, OnFootPlanService Kit, string Item) Suit()
    {
        var paths = new D47.Core.AppPaths(TempFolders.Create("d47-slot-engineers-onfoot-tests"));

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
                     """{"timestamp":"2026-08-20T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                     """{"timestamp":"2026-08-20T09:00:00Z","event":"Location","StarSystem":"Sol","StarPos":[0.0,0.0,0.0],"Docked":true,"StationName":"Abraham Lincoln"}""",

                     // Read, so the list groups — nobody named here is one of the suit's engineers, which is
                     // the point: both stay Locked.
                     """{"timestamp":"2026-08-20T09:00:00Z","event":"EngineerProgress","Engineers":[{"Engineer":"Liz Ryder","EngineerID":300080,"Progress":"Unlocked","Rank":5}]}""",
                 })
        {
            store.Apply(Event(line));
        }

        var live = store.Active!;

        var kit = new OnFootPlanService(
            new OnFootBuildStore(Path.Combine(paths.Data, "on-foot.json"), NullLogger<OnFootBuildStore>.Instance),
            checklists,
            () => live);

        var build = kit.BuildFor(D47.Core.Knowledge.OnFootKind.Suit, 7, "Maverick Suit");

        kit.Plan(build.Id, new KitPlan("Mod 1", Modification: "Added melee damage"));

        return (new OnFootMode(kit, checklists, () => live), kit, build.Id);
    }

    [Fact]
    public void AnOnFootModificationListsEveryEngineerTheTableNames()
    {
        var (mode, _, item) = Suit();

        var texts = Texts(mode.Engineers(item, "Mod 1"));

        Assert.Equal("Engineers", texts[0]);
        Assert.Contains("Locked", texts);
        Assert.Contains(texts, text => text.StartsWith("Kit Fowler", StringComparison.Ordinal));
        Assert.Contains(texts, text => text.StartsWith("Jude Navarro", StringComparison.Ordinal));

        // No ship-style grade text: on foot there is no grade to hold against.
        Assert.DoesNotContain(texts, text => text.Contains(" of ", StringComparison.Ordinal));
    }

    [Fact]
    public void AnOnFootGradeSlotHasNoEngineerList()
    {
        var (mode, _, item) = Suit();

        Assert.Empty(mode.Engineers(item, OnFootBuild.GradeSlot));
    }
}
