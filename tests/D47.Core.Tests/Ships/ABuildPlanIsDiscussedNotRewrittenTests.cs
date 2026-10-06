using D47.Core.Checklists;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Ships;
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
        {"kind":"goal","reply":"Range first, shields last.","changes":[
          {"slot":"FrameShiftDrive","module":"Frame Shift Drive","class":5,"rating":"A","blueprint":"Increased FSD Range","grade":5,"experimental":"Mass Manager","reason":"The drive is the range."},
          {"slot":"MainEngines","blueprint":"Dirty Drive Tuning","grade":2,"reason":"Lighter tuning, less mass."}]}
        """;

    private static (ShipPlanAdvisor Advisor, ScriptedModel Model) Advisor(CommanderGameState? state, params string[] replies)
    {
        var model = new ScriptedModel(replies);

        return (new ShipPlanAdvisor(() => model, () => null, () => null, () => null, () => state, null, null, NullLogger.Instance), model);
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
    public async Task AGoalComesBackAsPerSlotChanges()
    {
        var (advisor, _) = Advisor(null, JumpFurther);

        var advice = await advisor.AdviseAsync(Python, [], "make it jump further, I don't care about the shields", TestContext.Current.CancellationToken);

        Assert.Equal(BuildRemarkKind.Goal, advice.Kind);
        Assert.Empty(advice.Dropped);
        Assert.Collection(
            advice.Changes,
            drive =>
            {
                Assert.Equal("FrameShiftDrive", drive.Slot);
                Assert.Null(drive.Before);
                Assert.Equal("Frame Shift Drive", drive.After.Module);
                Assert.Equal("Increased FSD Range", drive.After.Blueprint);
                Assert.Equal(5, drive.After.Grade);
                Assert.Equal("Mass Manager", drive.After.Experimental);
                Assert.Equal("The drive is the range.", drive.Reason);
            },
            engines =>
            {
                Assert.Equal("MainEngines", engines.Slot);
                Assert.Equal(Python.For("MainEngines"), engines.Before);
                Assert.Equal("Thrusters", engines.After.Module);
                Assert.Equal("Dirty Drive Tuning", engines.After.Blueprint);
                Assert.Equal(2, engines.After.Grade);
                Assert.Equal("Lighter tuning, less mass.", engines.Reason);
            });
    }

    [Fact]
    public async Task AModuleThatDoesNotFitAndABlueprintThatDoesNotExistAreRefusedByNameAndAskedAgainOnce()
    {
        var (advisor, model) = Advisor(
            null,
            """
            {"kind":"goal","reply":"Bigger engines.","changes":[
              {"slot":"MainEngines","module":"Thrusters","class":8,"rating":"A","reason":"More thrust."},
              {"slot":"FrameShiftDrive","module":"Frame Shift Drive","blueprint":"Dirty Drive Tuning","grade":5,"reason":"Faster."}]}
            """,
            JumpFurther);

        var advice = await advisor.AdviseAsync(Python, [], "make it jump further", TestContext.Current.CancellationToken);

        Assert.Equal(2, model.Requests.Count);
        Assert.Contains("MainEngines: 8A Thrusters does not fit Thrusters", model.Requests[1], StringComparison.Ordinal);
        Assert.Contains("FrameShiftDrive: there is no blueprint called Dirty Drive Tuning for Frame Shift Drive", model.Requests[1], StringComparison.Ordinal);
        Assert.Equal(2, advice.Changes.Count);
        Assert.Empty(advice.Dropped);
    }

    [Fact]
    public async Task WhatStillFailsIsDroppedAndNamedInTheReply()
    {
        const string Wrong = """
            {"kind":"goal","reply":"Lighter drive.","changes":[
              {"slot":"FrameShiftDrive","module":"Frame Shift Drive","class":5,"rating":"A","blueprint":"Increased FSD Range","grade":5,"reason":"Range."},
              {"slot":"MainEngines","module":"Thrusters","class":8,"rating":"A","reason":"More thrust."}]}
            """;

        var (advisor, model) = Advisor(null, Wrong, Wrong, Wrong);

        var advice = await advisor.AdviseAsync(Python, [], "make it jump further", TestContext.Current.CancellationToken);

        Assert.Equal(2, model.Requests.Count);
        Assert.Equal("FrameShiftDrive", Assert.Single(advice.Changes).Slot);
        Assert.Contains("MainEngines", Assert.Single(advice.Dropped), StringComparison.Ordinal);
        Assert.Contains("I left out the change to Thrusters", advice.Reply, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheProposalIsCostedByEngineeringPlanWithEveryChangeApplied()
    {
        var (advisor, _) = Advisor(null, JumpFurther);

        var advice = await advisor.AdviseAsync(Python, [], "make it jump further", TestContext.Current.CancellationToken);

        var expected = EngineeringPlan.Cost(
            EngineeringPlan.Items(
                ChecklistScope.Universal,
                "python",
                [
                    new BuildRequest("MainEngines", "Dirty Drive Tuning", 2, null, "Thrusters"),
                    new BuildRequest("FrameShiftDrive", "Increased FSD Range", 5, "Mass Manager", "Frame Shift Drive"),
                ]),
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

    [Fact]
    public async Task NothingWritesToShipsJson()
    {
        var root = Directory.CreateTempSubdirectory("d47-advisor-");

        try
        {
            var store = new ShipBuildStore(Path.Combine(root.FullName, "ships.json"), NullLogger<ShipBuildStore>.Instance);
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
        var (advisor, _) = Advisor(null, """
            {"kind":"goal","reply":"Fit a bigger Frame Shift Drive. Tune the Thrusters. Cool the Power Plant. Charge the Power Distributor. Lighten the Life Support. Lighten the Sensors.","changes":[
              {"slot":"FrameShiftDrive","module":"Frame Shift Drive","class":5,"rating":"A","blueprint":"Increased FSD Range","grade":5,"reason":"Range."},
              {"slot":"MainEngines","blueprint":"Dirty Drive Tuning","grade":5,"reason":"Speed."},
              {"slot":"PowerPlant","module":"Power Plant","blueprint":"Low Emissions","grade":5,"reason":"Heat."},
              {"slot":"PowerDistributor","module":"Power Distributor","blueprint":"Charge Enhanced","grade":5,"reason":"Recharge."},
              {"slot":"LifeSupport","module":"Life Support","blueprint":"Lightweight","grade":5,"reason":"Mass."},
              {"slot":"Radar","module":"Sensors","blueprint":"Light Weight Scanner","grade":5,"reason":"Mass."}]}
            """);

        var advice = await advisor.AdviseAsync(Python, [], "make it jump further", TestContext.Current.CancellationToken);

        Assert.Equal(6, advice.Changes.Count);
        Assert.NotNull(advice.Reply);
        Assert.InRange(ShipPlanAdvisor.ModulesNamed(advice.Reply).Count, 1, ShipPlanAdvisor.MostModulesNamed);
        Assert.EndsWith("6 slots change; they are on the ship's page.", advice.Reply, StringComparison.Ordinal);
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

    private sealed class ScriptedModel(params string[] replies) : ILlmProvider
    {
        public List<string> Requests { get; } = [];

        public string Id => "scripted";

        public string DisplayName => "Scripted";

        public string DefaultModel => "scripted";

        public LlmProviderCapabilities CapabilitiesFor(string model) => new()
        {
            SupportsPromptCaching = false,
            SupportsThinkingEffort = false,
            SupportsOperatorSystemMessages = false,
            MinimumCacheablePrefixTokens = 0,
        };

        public async IAsyncEnumerable<LlmStreamEvent> StreamAsync(
            LlmRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Requests.Add(string.Join("\n", request.Prompt.History.Select(message => message.ToString())));
            yield return new LlmStreamEvent.TextDelta(replies[Math.Min(Requests.Count, replies.Length) - 1]);
            yield return new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.Completed);
            await Task.CompletedTask;
        }
    }
}
