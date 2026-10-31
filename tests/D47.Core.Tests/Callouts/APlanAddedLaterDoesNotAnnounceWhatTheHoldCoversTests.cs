using D47.Core.Callouts;
using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Ships;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

public class APlanAddedLaterDoesNotAnnounceWhatTheHoldCoversTests
{
    private const int ShipId = 21;

    private static readonly DateTimeOffset Now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

    private sealed class Bench
    {
        public GameStateStore Game { get; } = new();

        public ChecklistService Checklists { get; }

        public ShipPlanService Ships { get; }

        public ChecklistCallout Callout { get; }

        public Bench()
        {
            var install = new MemoryInstall();

            Game.Apply(Event("""{"timestamp":"2026-10-31T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}"""));
            Game.Apply(Event(
                $$"""
                {"timestamp":"2026-10-31T09:05:00Z","event":"Loadout","Ship":"python","ShipID":{{ShipId}},
                 "Modules":[{"Slot":"MainEngines","Item":"int_engine_size5_class5","On":true}]}
                """));

            Checklists = new ChecklistService(
                new ChecklistStore(Path.Combine(install.Root, "checklist.json"), new MemoryFileSystem(), NullLogger<ChecklistStore>.Instance),
                new ChecklistProposalStore(
                    Path.Combine(install.Root, "checklist-proposals.json"),
                    new MemoryFileSystem(),
                    NullLogger<ChecklistProposalStore>.Instance),
                () => Game.Active);

            Ships = new ShipPlanService(
                new ShipBuildStore(Path.Combine(install.Root, "ships.json"), new MemoryFileSystem(), NullLogger<ShipBuildStore>.Instance),
                Checklists,
                () => Game.Active);

            Callout = new ChecklistCallout(Checklists);
        }

        public void Plan(int grade, int shipId = ShipId)
        {
            var build = Ships.BuildFor(shipId, "python");

            Ships.Plan(build.Id, new SlotPlan("MainEngines", "Dirty Drive Tuning", grade));
            Ships.Promote(build.Id);
        }

        public PlanCosting Costing() => EngineeringPlan.Cost(Checklists.Document.Items, Game.Active!);

        /// <summary>Puts every plan material in the hold at its need, except those named.</summary>
        public void HoldEverythingBut(params string[] symbols)
        {
            var held = Costing().Ingredients
                .Where(ingredient => !symbols.Contains(ingredient.Material.Symbol))
                .Select(ingredient => $$"""{"Name":"{{ingredient.Material.Symbol}}","Count":{{ingredient.Needed}}}""")
                .ToList();
            var list = string.Join(",", held);

            Game.Apply(Event(
                $$"""{"timestamp":"2026-10-31T10:00:00Z","event":"Materials","Raw":[{{list}}],"Manufactured":[{{list}}],"Encoded":[{{list}}]}"""));
        }

        public void Collect(string symbol, int count = 1) =>
            Game.Apply(Event(
                $$"""{"timestamp":"2026-10-31T10:30:00Z","event":"MaterialCollected","Category":"Raw","Name":"{{symbol}}","Count":{{count}}}"""));

        public List<Announcement> Examine(bool priming, params string[] kinds) =>
            [.. Callout.Examine(new CalloutContext(
                Now,
                priming,
                Game.Active!,
                GameStatus.Unknown,
                NavRoute.None,
                [.. kinds.Select(kind => Event($$"""{"timestamp":"2026-10-31T10:30:00Z","event":"{{kind}}"}"""))]))];
    }

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static List<Announcement> Filled(IEnumerable<Announcement> announcements) =>
        [.. announcements.Where(line => line.Key.StartsWith("checklist.filled.", StringComparison.Ordinal))];

    [Fact]
    public void APlanLineAddedMidSessionWithItsMaterialsAlreadyHeldSaysNothing()
    {
        var bench = new Bench();

        bench.Examine(priming: true);
        bench.Plan(5);
        bench.HoldEverythingBut();

        Assert.Empty(Filled(bench.Examine(priming: false, "MaterialCollected")));
    }

    [Fact]
    public void ARaisedNeedIsAnnouncedWhenAPickupCoversItAgain()
    {
        var bench = new Bench();

        bench.Examine(priming: true);
        bench.Plan(1, ShipId);
        bench.HoldEverythingBut();
        Assert.Empty(Filled(bench.Examine(priming: false, "MaterialCollected")));

        bench.Plan(1, ShipId + 1);
        Assert.Empty(Filled(bench.Examine(priming: false, "MaterialCollected")));

        bench.HoldEverythingBut();
        Assert.NotEmpty(Filled(bench.Examine(priming: false, "MaterialCollected")));
        Assert.Empty(Filled(bench.Examine(priming: false, "MaterialCollected")));
    }

    [Fact]
    public void OnlyTheMaterialAPickupCoversIsAnnounced()
    {
        var bench = new Bench();

        bench.Examine(priming: true);
        bench.Plan(5);
        bench.HoldEverythingBut();

        var target = bench.Costing().Ingredients.First().Material;

        Assert.Empty(Filled(bench.Examine(priming: false, "MaterialCollected")));

        bench.HoldEverythingBut(target.Symbol);
        Assert.Empty(Filled(bench.Examine(priming: false, "MaterialCollected")));

        bench.Collect(target.Symbol, bench.Costing().Ingredients.First(i => i.Material.Symbol == target.Symbol).Needed);

        var said = Filled(bench.Examine(priming: false, "MaterialCollected"));

        Assert.Equal("checklist.filled." + target.Symbol, Assert.Single(said).Key);
    }
}
