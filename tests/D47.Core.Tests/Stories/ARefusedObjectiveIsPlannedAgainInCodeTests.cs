using D47.Core.Adventures;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Conversation.RoundScriptedLlmProvider;

namespace D47.Core.Tests.Stories;

/// <summary>
/// Never ask this on a story chapter: the planner builds the objectives from the refused one to the end, and the model writes only
/// their words, in one lines call that the fidelity and plain checks read.
/// </summary>
public sealed class ARefusedObjectiveIsPlannedAgainInCodeTests
{
    private static Adventure Chapter() => new()
    {
        Key = "the-ledger",
        Name = "The Ledger",
        Source = AdventureSource.Generated,
        StoryId = PlannedChapterWorld.Story.Id,
        Spine = new AdventureSpine { Premise = "A ledger.", Want = "To read it.", Stake = "Trust.", Turn = "It is forged.", Ending = "It burns." },
        Beats =
        [
            Beat("The Start", "setup", new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = 2, System = "Fixture Alpha" }),
            Beat("The Hunt", "complication", new AdventureTrigger { Kind = TriggerKind.Bounty, Count = 5 }),
            Beat("The Codex", "turn", new AdventureTrigger { Kind = TriggerKind.Codex, Count = 4 }),
            Beat("The Salvage", "setback", new AdventureTrigger { Kind = TriggerKind.Salvage, Count = 3 }),
            Beat("The End", "resolution", new AdventureTrigger { Kind = TriggerKind.Land, SystemAddress = 3, BodyId = 2, System = "Fixture Beta", Body = "Fixture Beta 2" }),
        ],
    };

    private static AdventureBeat Beat(string title, string function, AdventureTrigger trigger) =>
        new() { Title = title, Function = function, Steps = [new BeatStep { Trigger = trigger }], Lines = [new AdventureLine { Text = "A line.", Speaker = "ship" }] };

    /// <summary>The bounty refused: the story keeps the codex and salvage, as the director gives them, and has refused bounties.</summary>
    private static AdventureAsk Refusing() =>
        new(
            AdventureReach.Session,
            Story: PlannedChapterWorld.Story with { Slots = [PlannedChapterWorld.Codex, PlannedChapterWorld.Salvage], Refused = ["bounty"] },
            Rewrite: new AdventureRewrite(Chapter(), 1));

    [Fact]
    public async Task TheNewObjectivesAreThePlannersAndTheModelWritesOnlyTheirWords()
    {
        var ask = Refusing();
        var provider = new RoundScriptedLlmProvider(Saying(PlannedChapterWorld.NewWords()), Saying(PlannedChapterWorld.NoObjections));
        var generator = PlannedChapterWorld.Generator(provider);
        var places = await generator.RewritePlacesAsync(ask, TestContext.Current.CancellationToken);
        var expected = ChapterPlanner.Plan(AdventureGenerator.RewriteInputs(PlannedChapterWorld.State(), ask, null), places, AdventureGenerator.RewriteSeed(ask.Rewrite!)).Plan!;

        var outcome = await generator.GenerateAsync(ask, DateTimeOffset.UnixEpoch, TestContext.Current.CancellationToken);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal(["story_line_repairs", "story_fidelity", "story_plain"], provider.Requests.Select(request => request.ResponseSchema?.Name));

        var beats = outcome.Draft!.Beats;
        Assert.Equal("The Start", beats[0].Title);
        Assert.Equal(
            expected.Objectives.SelectMany(objective => objective.Steps).Select(Describe),
            beats.Skip(1).SelectMany(beat => beat.Steps).Select(step => Describe(step.Trigger)));
        Assert.Equal(expected.Objectives.Select(objective => objective.Function), beats.Skip(1).Select(beat => beat.Function));
        Assert.Equal(ChapterFunctions.Relabel(["complication", "turn", "setback", "resolution"], expected.Objectives.Count), beats.Skip(1).Select(beat => beat.Function));
        Assert.Equal("New One", beats[1].Title);
        Assert.Equal("The Ledger", outcome.Draft.Name);
    }

    [Fact]
    public async Task ARefusedActivityIsNotPlannedAgainAndTheRestAre()
    {
        var provider = new RoundScriptedLlmProvider(Saying(PlannedChapterWorld.NewWords()), Saying(PlannedChapterWorld.NoObjections));

        var outcome = await PlannedChapterWorld.Generator(provider).GenerateAsync(Refusing(), DateTimeOffset.UnixEpoch, TestContext.Current.CancellationToken);

        var steps = outcome.Draft!.Beats.Skip(1).SelectMany(beat => beat.Steps).Select(step => step.Trigger).ToList();

        Assert.DoesNotContain(steps, step => step.Kind == TriggerKind.Bounty);
        Assert.Equal(4, Assert.Single(steps, step => step.Kind == TriggerKind.Codex).Count);
        Assert.Equal(3, Assert.Single(steps, step => step.Kind == TriggerKind.Salvage).Count);
    }

    [Fact]
    public async Task TheLinesCallHasNoFieldForAPlaceAKindOrACount()
    {
        var provider = new RoundScriptedLlmProvider(Saying(PlannedChapterWorld.NewWords()), Saying(PlannedChapterWorld.NoObjections));

        await PlannedChapterWorld.Generator(provider).GenerateAsync(Refusing(), DateTimeOffset.UnixEpoch, TestContext.Current.CancellationToken);

        var properties = PlannedChapterWorld.Properties(provider.Requests[0].ResponseSchema!.SchemaJson).ToHashSet();

        Assert.DoesNotContain("kind", properties);
        Assert.DoesNotContain("system", properties);
        Assert.DoesNotContain("station", properties);
        Assert.DoesNotContain("count", properties);
        Assert.DoesNotContain("name", properties);
        Assert.DoesNotContain("opening", properties);
    }

    private static string Describe(AdventureTrigger trigger) =>
        $"{trigger.Kind} {trigger.System} {trigger.Station}{trigger.Body} {trigger.MarketId} {trigger.Count}";
}
