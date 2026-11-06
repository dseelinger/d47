using D47.Core.Checklists;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Ships;
using D47.Core.Storage;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Ships;

/// <summary>
/// A remark about a ship's plan is answered, and a goal comes back as per-slot changes checked against the
/// outfitting tables, costed by <see cref="EngineeringPlan"/>, and never applied (#569).
/// </summary>
public class ABuildPlanIsDiscussedNotRewrittenTests(ITestOutputHelper output)
{
    private static readonly ShipBuild Python = new("F1", "build-1", "python", null, "Long Shot",
        [new SlotPlan("MainEngines", "Clean Drive Tuning", 3, Module: "Thrusters")]);

    private const string JumpFurther = """
        {"kind":"goal","goal":{"mode":"only","target":"exploration","weights":{},"profile":"bounty","floors":[],"mustHaves":[],"credits":null,"engineering":false}}
        """;

    private const string Explained = """{"reply":"Range is up."}""";

    private static readonly BuildGoal SearchedGoal = new(
        new Dictionary<BuildRole, double>(), CombatProfile.Bounty, [], [], null, false, GoalMode.Only, BuildRole.Exploration);

    private static (ShipPlanAdvisor Advisor, ScriptedReplies Model) Advisor(CommanderGameState? state, params string[] replies)
    {
        var model = new ScriptedReplies(replies);

        return (new ShipPlanAdvisor(() => model, () => "high-model", () => null, () => null, () => state, null, null, NullLogger.Instance), model);
    }

    [Fact]
    public async Task AQuestionAboutASlotIsAnsweredWithNoChanges()
    {
        var (advisor, model) = Advisor(null, """
            {"kind":"question","reply":"Clean tuning keeps the Thrusters cool.","changes":[{"slot":"MainEngines","blueprint":"Dirty Drive Tuning","grade":5,"reason":"x"}]}
            """);

        var advice = await advisor.AdviseAsync(Python, [], "why that thruster tuning", TestContext.Current.CancellationToken);

        Assert.Equal(BuildRemarkKind.Question, advice.Kind);
        Assert.Equal("Clean tuning keeps the Thrusters cool.", advice.Reply);
        Assert.Empty(advice.Changes);
        Assert.Single(model.Requests);
    }

    [Fact]
    public async Task ACritiqueIsAnAssessmentWithNoChanges()
    {
        var (advisor, _) = Advisor(null, """
            {"kind":"critique","reply":"Half planned: the drive is stock.","changes":[]}
            """);

        var advice = await advisor.AdviseAsync(Python, [new BuildRemark("why clean tuning", "It runs cool.")], "is this build any good", TestContext.Current.CancellationToken);

        Assert.Equal(BuildRemarkKind.Critique, advice.Kind);
        Assert.Equal("Half planned: the drive is stock.", advice.Reply);
        Assert.Empty(advice.Changes);
    }

    [Fact]
    public async Task AGoalComesBackAsTheSearchsChangesAfterTwoRequests()
    {
        var (advisor, model) = Advisor(null, JumpFurther, Explained);

        var advice = await advisor.AdviseAsync(Python, [], "make it jump further, I don't care about the rest", TestContext.Current.CancellationToken);

        var found = BuildSearch.Find(Python, null, SearchedGoal, CancellationToken.None);

        Assert.Equal(BuildRemarkKind.Goal, advice.Kind);
        Assert.Equal(2, model.Requests.Count);
        Assert.NotEmpty(advice.Changes);
        Assert.Equal(found.Changes, advice.Changes);
        Assert.Empty(advice.Dropped);
        Assert.StartsWith("Range is up.", advice.Reply, StringComparison.Ordinal);
        Assert.Contains("no spending limit", advice.Reply, StringComparison.Ordinal);
        Assert.Equal(GoalMode.Only, advice.Goal!.Mode);
        Assert.Equal(BuildRole.Exploration, advice.Goal.Target);
    }

    [Fact]
    public async Task TheSecondRequestCarriesTheFiguresBeforeAndAfterAndNoRequestAsksTheModelToNameASlot()
    {
        var (advisor, model) = Advisor(null, JumpFurther, Explained);

        await advisor.AdviseAsync(Python, [], "make it jump further", TestContext.Current.CancellationToken);

        Assert.Contains("Figures before", model.Requests[1], StringComparison.Ordinal);
        Assert.Contains("Figures after", model.Requests[1], StringComparison.Ordinal);
        Assert.DoesNotContain("\"slot\"", model.Requests[0], StringComparison.Ordinal);
        Assert.DoesNotContain("\"blueprint\"", model.Requests[0], StringComparison.Ordinal);
        Assert.DoesNotContain("\"grade\"", model.Requests[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task AGoalThatDoesNotSayHowMuchGetsTheQuestionAndOneRequest()
    {
        var (advisor, model) = Advisor(null, """
            {"kind":"goal","ask":"Do you want range at the expense of everything else, or without giving up the rest?"}
            """);

        var advice = await advisor.AdviseAsync(Python, [], "more range", TestContext.Current.CancellationToken);

        Assert.Single(model.Requests);
        Assert.Empty(advice.Changes);
        Assert.Null(advice.Goal);
        Assert.Contains("expense of everything else", advice.Reply, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AFollowUpAmendsTheHeldGoal()
    {
        var (advisor, model) = Advisor(null, """
            {"kind":"goal","goal":{"mode":"keep_rest","target":"cargo"}}
            """, Explained);

        var held = SearchedGoal with { Floors = [new BuildFloor(BuildFigure.LadenJump, 20)] };
        var advice = await advisor.AdviseAsync(
            Python,
            [new BuildRemark("more range", "Done.", held)],
            "a bit more cargo",
            TestContext.Current.CancellationToken);

        Assert.Contains("The goal held from this conversation", model.Requests[0], StringComparison.Ordinal);
        Assert.Contains("\"mode\":\"only\"", model.Requests[0], StringComparison.Ordinal);
        Assert.Equal(GoalMode.KeepRest, advice.Goal!.Mode);
        Assert.Equal(BuildRole.Cargo, advice.Goal.Target);
        Assert.Equal(BuildFigure.LadenJump, Assert.Single(advice.Goal.Floors).Figure);
    }

    [Fact]
    public async Task AMustHaveTheTableDoesNotKnowIsDroppedAndNamed()
    {
        var (advisor, _) = Advisor(null, """
            {"kind":"goal","goal":{"mode":"only","target":"exploration","mustHaves":["Fuel Scoop","Flux Capacitor"]}}
            """, Explained);

        var advice = await advisor.AdviseAsync(Python, [], "range, and fit a flux capacitor", TestContext.Current.CancellationToken);

        Assert.Equal("Flux Capacitor", Assert.Single(advice.Dropped));
        Assert.Equal(["Fuel Scoop"], advice.Goal!.MustHaves);
        Assert.Contains("Flux Capacitor", advice.Reply, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACreditAmountTheCommanderNamedLimitsTheSearch()
    {
        var (advisor, _) = Advisor(null, """
            {"kind":"goal","goal":{"mode":"only","target":"exploration","credits":5000000}}
            """, Explained);

        var advice = await advisor.AdviseAsync(Python, [], "range for five million", TestContext.Current.CancellationToken);

        Assert.Equal(5_000_000, advice.Goal!.Credits);
        Assert.DoesNotContain("no spending limit", advice.Reply, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AGoalKeepsTheSearchsChangesWhenTheSecondRequestGivesNoReply()
    {
        var (advisor, _) = Advisor(null, JumpFurther, "not json");

        var advice = await advisor.AdviseAsync(Python, [], "make it jump further", TestContext.Current.CancellationToken);

        Assert.True(advice.Succeeded);
        Assert.NotEmpty(advice.Changes);
        Assert.StartsWith("I found", advice.Reply, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheProposalIsCostedByEngineeringPlanWithEveryChangeApplied()
    {
        var (advisor, _) = Advisor(null, JumpFurther.Replace("\"engineering\":false", "\"engineering\":true", StringComparison.Ordinal), Explained);

        var advice = await advisor.AdviseAsync(Python, [], "make it jump further", TestContext.Current.CancellationToken);

        var applied = advice.Changes.Aggregate(Python, (plan, change) => plan.With(change.After));
        var expected = EngineeringPlan.Cost(
            EngineeringPlan.Items(
                ChecklistScope.Universal,
                "python",
                [.. applied.Slots.Where(slot => slot.Blueprint is not null || slot.Grade > 0 || slot.Experimental is not null).Select(slot => slot.ToRequest())]),
            null);

        Assert.NotNull(advice.Cost);
        Assert.NotEmpty(expected.Ingredients);
        Assert.Equal(Figures(expected), Figures(advice.Cost));
        Assert.Equal(expected.Gates.Order(StringComparer.Ordinal), advice.Cost.Gates.Order(StringComparer.Ordinal));

        static IEnumerable<string> Figures(PlanCosting costing) =>
            costing.Ingredients
                .Select(ingredient => $"{ingredient.Material.Symbol}={ingredient.Needed}")
                .Order(StringComparer.Ordinal);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task NothingWritesToShipsJson()
    {
        var root = Directory.CreateTempSubdirectory("d47-advisor-");

        try
        {
            var store = new ShipBuildStore(Path.Combine(root.FullName, "ships.json"), new DiskFileSystem(), NullLogger<ShipBuildStore>.Instance);
            store.Save([Python]);
            var before = File.ReadAllBytes(store.Path);
            var written = File.GetLastWriteTimeUtc(store.Path);

            var (advisor, _) = Advisor(null, JumpFurther);
            var build = Assert.Single(store.BuildsFor("F1"));

            var advice = await advisor.AdviseAsync(build, [], "make it jump further", TestContext.Current.CancellationToken);

            Assert.NotEmpty(advice.Changes);
            Assert.Equal(before, File.ReadAllBytes(store.Path));
            Assert.Equal(written, File.GetLastWriteTimeUtc(store.Path));
            Assert.Equal(Python.Slots, Assert.Single(store.BuildsFor("F1")).Slots);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task NoReplyNamesMoreThanThreeModules()
    {
        var (advisor, _) = Advisor(
            null,
            """{"kind":"goal","goal":{"mode":"weighted","weights":{"combat":1,"exploration":1,"cargo":1}}}""",
            """{"reply":"Fit a bigger Frame Shift Drive. Tune the Thrusters. Cool the Power Plant. Charge the Power Distributor. Lighten the Life Support. Lighten the Sensors."}""");

        var advice = await advisor.AdviseAsync(Python, [], "make it jump further", TestContext.Current.CancellationToken);

        Assert.True(advice.Changes.Count > ShipPlanAdvisor.MostModulesNamed);
        Assert.NotNull(advice.Reply);
        Assert.InRange(ShipPlanAdvisor.ModulesNamed(advice.Reply).Count, 1, ShipPlanAdvisor.MostModulesNamed);
        Assert.EndsWith($"{advice.Changes.Count} slots change; they are on the ship's page.", advice.Reply, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEmptySlotAndAnUnknownModuleAreToldApart()
    {
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{"timestamp":"2026-10-05T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                     """{"timestamp":"2026-10-05T09:00:00Z","event":"Loadout","Ship":"python","ShipID":5,"ShipName":"Long Shot","ShipIdent":"LS-01","Modules":[{"Slot":"FrameShiftDrive","Item":"int_hyperdrive_size5_class5","On":true,"Priority":0,"Health":1.0},{"Slot":"Slot01_Size6","Item":"int_mysterybox_size6_class1","On":true,"Priority":0,"Health":1.0}]}""",
                 })
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        var owned = Python with { ShipId = 5 };
        var prompt = ShipPlanAdvisor.Instruction(owned, store.Active, [], "is this build any good");

        Assert.Contains("- MainEngines (Thrusters), size 6; plan: Thrusters, grade 3 Clean Drive Tuning; fitted: empty;", prompt, StringComparison.Ordinal);
        Assert.Contains("fitted: a module d47 does not know (int_mysterybox_size6_class1)", prompt, StringComparison.Ordinal);
        Assert.Contains("- FrameShiftDrive (Frame Shift Drive), size 5; plan: none; fitted: 5A Frame Shift Drive;", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void ThePromptCarriesThePlainWordsRule() =>
        Assert.Contains(D47.Core.Stories.PlainWords.Rule, ShipPlanAdvisor.Instruction(new ShipBuild("F1", "plain", "sidewinder", null, null, []), null, [], "is this build any good"), StringComparison.Ordinal);

    [Fact]
    public void ThePromptGivesTheFiguresDComputesForThePlan()
    {
        var build = new ShipBuild("F1", "figures", "python", null, "Figures",
        [
            new SlotPlan("Slot04_Size5") { Variant = "int_shieldgenerator_size5_class5" },
            new SlotPlan("LargeHardpoint1") { Variant = "hpt_pulselaser_fixed_large" },
            new SlotPlan("Slot01_Size6") { Variant = "int_cargorack_size5_class1" },
        ]);

        var prompt = ShipPlanAdvisor.Instruction(build, null, [], "is this build any good");
        var combat = ShipGauges.Read(build, null).Combat!;

        Assert.Contains("What the plan does (computed by d47", prompt, StringComparison.Ordinal);
        Assert.Contains("shields (modelled): " + combat.Shields!.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " MJ", prompt, StringComparison.Ordinal);
        Assert.Contains("damage (modelled): 18.12 per second raw", prompt, StringComparison.Ordinal);
        Assert.Contains("cargo (modelled): " + combat.Cargo.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " t", prompt, StringComparison.Ordinal);
        Assert.Contains("do not work out others", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void ThePromptForTheLargestBuildIsMeasured()
    {
        var hull = EliteSpecifications.Ships.MaxBy(ship => EliteSpecifications.Slots(ship.Symbol).Count)!;
        var slots = new List<SlotPlan>();

        foreach (var slot in EliteSpecifications.Slots(hull.Symbol))
        {
            if (EliteSpecifications.ModulesFor(slot).FirstOrDefault(module => BlueprintCatalogue.For(module) is { Count: > 0 }) is not { } module)
            {
                continue;
            }

            var recipe = BlueprintCatalogue.For(module)!.Where(blueprint => blueprint.Kind == BlueprintKind.Modification).MaxBy(blueprint => blueprint.Grade);
            slots.Add(new SlotPlan(slot.Name, recipe?.Name, recipe?.Grade ?? 0, Module: module.Name));
        }

        var build = new ShipBuild("F1", "largest", hull.Symbol, null, null, slots);
        var prompt = ShipPlanAdvisor.Instruction(build, null, [], "make it jump further");
        var tokens = D47.Core.Logbook.LogPrompt.Tokens(prompt.Length);

        output.WriteLine($"{hull.Name}: {slots.Count} slots planned, {prompt.Length} characters, about {tokens} tokens");
        Assert.InRange(tokens, 1, 16000);
    }
}
