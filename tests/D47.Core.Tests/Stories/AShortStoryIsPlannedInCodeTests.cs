using System.Text.Json.Nodes;
using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Xunit;

namespace D47.Core.Tests.Stories;

/// <summary>A short story's objectives are drawn and planned in code from the Commander's career and own words; the model writes only the words.</summary>
public sealed class AShortStoryIsPlannedInCodeTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Fields through which a call could name a place, a kind of objective or a count.</summary>
    private static readonly string[] Placing = ["kind", "system", "station", "body", "count", "faction", "commodity", "mission", "filter", "beats", "replacements"];

    private const string NoBrief = """{"home": false, "system": null, "activities": [], "ship": null, "changes": false}""";

    /// <summary>A galaxy around Fixture Prime with enough stations and bodies within a session's reach for a long short story's stops.</summary>
    private static PlannedChapterWorld.Galaxy Roomy()
    {
        var galaxy = new PlannedChapterWorld.Galaxy();

        for (var index = 0; index < 6; index++)
        {
            galaxy.MoreStations.Add(new StationSummary
            {
                Name = $"Roomy Port {index}", SystemName = $"Roomy {index}", SystemAddress = 900 + index, MarketId = 9000 + index,
                Position = new StarPosition(130 + (index * 10), 0, 0), Type = "Coriolis Starport", LargePads = 2,
            });
            galaxy.MoreBodies.Add(new BodySummary
            {
                Name = $"Roomy Rock {index} 1", SystemName = $"Roomy Rock {index}", SystemAddress = 950 + index, BodyId = 1,
                Position = new StarPosition(130 + (index * 10), 10, 0), IsLandable = true,
            });
        }

        return galaxy;
    }

    /// <summary>A short story's lines answer for up to eight objectives, each line a plain string.</summary>
    private static string ShortLines() => PlannedChapterWorld.Lines(
        ChapterPlanner.MostObjectives,
        (number, objective) => objective["lines"] = new JsonArray(JsonValue.Create($"Page {number} of the ledger is open.")),
        answer => answer["opening"] = "A ledger sits in the hold.");

    private static RoundScriptedLlmProvider Answering(params string[] answers) =>
        new([.. answers.Select(answer => RoundScriptedLlmProvider.Saying(answer))]);

    /// <summary>The calls that write the story's words; the fidelity and plain checks only read them.</summary>
    private static IEnumerable<D47.Core.Conversation.LlmRequest> Writing(RoundScriptedLlmProvider provider) =>
        provider.Requests.Where(request => request.ResponseSchema?.Name is "story_lines" or "story_line_repairs" or "story_spine" or "story_beats" or "story_replacements");

    private static IReadOnlyList<AdventureTrigger> Triggers(Adventure adventure) => [.. adventure.Beats.SelectMany(beat => beat.Steps).Select(step => step.Trigger)];

    [Theory]
    [InlineData(AdventureLength.Short, 3, 4)]
    [InlineData(AdventureLength.Evening, 4, 6)]
    [InlineData(AdventureLength.Long, 6, 8)]
    public async Task EachLengthHoldsItsCountOfObjectivesWithItsLabels(AdventureLength length, int fewest, int most)
    {
        var generator = PlannedChapterWorld.Generator(null, Roomy());
        var counts = new HashSet<int>();

        Assert.Equal((fewest, most), AdventureGenerator.ShortObjectives(length));

        for (var seed = 1u; seed <= 16; seed++)
        {
            var ask = new AdventureAsk(AdventureReach.Session, length);
            var planned = await generator.ShortPlanAsync(ask, AdventureGenerator.Facts.Of(PlannedChapterWorld.State(), ask), AdventureGenerator.ShortWants.None, seed, TestContext.Current.CancellationToken);

            Assert.True(planned.Succeeded, planned.Refusal?.Text);

            var functions = planned.Plan!.Objectives.Select(objective => objective.Function).ToList();

            Assert.InRange(functions.Count, fewest, most);
            Assert.Equal(length == AdventureLength.Long ? ChapterFunctions.Long(functions.Count) : ChapterFunctions.For(functions.Count, StoryStage.FunAndGames), functions);
            counts.Add(functions.Count);
        }

        Assert.True(counts.Count > 1, $"Every seed gave {string.Join(", ", counts)} objectives.");
    }

    [Fact]
    public void ALongShortStoryAddsAnOpeningAHesitationBeforeTheTurnAndAClosing()
    {
        Assert.Equal(["opening", "setup", "hesitation", "turn", "resolution", "closing"], ChapterFunctions.Long(6));
        Assert.Equal(["opening", "setup", "complication", "hesitation", "turn", "resolution", "closing"], ChapterFunctions.Long(7));
        Assert.Equal(["opening", "setup", "complication", "hesitation", "turn", "setback", "resolution", "closing"], ChapterFunctions.Long(8));
    }

    [Fact]
    public async Task AShortStoryWithNoBriefIsWrittenWithNoCallThatPlacesAnything()
    {
        var provider = Answering(ShortLines(), PlannedChapterWorld.NoObjections);
        var generator = PlannedChapterWorld.Generator(provider, Roomy());

        var outcome = await generator.GenerateAsync(new AdventureAsk(AdventureReach.Session, AdventureLength.Evening), Now, TestContext.Current.CancellationToken);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal("story_lines", provider.Requests[0].ResponseSchema?.Name);
        Assert.DoesNotContain(provider.Requests, request => request.ResponseSchema?.Name == "story_brief");
        Assert.All(Writing(provider), request => Assert.DoesNotContain(PlannedChapterWorld.Properties(request.ResponseSchema!.SchemaJson), Placing.Contains));
        Assert.InRange(outcome.Draft!.Beats.Count, 4, 6);
        Assert.Null(outcome.Draft.StoryId);
        Assert.Equal("A ledger sits in the hold.", Assert.Single(outcome.Draft.OpeningLines).Text);
    }

    [Fact]
    public async Task TheBriefIsReadIntoAPlaceAnActivityAndAShipThatCodePlans()
    {
        const string read = """{"home": false, "system": "Fixture Second", "activities": [{"activity": "bounty", "count": 7}], "ship": "Cobra Mk III"}""";
        const string brief = "A grim bounty hunt, seven of them, flown in a Cobra, ending in Fixture Second.";
        var provider = Answering(read, ShortLines(), PlannedChapterWorld.NoObjections);
        var generator = PlannedChapterWorld.Generator(provider, Roomy());

        var outcome = await generator.GenerateAsync(new AdventureAsk(AdventureReach.Session, AdventureLength.Evening, Brief: brief), Now, TestContext.Current.CancellationToken);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal("story_brief", provider.Requests[0].ResponseSchema?.Name);
        Assert.Contains(brief, provider.Requests[0].Prompt.History[0].Text);
        Assert.Equal("story_lines", provider.Requests[1].ResponseSchema?.Name);
        Assert.Contains(brief, provider.Requests[1].Prompt.History[0].Text);
        Assert.All(Writing(provider), request => Assert.DoesNotContain(PlannedChapterWorld.Properties(request.ResponseSchema!.SchemaJson), Placing.Contains));

        var triggers = Triggers(outcome.Draft!);

        Assert.Equal(new AdventureTrigger { Kind = TriggerKind.Board, ShipType = "cobramkiii" }, triggers[0]);
        Assert.Contains(triggers, trigger => trigger is { Kind: TriggerKind.Bounty, Count: 7 });
        Assert.Equal(("Fixture Second", TriggerKind.Arrive), (triggers[^1].System, triggers[^1].Kind));
        Assert.InRange(outcome.Draft!.Beats.Count, 4, 6);
    }

    [Fact]
    public async Task AShipIsNotBoardedWhenTheStoryStaysInThisShip()
    {
        const string read = """{"home": false, "system": null, "activities": [], "ship": "Cobra Mk III"}""";
        var provider = Answering(read, ShortLines(), PlannedChapterWorld.NoObjections);
        var generator = PlannedChapterWorld.Generator(provider, Roomy());

        var outcome = await generator.GenerateAsync(new AdventureAsk(AdventureReach.Session, AdventureLength.Short, ThisShipOnly: true, Brief: "In a Cobra."), Now, TestContext.Current.CancellationToken);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.DoesNotContain(Triggers(outcome.Draft!), trigger => trigger.Kind == TriggerKind.Board);
    }

    [Fact]
    public async Task ARemarkWithNoConstraintsKeepsEveryTrigger()
    {
        var provider = Answering(ShortLines(), PlannedChapterWorld.NoObjections, NoBrief, ShortLines(), PlannedChapterWorld.NoObjections);
        var generator = PlannedChapterWorld.Generator(provider, Roomy());
        var ask = new AdventureAsk(AdventureReach.Session, AdventureLength.Evening);
        var draft = (await generator.GenerateAsync(ask, Now, TestContext.Current.CancellationToken)).Draft!;
        var before = provider.Requests.Count;

        var revised = await generator.ReviseAsync(draft, ask, [], "Make it darker.", Now, TestContext.Current.CancellationToken);

        Assert.True(revised.Succeeded, revised.Refusal);
        Assert.Equal(Triggers(draft), Triggers(revised.Draft!));
        Assert.Equal(draft.Beats.Select(beat => beat.Function), revised.Draft!.Beats.Select(beat => beat.Function));
        Assert.Equal("story_brief", provider.Requests[before].ResponseSchema?.Name);
        Assert.Contains("Make it darker.", provider.Requests[before + 1].Prompt.History[0].Text);
        Assert.Contains("the objectives stay as they are", provider.Requests[before + 1].Prompt.History[0].Text);
    }

    [Fact]
    public async Task ARemarkAskingForAnActivityIsPlannedAgain()
    {
        const string read = """{"home": false, "system": null, "activities": [{"activity": "bounty", "count": 9}], "ship": null, "changes": true}""";
        var provider = Answering(ShortLines(), PlannedChapterWorld.NoObjections, read, ShortLines(), PlannedChapterWorld.NoObjections);
        var generator = PlannedChapterWorld.Generator(provider, Roomy());
        var ask = new AdventureAsk(AdventureReach.Session, AdventureLength.Evening);
        var draft = (await generator.GenerateAsync(ask, Now, TestContext.Current.CancellationToken)).Draft!;

        Assert.DoesNotContain(Triggers(draft), trigger => trigger is { Kind: TriggerKind.Bounty, Count: 9 });

        var revised = await generator.ReviseAsync(draft, ask, [], "Make it a bounty hunt, nine of them.", Now, TestContext.Current.CancellationToken);

        Assert.True(revised.Succeeded, revised.Refusal);
        Assert.Contains(Triggers(revised.Draft!), trigger => trigger is { Kind: TriggerKind.Bounty, Count: 9 });
        Assert.InRange(revised.Draft!.Beats.Count, 4, 6);
    }

    [Fact]
    public async Task ARemarkThatDropsAnActivityPlansTheStoryAgainWithoutIt()
    {
        const string bounties = """{"home": false, "system": null, "activities": [{"activity": "bounty", "count": 5}], "ship": null}""";
        const string drop = """{"home": false, "system": null, "activities": [], "ship": null, "leaveOut": ["bounty"], "changes": true}""";
        var provider = Answering(bounties, ShortLines(), PlannedChapterWorld.NoObjections, drop, ShortLines(), PlannedChapterWorld.NoObjections);
        var generator = PlannedChapterWorld.Generator(provider, Roomy());
        var ask = new AdventureAsk(AdventureReach.Session, AdventureLength.Evening, Brief: "Some bounty hunting.");
        var draft = (await generator.GenerateAsync(ask, Now, TestContext.Current.CancellationToken)).Draft!;
        var before = provider.Requests.Count;

        Assert.Contains(Triggers(draft), trigger => trigger.Kind == TriggerKind.Bounty);

        var revised = await generator.ReviseAsync(draft, ask, [], "No bounties, please.", Now, TestContext.Current.CancellationToken);

        Assert.True(revised.Succeeded, revised.Refusal);
        Assert.DoesNotContain(Triggers(revised.Draft!), trigger => trigger.Kind == TriggerKind.Bounty);
        Assert.Contains("\"leaveOut\"", provider.Requests[before].ResponseSchema!.SchemaJson);
        Assert.DoesNotContain("the objectives stay as they are", provider.Requests[before + 1].Prompt.History[0].Text);
        Assert.Contains("d47 built new objectives from what they asked for", provider.Requests[before + 1].Prompt.History[0].Text);
    }

    [Fact]
    public async Task ABarredActivityIsNotOfferedToTheBriefOrDrawnWhenADraftIsPlannedAgain()
    {
        const string read = """{"home": false, "system": "Fixture Second", "activities": [], "ship": null, "changes": true}""";
        var provider = Answering(ShortLines(), PlannedChapterWorld.NoObjections, read, ShortLines(), PlannedChapterWorld.NoObjections);
        var generator = PlannedChapterWorld.Generator(provider, Roomy());
        var ask = new AdventureAsk(AdventureReach.Session, AdventureLength.Evening);
        var draft = (await generator.GenerateAsync(ask, Now, TestContext.Current.CancellationToken)).Draft!;
        var before = provider.Requests.Count;

        generator.Barred = () => [RefusedActivities.Key(TriggerKind.Bounty, null)!];

        var revised = await generator.ReviseAsync(draft, ask, [], "End it in Fixture Second.", Now, TestContext.Current.CancellationToken);

        Assert.True(revised.Succeeded, revised.Refusal);
        Assert.Contains("- salvage:", provider.Requests[before].Prompt.History[0].Text);
        Assert.DoesNotContain("- bounty:", provider.Requests[before].Prompt.History[0].Text);
        Assert.DoesNotContain(Triggers(revised.Draft!), trigger => trigger.Kind == TriggerKind.Bounty);
        Assert.Equal("Fixture Second", Triggers(revised.Draft!)[^1].System);
    }

    [Fact]
    public async Task ASecondDraftWritesItsWordsAgainstTheFirstDraftsObjectives()
    {
        var provider = Answering("I cannot write that.", ShortLines(), PlannedChapterWorld.NoObjections);
        var generator = PlannedChapterWorld.Generator(provider, Roomy());

        var outcome = await generator.DraftAsync(new AdventureAsk(AdventureReach.Session, AdventureLength.Long), Now, null, TestContext.Current.CancellationToken);

        Assert.True(outcome.Succeeded, outcome.Refusal);

        var lines = provider.Requests.Where(request => request.ResponseSchema?.Name == "story_lines").Select(request => Objectives(request.Prompt.History[0].Text)).ToList();

        Assert.Equal(2, lines.Count);
        Assert.Equal(lines[0], lines[1]);

        static string Objectives(string prompt) =>
            prompt[prompt.IndexOf("The chapter's objectives, in order", StringComparison.Ordinal)..prompt.IndexOf("The functions:", StringComparison.Ordinal)];
    }
}
