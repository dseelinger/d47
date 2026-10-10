using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Loadout;
using D47.Core.Storage;
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

    private sealed class Harness
    {
        private readonly MemoryInstall _install = new();

        public IFileSystem Files => _install.Files;

        public Harness()
        {
            Store = new OnFootBuildStore(
                Path.Combine(_install.Root, "on-foot.json"), _install.Files, NullLogger<OnFootBuildStore>.Instance);

            Kit = new OnFootPlanService(
                Store,
                new ChecklistService(
                    new ChecklistStore(
                        Path.Combine(_install.Root, "checklist.json"), _install.Files, NullLogger<ChecklistStore>.Instance),
                    new ChecklistProposalStore(
                        Path.Combine(_install.Root, "checklist-proposals.json"),
                        _install.Files,
                        NullLogger<ChecklistProposalStore>.Instance),
                    () => Active),
                () => Active,
                () => Ledgers);
        }

        public Dictionary<string, OwnedKit> Ledgers { get; } = [];

        public OnFootBuildStore Store { get; }

        public OnFootPlanService Kit { get; }

        public CommanderGameState? Active { get; set; }
    }

    [Fact]
    public void EachCommanderSeesOnlyTheirOwnKitPage()
    {
        var world = new Harness();
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
        var world = new Harness();
        world.Active = Commander("F1", "Jameson");

        var build = world.Kit.Intend("Maverick")!;

        Assert.Equal("F1", build.CommanderFid);

        var text = world.Files.ReadText(world.Store.Path);

        Assert.Contains("\"commanderFid\": \"F1\"", text, StringComparison.Ordinal);
        Assert.Contains("\"commanderName\": \"Jameson\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void AnotherCommanderCannotChangeOrDropABuildItDoesNotOwn()
    {
        var world = new Harness();
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
        var world = new Harness();
        world.Active = Commander("F1", "Jameson");
        world.Kit.Intend("Maverick");

        world.Active = Commander("F2", "Other");

        var said = world.Kit.Observe([Event("""
            {"timestamp":"2026-08-18T09:00:00Z","event":"BuySuit","Name":"UtilitySuit_Class1","Name_Localised":"Maverick Suit","Price":150000,"SuitID":1837009111675068,"SuitMods":[]}
            """)]);

        Assert.Empty(said);
        Assert.All(world.Store.Builds, build => Assert.Null(build.ItemId));
    }

    private static OwnedKit Owning(long suitId, long weaponId) => new()
    {
        Suits = new Dictionary<long, OwnedSuit> { [suitId] = new OwnedSuit(suitId, "tacticalsuit_class1", 1, [], default) },
        Weapons = new Dictionary<long, OwnedWeapon> { [weaponId] = new OwnedWeapon(weaponId, "wpn_m_assaultrifle_laser_fauto", 1, [], default) },
    };

    private static void Write(Harness world, string json)
    {
        world.Files.WriteText(world.Store.Path, json);
        world.Store.Poll();
    }

    [Fact]
    public void OldBuildsGoToTheCommanderWhoseLedgerHoldsTheItemEvenWhenAnotherIsSeenFirst()
    {
        var world = new Harness();
        world.Ledgers["F1"] = Owning(11, 21);
        world.Ledgers["F2"] = new OwnedKit();

        Write(world, """
            {"kit":[
              {"id":"a","equipment":"Maverick Suit","kind":"suit","itemId":11},
              {"id":"b","equipment":"Karma AR-50","kind":"weapon","itemId":21}]}
            """);

        world.Active = Commander("F2", "Other");
        world.Kit.Observe([]);

        Assert.Empty(world.Kit.Mine);
        Assert.All(world.Store.Builds, build => Assert.Equal("F1", build.CommanderFid));
    }

    [Fact]
    public void AnIntendedBuildAndASoldItemGoWithTheCommanderWhoOwnsTheRest()
    {
        var world = new Harness();
        world.Ledgers["F1"] = Owning(11, 21);

        Write(world, """
            {"kit":[
              {"id":"a","equipment":"Maverick Suit","kind":"suit","itemId":11},
              {"id":"b","equipment":"Karma AR-50","kind":"weapon","itemId":21},
              {"id":"c","equipment":"Dominator Suit","kind":"suit"},
              {"id":"d","equipment":"Karma P-15","kind":"weapon","itemId":99}]}
            """);

        world.Active = Commander("F2", "Other");
        world.Kit.Observe([]);

        Assert.Empty(world.Kit.Mine);
        Assert.Equal(4, world.Store.BuildsFor("F1").Count);
    }

    [Fact]
    public void AFileMatchingNoLedgerGoesToTheFirstCommanderSeen()
    {
        var world = new Harness();
        world.Ledgers["F1"] = Owning(11, 21);

        Write(world, """
            {"kit":[
              {"id":"a","equipment":"Maverick Suit","kind":"suit","itemId":98},
              {"id":"b","equipment":"Dominator Suit","kind":"suit"}]}
            """);

        world.Active = Commander("F2", "Other");
        world.Kit.Observe([]);

        Assert.Equal(2, world.Kit.Mine.Count);
    }

    [Fact]
    public void TwoCommandersTyingForTheMostBuildsLeaveTheRestWithTheFirstSeen()
    {
        var world = new Harness();
        world.Ledgers["F1"] = Owning(11, 21);
        world.Ledgers["F2"] = Owning(12, 22);
        world.Ledgers["F3"] = new OwnedKit();

        Write(world, """
            {"kit":[
              {"id":"a","equipment":"Maverick Suit","kind":"suit","itemId":11},
              {"id":"b","equipment":"Maverick Suit","kind":"suit","itemId":12},
              {"id":"c","equipment":"Dominator Suit","kind":"suit"}]}
            """);

        world.Active = Commander("F3", "Third");
        world.Kit.Observe([]);

        Assert.Equal("c", Assert.Single(world.Kit.Mine).Id);
        Assert.Equal("a", Assert.Single(world.Store.BuildsFor("F1")).Id);
        Assert.Equal("b", Assert.Single(world.Store.BuildsFor("F2")).Id);
    }

    [Fact]
    public void AMatchedOwnerIsNamedFromTheirActiveIdentityOnlyWhenKnown()
    {
        var world = new Harness();
        world.Ledgers["F1"] = Owning(11, 21);

        Write(world, """
            {"kit":[
              {"id":"a","equipment":"Maverick Suit","kind":"suit","itemId":11},
              {"id":"z","commanderFid":"F1","commanderName":"Jameson","equipment":"Dominator Suit","kind":"suit"}]}
            """);

        world.Active = Commander("F2", "Other");
        world.Kit.Observe([]);

        Assert.All(world.Store.Builds, build => Assert.Equal("Jameson", build.CommanderName));
    }

    [Fact]
    public void ABuildAlreadyCarryingACommanderIsNotChanged()
    {
        var world = new Harness();
        world.Ledgers["F1"] = Owning(11, 21);

        Write(world, """
            {"kit":[
              {"id":"a","commanderFid":"F2","equipment":"Maverick Suit","kind":"suit","itemId":11},
              {"id":"b","equipment":"Dominator Suit","kind":"suit"}]}
            """);

        world.Active = Commander("F3", "Third");
        world.Kit.Observe([]);

        Assert.Equal("F2", world.Store.Find("a")!.CommanderFid);
    }

    [Fact]
    public void TheGapCountsOnlyTheActiveCommandersPlans()
    {
        var world = new Harness();
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
