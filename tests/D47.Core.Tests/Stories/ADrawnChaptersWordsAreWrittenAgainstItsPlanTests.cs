using D47.Core.Adventures;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.PlannedChapterWorld;

namespace D47.Core.Tests.Stories;

/// <summary>
/// A story chapter with drawn activities is planned in code: one lines call writes its spine, opening and each objective's words
/// against the plan, the checks on lines run, and a repair or a redraft writes words again against the same plan.
/// </summary>
public sealed class ADrawnChaptersWordsAreWrittenAgainstItsPlanTests
{
    private static readonly string[] PlaceKindOrCount = ["kind", "system", "station", "body", "count", "faction", "commodity", "destination"];

    private static readonly DateTimeOffset Now = new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);

    private static async Task<(AdventureOutcome Outcome, RoundScriptedLlmProvider Provider, ChapterPlan Plan)> Written(AdventureAsk ask, Func<ChapterPlan, string[]> answers)
    {
        var plan = await PlanAsync(ask);
        var provider = new RoundScriptedLlmProvider([.. answers(plan).Select(answer => RoundScriptedLlmProvider.Saying(answer))]);
        var outcome = await Generator(provider).GenerateAsync(ask, Now, TestContext.Current.CancellationToken);

        return (outcome, provider, plan);
    }

    private static IEnumerable<string?> Schemas(RoundScriptedLlmProvider provider) => provider.Requests.Select(request => request.ResponseSchema?.Name);

    [Fact]
    public async Task AChapterWhoseChecksPassIsWrittenInOneLinesCallBesideTheTwoChecks()
    {
        var (outcome, provider, plan) = await Written(Ask(), plan => [Lines(plan.Objectives.Count), NoObjections]);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal(["story_lines", "story_fidelity", "story_plain"], Schemas(provider));
        Assert.Empty(Properties(provider.Requests[0].ResponseSchema!.SchemaJson).Intersect(PlaceKindOrCount));
    }

    [Fact]
    public async Task ALinesReplyThatNamesAnotherPlaceLeavesThePlansTriggers()
    {
        var (outcome, _, plan) = await Written(Ask(), plan =>
        [
            Lines(plan.Objectives.Count, (number, objective) =>
            {
                objective["kind"] = "dock";
                objective["system"] = "Shinrarta Dezhra";
                objective["station"] = "Jameson Memorial";
                objective["count"] = 99;
                objective["lines"]![0]!["text"] = "Dock at Jameson Memorial in Shinrarta Dezhra.";
            }),
            NoObjections,
        ]);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal(plan.Objectives.Select(objective => objective.Steps), outcome.Draft!.Beats.Select(beat => beat.Steps.Select(step => step.Trigger)), new StepsComparer());
        Assert.Equal(plan.Objectives.Select(objective => objective.Function), outcome.Draft.Beats.Select(beat => beat.Function));
    }

    [Fact]
    public async Task TheLinesSchemaHasNoFieldForAPlaceAKindOrACount()
    {
        var (_, provider, _) = await Written(Ask(), plan => [Lines(plan.Objectives.Count), NoObjections]);
        var lines = provider.Requests.Single(request => request.ResponseSchema?.Name == "story_lines").ResponseSchema!.SchemaJson;

        Assert.Empty(Properties(lines).Intersect(PlaceKindOrCount));
        Assert.Contains("reason", Properties(lines));
        Assert.Contains("lines", Properties(lines));
        Assert.NotEmpty(Properties(lines.Replace("\"reason\":", "\"system\":", StringComparison.Ordinal)).Intersect(PlaceKindOrCount));
    }

    [Fact]
    public async Task TheLinesCallIsToldThePlanObjectiveByObjective()
    {
        var (_, provider, plan) = await Written(Ask(), plan => [Lines(plan.Objectives.Count), NoObjections]);
        var prompt = provider.Requests[0].Prompt.History[0].Text;

        Assert.Contains("collect 5 bounties", prompt);
        Assert.Contains("log 4 codex entries", prompt);
        Assert.Contains("pick up 3 cargo canisters", prompt);

        foreach (var (objective, index) in plan.Objectives.Select((objective, index) => (objective, index)))
        {
            Assert.Contains($"{index + 1}. {objective.Function}: ", prompt);
        }

        Assert.Contains("Public", prompt);
        Assert.Contains("Hidden", prompt);
        Assert.Contains("\"mara\": Mara Venn", prompt);
        Assert.DoesNotContain("Vary what the Commander does", prompt);
        Assert.DoesNotContain("in no order of preference", prompt);
    }

    [Fact]
    public async Task AChapterInTheBlackIsToldSoForItsWords()
    {
        var (outcome, provider, _) = await Written(Ask(PlannedChapterWorld.Story with { InTheBlack = true }), plan => [Lines(plan.Objectives.Count), NoObjections]);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Contains("This chapter of the story takes place in the black.", provider.Requests[0].Prompt.History[0].Text);
    }

    [Fact]
    public async Task AFidelityObjectionIsRepairedWithLinesOnly()
    {
        var objection = """{"objections": [{"objective": 1, "quote": "The ledger has another page, the one.", "kind": "action", "reason": "The objective does not confirm a page."}]}""";
        var (outcome, provider, plan) = await Written(Ask(), plan => [Lines(plan.Objectives.Count), objection, Repair(1), NoObjections]);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal(["story_lines", "story_fidelity", "story_line_repairs", "story_fidelity", "story_plain"], Schemas(provider));
        Assert.Contains("The objective does not confirm a page.", provider.Requests[2].Prompt.History[0].Text);
        Assert.Equal("Part one Again", outcome.Draft!.Beats[0].Title);
        Assert.Equal(plan.Objectives.Select(objective => objective.Steps), outcome.Draft.Beats.Select(beat => beat.Steps.Select(step => step.Trigger)), new StepsComparer());
    }

    [Fact]
    public async Task AnObjectiveThePlainCheckRefusesAgainAfterItsOwnRewriteGoesToTheWriterWithEveryFinding()
    {
        const string first = """{"objections": [{"part": "objective 1", "quote": "another page", "kind": "figurative", "reason": "a page stands for a lead.", "plain": "a further record"}]}""";
        const string second = """{"objections": [{"part": "objective 1", "quote": "a further record", "kind": "figurative", "reason": "it does not say what is recorded.", "plain": "a cargo manifest"}]}""";
        var ask = Ask();
        var plan = await PlanAsync(ask);
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Lines(plan.Objectives.Count)),
            RoundScriptedLlmProvider.Saying(NoObjections),
            RoundScriptedLlmProvider.Saying(first),
            RoundScriptedLlmProvider.Saying(NoObjections),
            RoundScriptedLlmProvider.Saying(second),
            RoundScriptedLlmProvider.Saying(Repair(1)),
            RoundScriptedLlmProvider.Saying(NoObjections),
            RoundScriptedLlmProvider.Saying(NoObjections))
        {
            ScriptsPlainChecks = true,
        };

        var outcome = await Generator(provider).GenerateAsync(ask, Now, TestContext.Current.CancellationToken);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal(["story_lines", "story_fidelity", "story_plain", "story_fidelity", "story_plain", "story_line_repairs", "story_fidelity", "story_plain"], Schemas(provider));
        var repair = provider.Requests[5].Prompt.History[0].Text;
        Assert.Contains("\"a further record\" is figurative", repair);
        Assert.Contains("\"another page\" is figurative", repair);
        Assert.Equal("Part one Again", outcome.Draft!.Beats[0].Title);
    }

    [Fact]
    public async Task ADraftThatStillFailsIsWrittenAgainAgainstTheSamePlan()
    {
        Action<int, System.Text.Json.Nodes.JsonObject> noReasons = (_, objective) => objective.Remove("reason");
        var (first, _, plan) = await Written(Ask(), plan => [Lines(plan.Objectives.Count, noReasons), Lines(plan.Objectives.Count, noReasons), Lines(plan.Objectives.Count, noReasons)]);

        Assert.False(first.Succeeded);
        Assert.True(first.Redraftable);
        Assert.Contains("has no \"reason\"", first.Refusal);
        Assert.Equal(PlannerWorld.Describe(plan), PlannerWorld.Describe(first.Plan!));

        var galaxy = new Galaxy();
        var provider = new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Lines(plan.Objectives.Count)), RoundScriptedLlmProvider.Saying(NoObjections));
        var second = await Generator(provider, galaxy).GenerateAsync(Ask() with { Plan = first.Plan, Draft = 2, LastDraftRefused = first.ForNextDraft }, Now, TestContext.Current.CancellationToken);

        Assert.True(second.Succeeded, second.Refusal);
        Assert.Equal(0, galaxy.Searches);
        Assert.Equal(first.Plan!.Objectives.Select(objective => objective.Steps), second.Draft!.Beats.Select(beat => beat.Steps.Select(step => step.Trigger)), new StepsComparer());
        Assert.Contains("has no \"reason\"", provider.Requests[0].Prompt.History[0].Text);
    }

    [Fact]
    public async Task APlannerRefusalFailsTheWriteWithItsReasonAndIsNotWrittenAgain()
    {
        var provider = new RoundScriptedLlmProvider();
        var nowhere = State(position: false);
        var outcome = await Generator(provider, state: () => nowhere).GenerateAsync(Ask(), Now, TestContext.Current.CancellationToken);

        Assert.False(outcome.Succeeded);
        Assert.False(outcome.Redraftable);
        Assert.StartsWith("The chapter could not be planned: ", outcome.Refusal);
        Assert.Contains("position is unknown", outcome.Message);
        Assert.Empty(provider.Requests);
    }

    [Fact]
    public async Task AChapterAfterOneTheModelWroteIsPlannedAndToldHowThatOneEnded()
    {
        var before = new Adventure
        {
            Key = "the-old-way",
            Name = "The Old Way",
            Source = AdventureSource.Generated,
            Written = Now.AddDays(-1),
            StoryId = PlannedChapterWorld.Story.Id,
            Spine = new AdventureSpine { Premise = "A debt.", Want = "To pay it.", Stake = "Trust.", Turn = "It is owed.", Ending = "The debt is forgiven at the dock." },
            Beats =
            [
                new AdventureBeat { Title = "Blood", Function = "midpoint, a turn the model named", Steps = [new BeatStep { Trigger = new AdventureTrigger { Kind = TriggerKind.Bounty, Count = 2 }, Reason = "Debts." }], Line = "Two." },
            ],
        };
        var ask = Ask() with { Chapter = new AdventureChapter(before, []) };
        var (outcome, provider, plan) = await Written(ask, plan => [Lines(plan.Objectives.Count), NoObjections]);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal("the-old-way", outcome.Draft!.Follows);
        Assert.Contains("How its spine said it ends: The debt is forgiven at the dock.", provider.Requests[0].Prompt.History[0].Text);
        Assert.Equal(plan.Objectives.Select(objective => objective.Function), outcome.Draft.Beats.Select(beat => beat.Function));
    }

    [Fact]
    public async Task ATurnAndASetbackAreLabelledByCodeSoTheBackstoryReminderFollowsThem()
    {
        var (outcome, _, _) = await Written(Ask(), plan => [Lines(plan.Objectives.Count), NoObjections]);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Contains(outcome.Draft!.Beats, beat => beat.Function == ChapterFunctions.Turn);
        Assert.Equal(ChapterFunctions.Setup, outcome.Draft.Beats[0].Function);
        Assert.Equal(ChapterFunctions.Resolution, outcome.Draft.Beats[^1].Function);
    }

    [Fact]
    public async Task ALineGivenToSomeoneOutsideTheStoryIsSentBackOnce()
    {
        Action<int, System.Text.Json.Nodes.JsonObject> stranger = (number, objective) =>
        {
            if (number == 2)
            {
                objective["lines"]![0]!["speaker"] = "stranger";
            }
        };
        var (outcome, provider, plan) = await Written(Ask(), plan => [Lines(plan.Objectives.Count, stranger), Repair(2), NoObjections]);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal(["story_lines", "story_line_repairs", "story_fidelity", "story_plain"], Schemas(provider));
        Assert.Contains("A line is given to \"stranger\"", provider.Requests[1].Prompt.History[0].Text);
    }

    private sealed class StepsComparer : IEqualityComparer<IEnumerable<AdventureTrigger>>
    {
        public bool Equals(IEnumerable<AdventureTrigger>? x, IEnumerable<AdventureTrigger>? y) =>
            x is not null && y is not null && x.Select(Plain).SequenceEqual(y.Select(Plain));

        public int GetHashCode(IEnumerable<AdventureTrigger> obj) => 0;

        // The task is the model's words; everything else on a step is the plan's.
        private static AdventureTrigger Plain(AdventureTrigger trigger) => trigger with { Task = null };
    }
}
