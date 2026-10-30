using D47.Core.Adventures;
using D47.Core.Goals;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Stories;
using D47.Core.Tests.Adventures;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Goals;

/// <summary>A Goals chapter works the two arcs furthest behind that can each give a step, the first leading with two of the three slots.</summary>
public sealed class AGoalsChapterWorksTwoArcsAtOnceTests
{
    private static readonly GoalClimbStep Explore = new("rank.explore", "Elite V in Exploration") { Draw = "Explore" };

    private static readonly GoalClimbStep Trade = new("rank.trade", "Elite V in Trade") { Draw = "Trade" };

    private static readonly GoalClimbStep CombatRank = new("rank.combat", "Elite V in Combat")
    {
        Objective = new AdventureTrigger { Kind = TriggerKind.Rank, Career = "Combat", Rank = 2 },
    };

    private static readonly GoalClimbStep Pledge = new(GoalCatalogue.Powerplay, "Powerplay rank 100")
    {
        Objective = new AdventureTrigger { Kind = TriggerKind.Pledge },
    };

    private static List<GoalStanding> Standings(double fraction, params GoalStanding[] set) =>
    [
        .. GoalCatalogue.Every.Select(arc => set.FirstOrDefault(standing => standing.Arc.Key == arc.Key) ?? At(arc.Key, fraction)),
    ];

    private static GoalStanding At(string key, double fraction) => new()
    {
        Arc = GoalCatalogue.Find(key)!,
        Have = (long)Math.Round(fraction * 1000),
        Need = 1000,
        Source = GoalSource.Live,
    };

    [Fact]
    public void TheTwoArcsFurthestBehindAreStepsInTheOrderNextRanksThem()
    {
        var standings = Standings(0.5, At("rank.explore", 0.1), At("rank.trade", 0.2));

        var steps = GoalClimb.Steps(standings, Loaded(1_000_000_000), null);

        Assert.Equal(["rank.explore", "rank.trade"], steps.Select(step => step.Key));
        Assert.Equal(GoalClimb.Next(standings, Loaded(1_000_000_000), null), steps[0]);
    }

    [Fact]
    public void OneArcThatCanGiveAStepIsOneStep()
    {
        var standings = Standings(0.5, At("rank.explore", 0.1)).Select(standing => standing.Arc.Key == "rank.explore" ? standing : standing with { IsDone = true }).ToList();

        Assert.Equal("rank.explore", Assert.Single(GoalClimb.Steps(standings, Loaded(1_000_000_000), null)).Key);
    }

    [Fact]
    public void AnArcThatCannotGiveAStepIsPassedOverForTheSecond()
    {
        var standings = Standings(0.5, At("rank.explore", 0.1), At("rank.trade", 0.2), At("rank.combat", 0.3));

        var steps = GoalClimb.Steps(standings, Loaded(1_000_000_000), null, row => !GoalClimb.Earns("Trade", row));

        Assert.Equal(["rank.explore", "rank.combat"], steps.Select(step => step.Key));
    }

    [Fact]
    public void TwoDrawStepsDrawTwoSlotsForTheFirstAndOneForTheSecond()
    {
        foreach (var chapter in Enumerable.Range(1, 40))
        {
            var drawn = Draw([Explore, Trade], ChapterDraw.Seed("the-test-story", chapter));

            Assert.Equal(3, drawn.Count);
            Assert.True(Earns(Explore, drawn[0]) && Earns(Explore, drawn[1]), $"chapter {chapter}");
            Assert.True(Earns(Trade, drawn[2]), $"chapter {chapter}");
        }
    }

    [Fact]
    public void ASlotTheFirstArcCannotFillGoesToTheSecond()
    {
        var combat = new GoalClimbStep("rank.combat", "Elite V in Combat") { Draw = "Combat" };
        Func<DrawableActivity, bool> bounties = row => row.Kind == TriggerKind.Bounty;

        foreach (var chapter in Enumerable.Range(1, 40))
        {
            var drawn = ChapterDraw.Draw(
                CareerWeights.Even, RankState.Empty, ShipLoadout.Unknown, odyssey: true, [], null, comfort: false, CareerStatistics.Empty, 0,
                ChapterDraw.Seed("the-test-story", chapter), limits: [bounties, bounties, Trade.Earns]);

            Assert.Equal(3, drawn.Count);
            Assert.Equal(TriggerKind.Bounty, drawn[0].Kind);
            Assert.True(Earns(Trade, drawn[1]) && Earns(Trade, drawn[2]), $"chapter {chapter}");
            Assert.Equal([combat, Trade, Trade], GoalClimb.Credited([combat, Trade], drawn));
        }
    }

    [Fact]
    public void AnObjectiveStepTakesOneSlotAndDrawsNone()
    {
        Assert.Equal(ChapterDraw.Slots - 1, GoalClimb.Slots([CombatRank, Trade]));
        Assert.Single(GoalClimb.Limits([CombatRank, Trade]));
        Assert.Equal(ChapterDraw.Slots - 1, GoalClimb.Slots([Explore, CombatRank]));
        Assert.Equal(ChapterDraw.Slots - 1, GoalClimb.Limits([Explore, CombatRank]).Count);

        foreach (var chapter in Enumerable.Range(1, 40))
        {
            var drawn = Draw([CombatRank, Trade], ChapterDraw.Seed("the-test-story", chapter));

            Assert.Equal(ChapterDraw.Slots - 1, drawn.Count);
            Assert.True(Earns(Trade, drawn[0]), $"chapter {chapter}");
            Assert.All(Draw([Explore, CombatRank], ChapterDraw.Seed("the-test-story", chapter)), slot => Assert.True(Earns(Explore, slot)));
        }
    }

    [Fact]
    public void TwoObjectiveStepsLeaveOneSlotFromTheWholeTable()
    {
        Assert.Equal(1, GoalClimb.Slots([CombatRank, Pledge]));
        Assert.Empty(GoalClimb.Limits([CombatRank, Pledge]));

        var plain = ChapterDraw.Draw(
            CareerWeights.Even, RankState.Empty, ShipLoadout.Unknown, odyssey: true, [], null, comfort: false, CareerStatistics.Empty, 0, 5u, slots: 1);

        Assert.Equal(plain, Draw([CombatRank, Pledge], 5u));
    }

    [Fact]
    public void ALoneDrawStepLimitsEverySlot()
    {
        Assert.Equal(ChapterDraw.Slots, GoalClimb.Limits([Explore]).Count);
        Assert.Equal(ChapterDraw.Slots, GoalClimb.Slots([Explore]));
        Assert.Equal(ChapterDraw.Slots - 1, GoalClimb.Slots([CombatRank]));
        Assert.Empty(GoalClimb.Limits([CombatRank]));
    }

    [Fact]
    public void AStepWhoseLimitedDrawFellBackIsDropped()
    {
        var nowhere = new GoalClimbStep("rank.trade", "Elite V in Trade") { Draw = "Nowhere" };

        var drawn = Draw([Explore, nowhere], 9u);

        Assert.Equal(3, drawn.Count);
        Assert.Equal([Explore], GoalClimb.Worked([Explore, nowhere], drawn));
        Assert.Equal([CombatRank], GoalClimb.Worked([CombatRank], []));
    }

    [Fact]
    public async Task TheWriterIsToldBothGoalsAndOneStoryLineForThem()
    {
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(BeatsWithTheTradeRankOnly),
            RoundScriptedLlmProvider.Saying(Repairs.Replacing(BeatsWithTheTradeRankOnly, 2)),
            RoundScriptedLlmProvider.Saying(Repairs.Replacing(BeatsWithTheTradeRankOnly, 2)));
        var trade = new GoalClimbStep("rank.trade", "Elite V in Trade")
        {
            Objective = new AdventureTrigger { Kind = TriggerKind.Rank, Career = "Trade", Rank = 8 },
        };

        var outcome = await Generate(provider, [CombatRank, trade]);
        var prompt = provider.Requests[1].Prompt.History[0].Text;

        Assert.False(outcome.Succeeded);
        Assert.Contains("has no objective for its Goals step: a \"rank\" objective for Combat Rank 2 of 13, Novice", outcome.Refusal);
        Assert.DoesNotContain("for Trade Rank", outcome.Refusal);
        Assert.Contains("works the Commander's goal \"Elite V in Combat\"", prompt);
        Assert.Contains("works the Commander's goal \"Elite V in Trade\"", prompt);
        Assert.Contains("works both goals, \"Elite V in Combat\" and \"Elite V in Trade\", in one story line", prompt);
    }

    [Fact]
    public async Task TwoDrawStepsAreToldWhichActivitiesWorkEach()
    {
        var provider = new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(BeatsWithTheTradeRankOnly));

        await Generate(provider, [Explore, Trade], [new DrawnActivity(TriggerKind.Map, null, 2), new DrawnActivity(TriggerKind.Sell, null, 40)]);
        var prompt = provider.Requests[1].Prompt.History[0].Text;

        Assert.Contains("The drawn activities to map bodies with the surface mapper work the Commander's goal \"Elite V in Exploration\".", prompt);
        Assert.Contains("The drawn activities to sell cargo work the Commander's goal \"Elite V in Trade\".", prompt);
    }

    [Fact]
    public void EachSlotIsCreditedToOneStepByItsShare()
    {
        var empire = new GoalClimbStep("rank.empire", "Duke in the Imperial Navy") { Draw = "Empire" };
        var federation = new GoalClimbStep("rank.federation", "Admiral in the Federal Navy") { Draw = "Federation" };
        DrawnActivity[] missions = [Mission("Mission_Courier"), Mission("Mission_Delivery"), Mission("Mission_Collect")];

        Assert.Equal([empire, empire, federation], GoalClimb.Credited([empire, federation], missions));
        Assert.Equal([Explore, Explore, null], GoalClimb.Credited([CombatRank, Explore], [new DrawnActivity(TriggerKind.Map, null, 2), new DrawnActivity(TriggerKind.Map, null, 3), new DrawnActivity(TriggerKind.Bounty, null, 3)]));
    }

    [Fact]
    public async Task TwoNaviesAreEachToldOnlyTheMissionsCreditedToThem()
    {
        var empire = new GoalClimbStep("rank.empire", "Duke in the Imperial Navy") { Draw = "Empire" };
        var federation = new GoalClimbStep("rank.federation", "Admiral in the Federal Navy") { Draw = "Federation" };
        var provider = new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(BeatsWithTheTradeRankOnly));

        await Generate(provider, [empire, federation], [Mission("Mission_Courier"), Mission("Mission_Delivery"), Mission("Mission_Collect")]);
        var lines = provider.Requests[1].Prompt.History[0].Text.Split('\n');
        var courier = RefusedActivities.Phrase(TriggerKind.Mission, "Mission_Courier")!;
        var delivery = RefusedActivities.Phrase(TriggerKind.Mission, "Mission_Delivery")!;
        var collect = RefusedActivities.Phrase(TriggerKind.Mission, "Mission_Collect")!;

        Assert.Contains(lines, line => line.StartsWith($"The drawn activities to {courier} and {delivery} work the Commander's goal \"Duke in the Imperial Navy\"", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.StartsWith($"The drawn activities to {collect} work the Commander's goal \"Admiral in the Federal Navy\"", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AOneStepChapterIsWrittenAsBefore()
    {
        var provider = new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(BeatsWithTheTradeRankOnly));

        await Generate(provider, [Explore], [new DrawnActivity(TriggerKind.Map, null, 2)]);
        var prompt = provider.Requests[1].Prompt.History[0].Text;

        Assert.Contains("The drawn activities work the Commander's goal \"Elite V in Exploration\".", prompt);
        Assert.DoesNotContain("both goals", prompt);
    }

    private const string BeatsWithTheTradeRankOnly = """
        {"opening": "Again.", "reply": "Here.", "beats": [
          {"title": "The Lantern", "function": "setup", "kind": "arrive", "reason": "Someone there knows about the burst.", "system": "Ossen's Lantern", "line": "Back."},
          {"title": "The Turn", "function": "turn", "kind": "rank", "system": null, "station": null, "body": null, "career": "Trader", "rank": 8, "line": "Turn."},
          {"title": "The Bounties", "function": "resolution", "kind": "bounty", "count": 3, "line": "Paid."}
        ]}
        """;

    private static Task<AdventureOutcome> Generate(RoundScriptedLlmProvider provider, IReadOnlyList<GoalClimbStep> goals, IReadOnlyList<DrawnActivity>? slots = null) =>
        AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy()).GenerateAsync(
            new AdventureAsk(
                AdventureReach.Session,
                AdventureLength.Short,
                Story: new AdventureStory(Id, Card.Title, Card.Describe(), Secret.Secret, 3, 10, 5, Slots: slots, Goals: goals)),
            Now,
            CancellationToken.None);

    private static DrawnActivity Mission(string family) => new(TriggerKind.Mission, family, 2);

    private static bool Earns(GoalClimbStep step, DrawnActivity slot) => step.Earns(ChapterActivities.Find(slot.Kind, slot.Family, slot.Organic)!);

    private static IReadOnlyList<DrawnActivity> Draw(IReadOnlyList<GoalClimbStep> steps, uint seed) =>
        ChapterDraw.Draw(
            CareerWeights.Even,
            RankState.Empty,
            ShipLoadout.Unknown,
            odyssey: true,
            [],
            null,
            comfort: false,
            CareerStatistics.Empty,
            0,
            seed,
            events: StepEventCatalogue.Default,
            tools: [new DrawableTool("plot_trade_route", "plot a trade route")],
            slots: GoalClimb.Slots(steps),
            limits: GoalClimb.Limits(steps));
}
