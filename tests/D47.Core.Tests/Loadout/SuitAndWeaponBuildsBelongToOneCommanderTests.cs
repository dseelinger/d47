using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Loadout;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Loadout;

/// <summary>Two Commanders share one on-foot.json and see only their own builds.</summary>
public class SuitAndWeaponBuildsBelongToOneCommanderTests
{
    private static CommanderGameState Commander(string fid, string name)
    {
        var store = new GameStateStore();

        Assert.True(JournalEvent.TryParse(
            $$"""{"timestamp":"2026-08-18T09:00:00Z","event":"Commander","FID":"{{fid}}","Name":"{{name}}"}""",
            NullLogger.Instance,
            out var parsed));

        store.Apply(parsed!);
        return store.Active!;
    }

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private sealed class Harness : IDisposable
    {
        private readonly TempInstall _install = new();

        public Harness()
        {
            Store = new OnFootBuildStore(
                Path.Combine(_install.Root, "on-foot.json"), NullLogger<OnFootBuildStore>.Instance);

            Kit = new OnFootPlanService(
                Store,
                new ChecklistService(
                    new ChecklistStore(
                        Path.Combine(_install.Root, "checklist.json"), NullLogger<ChecklistStore>.Instance),
                    new ChecklistProposalStore(
                        Path.Combine(_install.Root, "checklist-proposals.json"),
                        NullLogger<ChecklistProposalStore>.Instance),
                    () => Active),
                () => Active);
        }

        public OnFootBuildStore Store { get; }

        public OnFootPlanService Kit { get; }

        public CommanderGameState? Active { get; set; }

        public void Dispose() => _install.Dispose();
    }

    [Fact]
    public void EachCommanderSeesOnlyTheirOwnKitPage()
    {
        using var world = new Harness();
        var jameson = Commander("F1", "Jameson");
        var other = Commander("F2", "Other");

        world.Active = jameson;
        world.Kit.Intend("Maverick");

        world.Active = other;
        Assert.Empty(world.Kit.Kit());

        world.Kit.Intend("Dominator");
        Assert.Equal("Dominator Suit", Assert.Single(world.Kit.Kit()).Equipment);

        world.Active = jameson;
        Assert.Equal("Maverick Suit", Assert.Single(world.Kit.Kit()).Equipment);
        Assert.Equal(2, world.Store.Builds.Count);
    }

    [Fact]
    public void ABuildIsStampedWithTheActiveCommanderAndWrittenToTheFile()
    {
        using var world = new Harness();
        world.Active = Commander("F1", "Jameson");

        var build = world.Kit.Intend("Maverick")!;

        Assert.Equal("F1", build.CommanderFid);

        var text = File.ReadAllText(world.Store.Path);

        Assert.Contains("\"commanderFid\": \"F1\"", text, StringComparison.Ordinal);
        Assert.Contains("\"commanderName\": \"Jameson\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void AnotherCommanderCannotChangeOrDropABuildItDoesNotOwn()
    {
        using var world = new Harness();
        world.Active = Commander("F1", "Jameson");
        var build = world.Kit.Intend("Maverick")!;

        world.Active = Commander("F2", "Other");

        Assert.False(world.Kit.Plan(build.Id, new KitPlan(OnFootBuild.GradeSlot, 5)));
        Assert.Equal("There is no such build.", world.Kit.Delete(build.Id));
        Assert.Single(world.Store.Builds);
    }

    [Fact]
    public void ABuyEventAdoptsOnlyTheBuyersPlan()
    {
        using var world = new Harness();
        world.Active = Commander("F1", "Jameson");
        world.Kit.Intend("Maverick");

        world.Active = Commander("F2", "Other");

        var said = world.Kit.Observe([Event("""
            {"timestamp":"2026-08-18T09:00:00Z","event":"BuySuit","Name":"UtilitySuit_Class1","Name_Localised":"Maverick Suit","Price":150000,"SuitID":1837009111675068,"SuitMods":[]}
            """)]);

        Assert.Empty(said);
        Assert.All(world.Store.Builds, build => Assert.Null(build.ItemId));
    }

    [Fact]
    public void AFileFromBeforeCommandersWereRecordedGoesToTheFirstCommanderSeen()
    {
        using var world = new Harness();
        File.WriteAllText(
            world.Store.Path,
            """{"kit":[{"id":"kit-1","equipment":"Maverick Suit","kind":"suit"}]}""");

        world.Store.Poll();

        world.Active = Commander("F1", "Jameson");
        world.Kit.Observe([]);

        world.Active = Commander("F2", "Other");
        world.Kit.Observe([]);

        Assert.Empty(world.Kit.Kit());

        world.Active = Commander("F1", "Jameson");
        Assert.Single(world.Kit.Kit());
    }

    [Fact]
    public void TheGapCountsOnlyTheActiveCommandersPlans()
    {
        using var world = new Harness();
        var jameson = Commander("F1", "Jameson");
        var other = Commander("F2", "Other");

        world.Active = jameson;
        var first = world.Kit.BuildFor(OnFootKind.Weapon, 1845880282772980, "Manticore Executioner");
        world.Kit.Plan(first.Id, new KitPlan(OnFootBuild.ModSlot(1), Modification: "Greater range"));

        world.Active = other;
        var second = world.Kit.BuildFor(OnFootKind.Weapon, 1845880282772981, "Karma AR-50");
        world.Kit.Plan(second.Id, new KitPlan(OnFootBuild.ModSlot(1), Modification: "Greater range"));

        var theirs = PlanGap.Of([], world.Kit.Mine, other);

        world.Active = jameson;
        var mine = PlanGap.Of([], world.Kit.Mine, jameson);
        var everyone = PlanGap.Of([], world.Store.Builds, jameson);

        Assert.Single(mine.Uncovered);
        Assert.Single(theirs.Uncovered);
        Assert.Equal(2, everyone.Uncovered.Count);
    }
}
