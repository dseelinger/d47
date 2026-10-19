using System.Text.Json;
using D47.Core.Adventures;
using D47.Core.Stories;
using D47.Core.Tests.Adventures;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;
using static D47.Core.Tests.Stories.StoryFixtures;
using static D47.Core.Tests.Stories.TheFinaleEndsWhereItsFirstChapterSaidTests;

namespace D47.Core.Tests.Stories;

/// <summary>A chapter after the beacon scan is written around three activities drawn before its spine, and the resolve step holds it to them.</summary>
public sealed class ChaptersAreWrittenAroundDrawnActivitiesTests
{
    private const string SpineText = """
        {"name": "The Long Way", "premise": "A debt.", "want": "To pay it.", "stake": "Whether it can be.", "turn": "It is owed.", "ending": "Forgiven."}
        """;

    private static readonly DrawnActivity Bounties = new(TriggerKind.Bounty, null, 5);

    private static readonly DrawnActivity Courier = new(TriggerKind.Mission, "Mission_Courier", 3);

    private static readonly DrawnActivity Signals = new(TriggerKind.Signal, null, 2);

    private static readonly DrawnActivity[] Three = [Bounties, Courier, Signals];

    private static AdventureAsk Chapter(IReadOnlyList<DrawnActivity>? slots, AdventureChapter? previous = null) =>
        new(Chapter: previous, Story: new AdventureStory("s", "S", "public", "hidden", 3, 40, 20, Slots: slots));

    private static async Task<(AdventureOutcome Outcome, RoundScriptedLlmProvider Provider)> Written(AdventureAsk ask, params string[] answers)
    {
        var provider = new RoundScriptedLlmProvider([.. answers.Select(answer => RoundScriptedLlmProvider.Saying(answer))]);
        var outcome = await AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy())
            .GenerateAsync(ask, Now, TestContext.Current.CancellationToken);

        return (outcome, provider);
    }

    private static string PromptOf(RoundScriptedLlmProvider provider, int round) => provider.Requests[round].Prompt.History[0].Text;

    private static IReadOnlyList<string> KindsOf(RoundScriptedLlmProvider provider, int round) =>
        JsonDocument.Parse(provider.Requests[round].ResponseSchema!.SchemaJson).RootElement
            .GetProperty("properties").GetProperty("beats").GetProperty("items").GetProperty("properties").GetProperty("kind").GetProperty("enum")
            .EnumerateArray().Select(kind => kind.GetString()!).ToList();

    [Fact]
    public async Task TheSpineAndTheObjectivesNameTheSameDrawnActivities()
    {
        var (outcome, provider) = await Written(Chapter(Three), SpineText, BeatsMeeting(Three));

        Assert.True(outcome.Succeeded, outcome.Refusal);

        foreach (var round in new[] { 0, 1 })
        {
            var prompt = PromptOf(provider, round);

            Assert.Contains("a \"bounty\" objective, count 5", prompt);
            Assert.Contains("a \"mission\" objective whose family is exactly Mission_Courier, count 3", prompt);
            Assert.Contains("a \"signal\" objective, count 2", prompt);
        }

        Assert.Contains("Its want and its turn are built on them", PromptOf(provider, 0));
    }

    [Fact]
    public async Task TheObjectivesSchemaHasNoCountedKindOutsideTheSlots()
    {
        var (_, provider) = await Written(Chapter(Three), SpineText, BeatsMeeting(Three));
        string[] placesAndMilestones = ["arrive", "dock", "land", "scan", "rank", "board", "engineer", "crew", "livery", "carrierbuy", "carrierjump", "wing", "multicrew", "squadron", "squadronfound"];

        var kinds = KindsOf(provider, 1);

        Assert.Equal(["bounty", "mission", "signal"], kinds.Except(placesAndMilestones).Order());
        Assert.Contains("engineer", kinds);
        Assert.Contains("crew", kinds);
        Assert.Contains("livery", kinds);
        Assert.DoesNotContain("onfoot", PromptOf(provider, 1).Split('\n').Where(line => line.StartsWith("- \"", StringComparison.Ordinal)).Select(line => line.Split('"')[1]));
    }

    [Fact]
    public async Task ADraftMissingASlotGetsARepairRoundNamingItAndFailsIfItStaysMissing()
    {
        var without = BeatsMeeting([Bounties, Signals]);
        var (outcome, provider) = await Written(Chapter(Three), SpineText, without, Repairs.Replacing(without, 2), Repairs.Replacing(without, 2));

        Assert.False(outcome.Succeeded);
        Assert.Contains("has no objective for this one: ", PromptOf(provider, 2));
        Assert.Contains("a \"mission\" objective whose family is exactly Mission_Courier, count 3", PromptOf(provider, 2));
        Assert.Contains("has no objective for this one: ", outcome.Refusal);
    }

    [Fact]
    public async Task AMetSlotsObjectiveCarriesTheDrawnCount()
    {
        var wrongCounts = BeatsMeeting(Three.Select(slot => slot with { Count = 9 }));
        var (outcome, _) = await Written(Chapter(Three), SpineText, wrongCounts);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal([5, 3, 2], outcome.Draft!.Beats.Where(beat => beat.Trigger.Kind is TriggerKind.Bounty or TriggerKind.Mission or TriggerKind.Signal).Select(beat => beat.Trigger.Count));
    }

    [Fact]
    public async Task OneObjectiveMeetsOnlyOneSlot()
    {
        var twoBounties = BeatsMeeting([Bounties]);
        var (outcome, _) = await Written(Chapter([Bounties, Bounties with { Count = 7 }]), SpineText, twoBounties, Repairs.Replacing(twoBounties, 2), Repairs.Replacing(twoBounties, 2));

        Assert.False(outcome.Succeeded);
        Assert.Contains("has no objective for this one", outcome.Refusal);
    }

    [Fact]
    public async Task ALastFinaleChapterWhoseDrawIncludesAnIllegalFamilyIsWrittenAndPasses()
    {
        var heist = new DrawnActivity(TriggerKind.Mission, "Mission_OnFoot_Heist", 2) { PlaceBefore = true };
        var ask = Ask(finale: 4, destination: new AdventureDestination(Galaxy.Address("Edge Moon"), "Edge Moon", 1, "Edge Moon 1 a"));
        ask = ask with { Story = ask.Story! with { Slots = [heist] } };
        var beats = $$"""
            {"opening": "Go.", "reply": "Here.", "beats": [
              {{Arrive("Kestrel")}},
              {"title": "The Heist", "function": "turn", "kind": "mission", "count": 2, "mission": "Mission_OnFoot_Heist", "line": "Do it."},
              {{Land("Edge Moon", "Edge Moon 1 a")}}
            ]}
            """;

        var outcome = await Write(ask, beats);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal(TriggerKind.Land, outcome.Draft!.Beats[^1].Trigger.Kind);
    }

    [Fact]
    public async Task AChapterWithNoDrawnActivitiesIsWrittenAsBefore()
    {
        var previous = new Adventure
        {
            Key = "before",
            Name = "Before",
            Source = AdventureSource.Generated,
            Written = Now,
            Beats = [Beat("Blood", "setup", new AdventureTrigger { Kind = TriggerKind.Bounty, Count = 2 }, "Two.")],
        };
        var chapter = new AdventureChapter(previous, []);

        var (_, plain) = await Written(Chapter(null, chapter), SpineText);
        var (_, drawn) = await Written(Chapter(Three, chapter), SpineText, BeatsMeeting(Three));

        Assert.Contains("Vary what the Commander does", PromptOf(plain, 0));
        Assert.DoesNotContain("drawn for it by d47", PromptOf(plain, 0));
        Assert.DoesNotContain("Vary what the Commander does", PromptOf(drawn, 0));
        Assert.DoesNotContain("Vary what the Commander does", PromptOf(drawn, 1));
        Assert.Contains("The counted kinds, in no order of preference", PromptOf(plain, 1));
        Assert.DoesNotContain("in no order of preference", PromptOf(drawn, 1));
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task ARefusedSlotsObjectiveIsDrawnAgainForThatSlotOnly()
    {
        const string Key = "the-hard-way";

        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(NextBeats)));
        fixtures.WritesDrawnChapters = true;
        fixtures.Stories.Save("F1", new Story
        {
            Id = Id,
            Title = Card.Title,
            PublicLayer = Card.Describe(),
            PickedAt = Now.AddDays(-30),
            BeaconScanAt = Now.AddDays(-20),
            Chapters = ["chapter-one", Key],
            CluesGiven = 2,
        });

        Assert.Null(fixtures.Book.Write("F1", new Adventure
        {
            Key = Key,
            Name = "The Hard Way",
            Source = AdventureSource.Generated,
            Written = Now,
            Spine = new AdventureSpine { Premise = "A debt.", Want = "To pay it.", Stake = "Whether it can be.", Turn = "It is owed.", Ending = "Forgiven." },
            Opening = "Go.",
            StoryId = Id,
            Beats =
            [
                Beat("The Lantern", "setup", new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = Lantern, System = "Ossen's Lantern" }, "Scoop here."),
                Beat("First Blood", "catalyst", new AdventureTrigger { Kind = TriggerKind.Bounty, Count = 1 }, "One."),
                Beat("The Wing", "midpoint", new AdventureTrigger { Kind = TriggerKind.Bond, Count = 3 }, "Three bonds."),
                Beat("Home", "finale", new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = Hollow, System = "Dyson's Hollow" }, "End."),
            ],
        }));
        Assert.Null(fixtures.Book.Begin("F1", Key, Now));
        fixtures.Book.Observe(Jump(Lantern, Now.AddMinutes(1)), "F1");
        fixtures.Book.Observe(AdventureFixtures.Event($$"""{ "timestamp":"{{Stamp(Now.AddMinutes(2))}}", "event":"Bounty", "TotalReward":100 }"""), "F1");
        fixtures.Director.Clock = () => Now.AddHours(1);

        await fixtures.Director.RefuseBeatAsync("F1", TestContext.Current.CancellationToken);

        var slots = Assert.Single(fixtures.Asks).Story!.Slots!;

        Assert.Equal(2, slots.Count);
        Assert.Equal(new DrawnActivity(TriggerKind.Bounty, null, 1), slots[0]);
        Assert.NotEqual(TriggerKind.Bond, slots[1].Kind);
        Assert.NotEqual(TriggerKind.Bounty, slots[1].Kind);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task AChapterBeforeTheScanIsDrawnNothing()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(StoryFixtures.Spine), RoundScriptedLlmProvider.Saying(BeatsToTheBeacon)));
        fixtures.Director.Game = () => Loaded(1_000_000);

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        Assert.Null(Assert.Single(fixtures.Asks).Story!.Slots);
    }
}
