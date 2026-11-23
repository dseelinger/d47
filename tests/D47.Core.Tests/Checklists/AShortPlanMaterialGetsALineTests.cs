using System.Globalization;
using D47.Core.Callouts;
using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Ships;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Checklists;

/// <summary>A ship's plan put on the checklist writes a line for each ship material it leaves the Commander short of (#2727).</summary>
public class AShortPlanMaterialGetsALineTests
{
    private const int ShipId = 21;
    private const int OtherShipId = 22;

    private static readonly DateTimeOffset Now = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);

    private sealed class Bench
    {
        private int _second;

        public GameStateStore Game { get; } = new();

        public ChecklistService Checklists { get; }

        public ShipPlanService Ships { get; }

        public ChecklistCallout Callout { get; }

        public bool RemoveFulfilled { get; set; }

        public Bench()
        {
            var install = new MemoryInstall();

            Apply("""{"event":"Commander","FID":"F1","Name":"Jameson"}""");
            Apply($$"""{"event":"Loadout","Ship":"python","ShipID":{{OtherShipId}},"Modules":[{"Slot":"MainEngines","Item":"int_engine_size5_class5","On":true}]}""");
            Apply($$"""{"event":"Loadout","Ship":"python","ShipID":{{ShipId}},"Modules":[{"Slot":"MainEngines","Item":"int_engine_size5_class5","On":true}]}""");

            Checklists = new ChecklistService(
                new ChecklistStore(Path.Combine(install.Root, "checklist.json"), new MemoryFileSystem(), NullLogger<ChecklistStore>.Instance),
                new ChecklistProposalStore(
                    Path.Combine(install.Root, "checklist-proposals.json"),
                    new MemoryFileSystem(),
                    NullLogger<ChecklistProposalStore>.Instance),
                () => Game.Active,
                removeFulfilled: () => RemoveFulfilled);

            Ships = new ShipPlanService(
                new ShipBuildStore(Path.Combine(install.Root, "ships.json"), new MemoryFileSystem(), NullLogger<ShipBuildStore>.Instance),
                Checklists,
                () => Game.Active);

            Callout = new ChecklistCallout(Checklists);
        }

        public JournalEvent Apply(string json)
        {
            _second++;

            var stamped = json.Replace(
                "{\"event\"",
                $"{{\"timestamp\":\"2026-11-23T10:{_second / 60:00}:{_second % 60:00}Z\",\"event\"",
                StringComparison.Ordinal);

            Assert.True(JournalEvent.TryParse(stamped, NullLogger.Instance, out var parsed));
            Game.Apply(parsed!);
            return parsed!;
        }

        public ShipBuild Plan(int shipId = ShipId, string blueprint = "Dirty Drive Tuning", int grade = 3)
        {
            var build = Ships.BuildFor(shipId, "python");

            Ships.Plan(build.Id, new SlotPlan("MainEngines", blueprint, grade));
            return build;
        }

        /// <summary>A <c>Materials</c> snapshot holding these counts and nothing else.</summary>
        public JournalEvent Hold(IReadOnlyDictionary<string, int> counts)
        {
            var listed = string.Join(
                ",",
                counts.Select(pair => $$"""{"Name":"{{pair.Key}}","Count":{{pair.Value.ToString(CultureInfo.InvariantCulture)}}}"""));

            return Apply($$"""{"event":"Materials","Raw":[{{listed}}],"Manufactured":[],"Encoded":[]}""");
        }

        public JournalEvent HoldNothing() => Hold(new Dictionary<string, int>());

        /// <summary>What the open engineering lines of one list need, by material symbol.</summary>
        public IReadOnlyDictionary<string, PlanIngredient> Needs(ChecklistScope? scope = null) =>
            PlanMaterials.Needs(
                Checklists.Document.Items.Where(item => scope is null || item.Scope.Same(scope)),
                Game.Active);

        public IReadOnlyList<ChecklistItem> MaterialLines(ChecklistScope scope) =>
            [.. Checklists.Document.In(scope).Where(PlanMaterials.Is)];

        public IReadOnlyList<ChecklistNews> Poll(params JournalEvent[] events) =>
            Checklists.Poll(announce: true, events);

        public List<Announcement> Examine(bool priming, params JournalEvent[] events) =>
            [.. Callout.Examine(new CalloutContext(Now, priming, Game.Active!, GameStatus.Unknown, NavRoute.None, events))];
    }

    private static Dictionary<string, int> Counts(IReadOnlyDictionary<string, PlanIngredient> needs, int times = 1) =>
        needs.ToDictionary(pair => pair.Key, pair => pair.Value.Needed * times, StringComparer.OrdinalIgnoreCase);

    private static ChecklistItem Engineering(Bench bench, ChecklistScope scope) =>
        bench.Checklists.Document.In(scope).Single(item => item.Intent?.Kind == ChecklistIntentKind.Blueprint);

    [Fact]
    public void PromotingWritesALineForEachShortShipMaterialAndNoneForOneHeldInFull()
    {
        var bench = new Bench();
        var build = bench.Plan();

        bench.Ships.Promote(build.Id);
        var needs = bench.Needs(build.Scope);
        bench.Checklists.DeleteList(bench.Checklists.Lists().Single());

        var held = needs.Values.OrderBy(ingredient => ingredient.Material.Symbol, StringComparer.Ordinal).First();
        bench.Hold(new Dictionary<string, int> { [held.Material.Symbol] = held.Needed });

        var said = bench.Ships.Promote(build.Id);
        var lines = bench.MaterialLines(build.Scope);

        Assert.True(needs.Count > 1);
        Assert.Equal(needs.Count - 1, lines.Count);
        Assert.Contains($"{(1 + lines.Count).ToString(CultureInfo.InvariantCulture)} items are on your checklist", said, StringComparison.Ordinal);
        Assert.DoesNotContain(lines, line => line.Intent!.Subject == held.Material.Symbol);

        foreach (var line in lines)
        {
            var need = needs[line.Intent!.Subject];

            Assert.Equal($"Have {need.Needed.ToString(CultureInfo.InvariantCulture)} {need.Material.Name}", line.Text);
            Assert.Equal(ChecklistItemKind.Derived, line.Kind);
            Assert.Equal(ChecklistSource.EngineeringPlan, line.Source);
            Assert.Equal("python", line.Hull);
            Assert.Equal(ChecklistState.Open, line.State);
        }

        // Read from the engineering lines alone.
        Assert.Equal(needs.Values.Select(need => need.Needed), bench.Needs(build.Scope).Values.Select(need => need.Needed));
        Assert.Equal(1, bench.Checklists.Shortfall(build.Scope).PlanCount);
    }

    [Fact]
    public void TwoListsNeedingTheSameMaterialStayOpenUntilThereIsEnoughForBoth()
    {
        var bench = new Bench();
        var one = bench.Plan(ShipId);
        var two = bench.Plan(OtherShipId);

        bench.HoldNothing();
        bench.Ships.Promote(one.Id);
        var single = bench.Needs(one.Scope);
        bench.Hold(Counts(single));
        bench.Ships.Promote(two.Id);

        Assert.Equal(single.Count, bench.MaterialLines(one.Scope).Count);
        Assert.Equal(single.Count, bench.MaterialLines(two.Scope).Count);

        bench.Poll(bench.Hold(Counts(single)));

        foreach (var line in bench.MaterialLines(one.Scope).Concat(bench.MaterialLines(two.Scope)))
        {
            var need = single[line.Intent!.Subject];

            Assert.Equal(ChecklistState.Open, line.State);
            Assert.Equal($"Have {(need.Needed * 2).ToString(CultureInfo.InvariantCulture)} {need.Material.Name}", line.Text);

            var verdict = bench.Checklists.Verdict(line);
            Assert.Equal(
                $"This list needs {need.Needed} · {need.Needed} of {need.Needed * 2} so far",
                verdict!.Value.Says);
        }

        var news = bench.Poll(bench.Hold(Counts(single, times: 2)));

        Assert.All(bench.MaterialLines(one.Scope).Concat(bench.MaterialLines(two.Scope)), line => Assert.Equal(ChecklistState.Done, line.State));
        Assert.Empty(news);
        Assert.Empty(bench.Checklists.Drain());
    }

    [Fact]
    public void ATickedLineStaysTickedWhenTheMaterialIsSpent()
    {
        var bench = new Bench();
        var build = bench.Plan();

        bench.HoldNothing();
        bench.Ships.Promote(build.Id);
        var needs = bench.Needs(build.Scope);

        bench.Poll(bench.Hold(Counts(needs)));
        Assert.All(bench.MaterialLines(build.Scope), line => Assert.Equal(ChecklistState.Done, line.State));

        bench.Poll(bench.HoldNothing());

        var lines = bench.MaterialLines(build.Scope);

        Assert.Equal(needs.Count, lines.Count);
        Assert.All(lines, line => Assert.Equal(ChecklistState.Done, line.State));
    }

    [Fact]
    public void WithRemoveFulfilledOnALineThatTicksIsRemoved()
    {
        var bench = new Bench { RemoveFulfilled = true };
        var build = bench.Plan();

        bench.HoldNothing();
        bench.Ships.Promote(build.Id);
        var needs = bench.Needs(build.Scope);
        var first = needs.Values.First();

        bench.Poll(bench.Hold(new Dictionary<string, int> { [first.Material.Symbol] = first.Needed }));

        var lines = bench.MaterialLines(build.Scope);

        Assert.Equal(needs.Count - 1, lines.Count);
        Assert.DoesNotContain(lines, line => line.Intent!.Subject == first.Material.Symbol);
    }

    [Fact]
    public void DeletingAnEngineeringLineLowersTheNeedAndRemovesTheLinesItsListNoLongerNeeds()
    {
        var bench = new Bench();
        var one = bench.Plan(ShipId);
        var two = bench.Plan(OtherShipId);

        bench.HoldNothing();
        bench.Ships.Promote(one.Id);
        bench.Ships.Promote(two.Id);
        var single = bench.Needs(one.Scope);

        bench.Checklists.Delete(Engineering(bench, two.Scope).Id);
        bench.Poll();

        Assert.Empty(bench.MaterialLines(two.Scope));

        foreach (var line in bench.MaterialLines(one.Scope))
        {
            var need = single[line.Intent!.Subject];
            Assert.Equal($"Have {need.Needed.ToString(CultureInfo.InvariantCulture)} {need.Material.Name}", line.Text);
        }
    }

    [Fact]
    public void ARePromoteKeepsATickedLineAndAddsOnlyWhatIsNewlyShort()
    {
        var bench = new Bench();
        var build = bench.Plan();

        bench.HoldNothing();
        bench.Ships.Promote(build.Id);
        var needs = bench.Needs(build.Scope);

        bench.Poll(bench.Hold(Counts(needs)));
        bench.Poll(bench.HoldNothing());

        bench.Ships.Plan(build.Id, new SlotPlan("PowerPlant", "Overcharged", 3));
        bench.Ships.Promote(build.Id);

        var after = bench.Needs(build.Scope);
        var lines = bench.MaterialLines(build.Scope);

        Assert.Equal(after.Count, lines.Count);
        Assert.Equal(lines.Count, lines.Select(line => line.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(lines, line => Assert.Equal(
            needs.ContainsKey(line.Intent!.Subject) ? ChecklistState.Done : ChecklistState.Open,
            line.State));
    }

    [Fact]
    public void ARePromoteThatDropsASlotRemovesTheLinesNothingLeftNeeds()
    {
        var bench = new Bench();
        var build = bench.Plan(grade: 1);

        bench.Ships.Plan(build.Id, new SlotPlan("PowerPlant", "Overcharged", 5));
        bench.HoldNothing();
        bench.Ships.Promote(build.Id);
        var before = bench.MaterialLines(build.Scope).Count;

        bench.Ships.Clear(build.Id, "PowerPlant");
        bench.Ships.Promote(build.Id);
        bench.Poll();

        var needs = bench.Needs(build.Scope);
        var lines = bench.MaterialLines(build.Scope);

        Assert.True(lines.Count < before);
        Assert.Equal(needs.Count, lines.Count);
        Assert.All(lines, line => Assert.Contains(line.Intent!.Subject, needs.Keys));
    }

    [Fact]
    public void AnEngineeringLineThatIsDoneTakesTheLinesOnlyItNeeded()
    {
        var bench = new Bench();
        var build = bench.Plan();

        bench.HoldNothing();
        bench.Ships.Promote(build.Id);
        Assert.NotEmpty(bench.MaterialLines(build.Scope));

        // The fitted thrusters now carry the planned blueprint at its grade.
        bench.Poll(bench.Apply(
            $$$"""
            {"event":"Loadout","Ship":"python","ShipID":{{{ShipId}}},"Modules":[{"Slot":"MainEngines","Item":"int_engine_size5_class5","On":true,
             "Engineering":{"Engineer":"Felicity Farseer","EngineerID":300100,"BlueprintID":128673659,"BlueprintName":"Engine_Dirty","Level":3,"Quality":1.0,"Modifiers":[]}}]}
            """));

        Assert.Equal(ChecklistState.Done, Engineering(bench, build.Scope).State);

        bench.Poll();

        Assert.Empty(bench.MaterialLines(build.Scope));
    }

    [Fact]
    public void DeleteThisListTakesTheMaterialLinesAndDroppingAnOwnedShipsPlanKeepsThem()
    {
        var bench = new Bench();
        var one = bench.Plan(ShipId);
        var two = bench.Plan(OtherShipId);

        bench.HoldNothing();
        bench.Ships.Promote(one.Id);
        bench.Ships.Promote(two.Id);
        var kept = bench.MaterialLines(two.Scope).Count;

        bench.Checklists.DeleteList(bench.Checklists.Lists().Single(list => list.Scope.Same(one.Scope)));
        bench.Ships.Delete(two.Id);
        bench.Poll();

        Assert.Empty(bench.Checklists.Document.In(one.Scope));
        Assert.NotEqual(0, kept);
        Assert.Equal(kept, bench.MaterialLines(two.Scope).Count);
    }

    [Fact]
    public void BuyingAPlannedShipMovesItsMaterialLinesOntoTheShip()
    {
        var bench = new Bench();
        var build = bench.Ships.Intend("python", "Bad Idea")!;

        bench.Ships.Plan(build.Id, new SlotPlan("MainEngines", "Dirty Drive Tuning", 3));
        bench.HoldNothing();
        bench.Ships.Promote(build.Id);

        var planned = bench.MaterialLines(build.Scope);

        Assert.NotEmpty(planned);
        Assert.All(planned, line => Assert.Equal("Bad Idea", line.BuildName));
        Assert.All(planned, line => Assert.Equal("Bad Idea (planned)", ChecklistWording.Where(line, null)));

        bench.Ships.Adopt(build.Id, 30);

        Assert.Empty(bench.MaterialLines(build.Scope));
        Assert.Equal(planned.Count, bench.MaterialLines(ChecklistScope.Ship(30)).Count);
        Assert.All(bench.MaterialLines(ChecklistScope.Ship(30)), line => Assert.Equal("python", line.Hull));
    }

    [Fact]
    public void ALineSaysItIsForItsShip()
    {
        var line = PlanMaterials.Line(ChecklistScope.Ship(7), MaterialCatalogue.Find("arsenic")!, 12, "python", null);

        Assert.Equal("mat/arsenic", line.Key);
        Assert.Equal("Have 12 Arsenic for Python (ship 7)", ChecklistWording.Line(line, null));
        Assert.Null(ChecklistEvaluator.Evaluate(line, new Bench().Game.Active));
    }

    [Fact]
    public void ANeedOverTheStorageCapStaysOpenAndSaysTheTrips()
    {
        var bench = new Bench();
        bench.HoldNothing();

        var blueprints = Enumerable.Range(1, 60)
            .Select(id => new ChecklistItem
            {
                Key = $"bp/mainengines/{id}",
                Scope = ChecklistScope.Ship(100 + id),
                Kind = ChecklistItemKind.Derived,
                Source = ChecklistSource.EngineeringPlan,
                Text = "Grade 5 Dirty Drive Tuning on MainEngines",
                Intent = new ChecklistIntent(ChecklistIntentKind.Blueprint, "MainEngines") { Detail = "Dirty Drive Tuning", Grade = 5 },
                Hull = "python",
            })
            .ToList();

        var needs = PlanMaterials.Needs(blueprints, bench.Game.Active);
        var over = needs.Values.First(ingredient => ingredient.ExceedsCapacity);
        var line = PlanMaterials.Line(ChecklistScope.Ship(101), over.Material, over.Needed, "python", null);
        var document = ChecklistDocument.For("F1") with { Items = [.. blueprints, line] };

        var (moved, removed) = PlanMaterials.Reconcile(PlanMaterials.Read(document, bench.Game.Active!), document, bench.Game.Active!, removeFulfilled: false);
        var verdict = PlanMaterials.Verdict(line, PlanMaterials.Read(document, bench.Game.Active!), bench.Game.Active!);

        Assert.Empty(moved);
        Assert.Empty(removed);
        Assert.Equal(ChecklistState.Open, verdict!.Value.State);
        Assert.Contains(
            ShortfallTrip.Storage(over.Material.Name, over.Needed, over.Capacity!.Value).Sentence,
            verdict.Value.Says,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AMissionRewardThatCoversAMaterialIsAnnouncedOnceAndTheLineTicksWithoutATickedSentence()
    {
        var bench = new Bench();
        var build = bench.Plan();

        bench.HoldNothing();
        bench.Examine(priming: true);
        bench.Ships.Promote(bench.Ships.BuildFor(ShipId, "python").Id);
        bench.Poll();
        bench.Examine(priming: false);

        var target = bench.Needs(build.Scope).Values.First();
        var reward = bench.Apply(
            $$"""{"event":"MissionCompleted","MissionID":1,"Name":"Mission_Delivery","MaterialsReward":[{"Name":"{{target.Material.Symbol}}","Category":"Raw","Count":{{target.Needed}}}]}""");

        Assert.Empty(bench.Poll(reward));

        var said = bench.Examine(priming: false, reward)
            .Where(line => line.Key.StartsWith("checklist.filled.", StringComparison.Ordinal))
            .ToList();

        Assert.Equal("checklist.filled." + target.Material.Symbol, Assert.Single(said).Key);
        Assert.StartsWith($"That is the last {target.Material.Name} your plans needed.", said[0].Text, StringComparison.Ordinal);
        Assert.Equal(
            ChecklistState.Done,
            bench.MaterialLines(build.Scope).Single(line => line.Intent!.Subject == target.Material.Symbol).State);

        Assert.DoesNotContain(
            bench.Examine(priming: false, reward),
            line => line.Key.StartsWith("checklist.", StringComparison.Ordinal));
    }
}
