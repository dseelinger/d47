using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Checklists;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Ships;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Ships;

/// <summary>
/// A proposal about a build is reviewed slot by slot: accepting writes that slot through the plan service, rejecting
/// writes nothing, and a slot whose plan moved since the proposal is refused (#570).
/// </summary>
[Trait("Category", "Integration")]
public class AProposalReachesThePlanOnlyWhenAcceptedTests
{
    private const int ShipId = 21;

    private static readonly SlotPlan Engines = new("MainEngines", "Clean Drive Tuning", 3, Module: "Thrusters");

    private sealed record Bench(GameStateStore Game, ShipPlanService Ships, string BuildId);

    private static Bench Set(TempInstall install)
    {
        var game = new GameStateStore();

        game.Apply(Event("""{"timestamp":"2026-10-05T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}"""));
        game.Apply(Event($$"""
            {"timestamp":"2026-10-05T09:05:00Z","event":"Loadout","Ship":"python","ShipID":{{ShipId}},
             "Modules":[{"Slot":"MainEngines","Item":"int_engine_size5_class5","On":true}]}
            """));

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(install.Root, "checklist.json"), new MemoryFileSystem(), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(install.Root, "checklist-proposals.json"),
                new MemoryFileSystem(),
                NullLogger<ChecklistProposalStore>.Instance),
            () => game.Active);

        var ships = new ShipPlanService(
            new ShipBuildStore(Path.Combine(install.Root, "ships.json"), NullLogger<ShipBuildStore>.Instance),
            checklists,
            () => game.Active);

        var build = ships.BuildFor(ShipId, "python");
        ships.Plan(build.Id, Engines);

        return new Bench(game, ships, build.Id);
    }

    private static BuildAdvice ThreeChanges(ShipBuild build) => new(
        BuildRemarkKind.Goal,
        "Range first.",
        [
            new SlotChange("FrameShiftDrive", build.For("FrameShiftDrive"), new SlotPlan("FrameShiftDrive", "Increased FSD Range", 5, Module: "Frame Shift Drive"), "The drive is the range."),
            new SlotChange("MainEngines", build.For("MainEngines"), Engines with { Blueprint = "Dirty Drive Tuning", Grade = 2 }, "Lighter."),
            new SlotChange("PowerPlant", build.For("PowerPlant"), new SlotPlan("PowerPlant", "Low Emissions", 5, Module: "Power Plant"), "Cooler."),
        ],
        null,
        []);

    private static async Task<(BuildTalk Talk, TalkRound Round)> Proposed(Bench bench)
    {
        var talk = new BuildTalk(
            bench.Ships,
            (build, _, _, _) => Task.FromResult(ThreeChanges(build)),
            () => DateTimeOffset.UnixEpoch);

        await talk.AskAsync(bench.BuildId, "make it jump further", InputSource.Typed, TestContext.Current.CancellationToken);

        return (talk, Assert.Single(talk.Exchange(bench.BuildId)));
    }

    private static ShipBuild Build(Bench bench) => bench.Ships.Store.Find(bench.BuildId)!;

    [Fact]
    public async Task NothingReachesThePlanUntilAChangeIsAccepted()
    {
        using var install = new TempInstall();
        var bench = Set(install);
        var before = Build(bench).Slots;

        var (_, round) = await Proposed(bench);

        Assert.Equal(3, round.Advice!.Changes.Count);
        Assert.All(round.Decisions, decision => Assert.Equal(ChangeDecision.Undecided, decision));
        Assert.Equal(before, Build(bench).Slots);
    }

    [Fact]
    public async Task AcceptingOneChangeOfThreeWritesThatSlotOnly()
    {
        using var install = new TempInstall();
        var bench = Set(install);
        var (talk, round) = await Proposed(bench);

        Assert.Equal(AcceptOutcome.Written, talk.Accept(bench.BuildId, round.Id, 0));

        var build = Build(bench);
        Assert.Equal("Increased FSD Range", build.For("FrameShiftDrive")?.Blueprint);
        Assert.Equal(Engines, build.For("MainEngines"));
        Assert.Null(build.For("PowerPlant"));
        Assert.Equal(
            [ChangeDecision.Accepted, ChangeDecision.Undecided, ChangeDecision.Undecided],
            talk.Exchange(bench.BuildId)[0].Decisions);
    }

    [Fact]
    public async Task RejectingLeavesThePlanUntouched()
    {
        using var install = new TempInstall();
        var bench = Set(install);
        var before = Build(bench).Slots;
        var (talk, round) = await Proposed(bench);

        Assert.True(talk.Reject(bench.BuildId, round.Id, 1));
        Assert.Equal(3, talk.RejectAll(bench.BuildId, round.Id) + 1);

        Assert.Equal(before, Build(bench).Slots);
        Assert.All(talk.Exchange(bench.BuildId)[0].Decisions, decision => Assert.Equal(ChangeDecision.Rejected, decision));
    }

    [Fact]
    public async Task ADecisionIsFinal()
    {
        using var install = new TempInstall();
        var bench = Set(install);
        var (talk, round) = await Proposed(bench);

        talk.Reject(bench.BuildId, round.Id, 0);

        Assert.Equal(AcceptOutcome.Decided, talk.Accept(bench.BuildId, round.Id, 0));
        Assert.Null(Build(bench).For("FrameShiftDrive"));
    }

    [Fact]
    public async Task AcceptingAChangeWhoseSlotMovedSinceIsRefusedAndWritesNothing()
    {
        using var install = new TempInstall();
        var bench = Set(install);
        var (talk, round) = await Proposed(bench);

        var edited = Engines with { Grade = 5 };
        bench.Ships.Plan(bench.BuildId, edited);

        Assert.True(talk.IsStale(bench.BuildId, round.Advice!.Changes[1]));
        Assert.Equal(AcceptOutcome.Stale, talk.Accept(bench.BuildId, round.Id, 1));
        Assert.Equal(edited, Build(bench).For("MainEngines"));
        Assert.Equal(ChangeDecision.Undecided, talk.Exchange(bench.BuildId)[0].Decisions[1]);

        // Accept all skips it and takes the other two.
        Assert.Equal(2, talk.AcceptAll(bench.BuildId, round.Id));
        Assert.Equal(edited, Build(bench).For("MainEngines"));
        Assert.Equal("Low Emissions", Build(bench).For("PowerPlant")?.Blueprint);

        // Rejecting it still works.
        Assert.True(talk.Reject(bench.BuildId, round.Id, 1));
    }

    [Fact]
    public async Task AFailedTurnIsKeptWithItsCodeAndCanBeAskedAgain()
    {
        using var install = new TempInstall();
        var bench = Set(install);
        var calls = 0;

        var talk = new BuildTalk(
            bench.Ships,
            (build, _, _, _) => ++calls == 1
                ? throw new HttpRequestException("overloaded")
                : Task.FromResult(ThreeChanges(build)),
            () => DateTimeOffset.UnixEpoch);

        var failed = await talk.AskAsync(bench.BuildId, "make it jump further", InputSource.Typed, TestContext.Current.CancellationToken);

        Assert.False(failed!.Succeeded);
        Assert.Equal(nameof(HttpRequestException), failed.Code);

        var round = Assert.Single(talk.Exchange(bench.BuildId));
        var again = await talk.RetryAsync(bench.BuildId, round.Id, TestContext.Current.CancellationToken);

        Assert.True(again!.Succeeded);
        Assert.Equal(3, Assert.Single(talk.Exchange(bench.BuildId)).Decisions.Count);
    }

    [Fact]
    public async Task AModelThatNeverAnswersFailsTheRoundRatherThanHoldingThePage()
    {
        using var install = new TempInstall();
        var bench = Set(install);

        var talk = new BuildTalk(
            bench.Ships,
            async (_, _, _, token) =>
            {
                await Task.Delay(Timeout.Infinite, token);
                throw new InvalidOperationException("unreachable");
            },
            () => DateTimeOffset.UnixEpoch)
        {
            Limit = TimeSpan.FromMilliseconds(50),
        };

        var advice = await talk.AskAsync(bench.BuildId, "make it jump further", InputSource.Typed, TestContext.Current.CancellationToken);

        Assert.Equal("timeout", advice!.Code);
        Assert.False(talk.IsWorking(bench.BuildId));
        Assert.True(Assert.Single(talk.Exchange(bench.BuildId)).Failed);
    }

    [Fact]
    public async Task ClearingForgetsTheExchange()
    {
        using var install = new TempInstall();
        var bench = Set(install);
        var (talk, _) = await Proposed(bench);

        Assert.True(talk.Clear(bench.BuildId));
        Assert.Empty(talk.Exchange(bench.BuildId));
    }

    [Fact]
    public async Task ASpokenGoalIsAnsweredInThreeModulesAndTheProposalIsOnThePage()
    {
        using var install = new TempInstall();
        var bench = Set(install);
        var model = new ScriptedModel("""
            {"kind":"goal","reply":"Fit a bigger Frame Shift Drive. Tune the Thrusters. Cool the Power Plant. Charge the Power Distributor. Lighten the Life Support.","changes":[
              {"slot":"FrameShiftDrive","module":"Frame Shift Drive","class":5,"rating":"A","blueprint":"Increased FSD Range","grade":5,"reason":"Range."},
              {"slot":"MainEngines","blueprint":"Dirty Drive Tuning","grade":5,"reason":"Speed."},
              {"slot":"PowerPlant","module":"Power Plant","blueprint":"Low Emissions","grade":5,"reason":"Heat."},
              {"slot":"PowerDistributor","module":"Power Distributor","blueprint":"Charge Enhanced","grade":5,"reason":"Recharge."},
              {"slot":"LifeSupport","module":"Life Support","blueprint":"Lightweight","grade":5,"reason":"Mass."}]}
            """);

        var advisor = new ShipPlanAdvisor(() => model, () => null, () => null, () => null, () => bench.Game.Active, null, null, NullLogger.Instance);
        var talk = new BuildTalk(bench.Ships, advisor.AdviseAsync, () => DateTimeOffset.UnixEpoch);
        var before = Build(bench).Slots;

        var tool = ShipsCapability.Create(bench.Ships, new ShipsCapability.ShipsSurface { Talk = () => talk })
            .Tools.Single(definition => definition.Name == "talk_about_build");

        Assert.False(tool.Protected);

        // No page is open, so the remark is about the ship being flown.
        var result = await tool.Handler(
            new ToolArguments(new Dictionary<string, string> { ["remark"] = "make it jump further" }),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.True(result.Relayed);
        Assert.InRange(ShipPlanAdvisor.ModulesNamed(result.Content).Count, 1, ShipPlanAdvisor.MostModulesNamed);
        Assert.Contains("on the ship's page", result.Content, StringComparison.Ordinal);

        var round = Assert.Single(talk.Exchange(bench.BuildId));
        Assert.Equal(InputSource.Spoken, round.Source);
        Assert.Equal(5, round.Advice!.Changes.Count);
        Assert.Equal(before, Build(bench).Slots);
    }

    [Fact]
    public void ASpokenRemarkGoesToTheShipOpenOnThePage()
    {
        using var install = new TempInstall();
        var bench = Set(install);
        var intended = bench.Ships.Intend("anaconda")!;

        var talk = new BuildTalk(
            bench.Ships,
            (build, _, _, _) => Task.FromResult(new BuildAdvice(BuildRemarkKind.Critique, "Fine.", [], null, [])),
            () => DateTimeOffset.UnixEpoch)
        {
            Open = intended.Id,
        };

        Assert.Equal(intended.Id, talk.Target()?.Id);

        talk.Open = null;

        Assert.Equal(bench.BuildId, talk.Target()?.Id);
    }

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private sealed class ScriptedModel(string reply) : ILlmProvider
    {
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
            yield return new LlmStreamEvent.TextDelta(reply);
            yield return new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.Completed);
            await Task.CompletedTask;
        }
    }
}
