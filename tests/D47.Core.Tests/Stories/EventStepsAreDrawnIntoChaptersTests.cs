using System.Text.Json;
using System.Text.Json.Nodes;
using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Stories;
using D47.Core.Tests.Adventures;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>A chapter's last slot is sometimes an event step drawn by d47, and the writer is told only that slot.</summary>
public sealed class EventStepsAreDrawnIntoChaptersTests
{
    private const int Seeds = 1000;

    private const string SpineText = """
        {"name": "The Long Way", "premise": "A debt.", "want": "To pay it.", "stake": "Whether it can be.", "turn": "It is owed.", "ending": "Forgiven."}
        """;

    private const string EventRows = "journal\tDocked\tyes\t\t\tThe ship docked.\n"
        + "journal\tAsteroidCracked\tyes\tMiningKit\tMining\tA motherlode asteroid was broken up.\n"
        + "journal\tBounty\tyes\tDanger\tCombat\tA bounty was awarded.\n"
        + "d47\tToolRun\tyes\t\t\tA tool of the assistant ran.\n";

    private const string FieldRows = "Docked\tStationType\tstring\t\tyes\n"
        + "Docked\tStationType\tstring\tOrbis\tyes\n"
        + "Docked\tStationType\tstring\tOutpost\tno\n"
        + "ToolRun\tTool\tstring\tadd_log_entry\tyes\n"
        + "ToolRun\tTool\tstring\tset_volume\tyes\n"
        + "ToolRun\tTool\tstring\taccept_proposal\tno\n";

    private static readonly StepEventCatalogue Catalogue = StepEventCatalogue.Parse(EventRows, FieldRows);

    private static readonly DrawableTool[] Tools =
    [
        new("add_log_entry", "log this"),
        new("set_volume", "set volume to forty percent"),
        new("accept_proposal", "accept it"),
    ];

    private static ShipLoadout Ship() => new() { Type = "cobramkiii", CargoCapacity = 64, Modules = [] };

    private static IReadOnlyList<DrawnActivity> Draw(
        uint seed,
        StepEventCatalogue? events = null,
        IReadOnlyList<string>? refused = null,
        IReadOnlyList<DrawableTool>? tools = null,
        ShipLoadout? ship = null) =>
        ChapterDraw.Draw(
            CareerWeights.Even, RankState.Empty, ship ?? Ship(), odyssey: true, [], refused, comfort: false, CareerStatistics.Empty, 0, seed,
            events: events, tools: tools);

    private static IEnumerable<DrawnActivity> Events(Func<uint, IReadOnlyList<DrawnActivity>> draw) =>
        Enumerable.Range(0, Seeds).SelectMany(seed => draw((uint)seed)).Where(slot => slot.Kind == TriggerKind.Event);

    [Fact]
    public void AChapterHoldsAtMostOneEventSlotAndOtherChaptersDrawAsBefore()
    {
        var withEvent = 0;

        for (uint seed = 0; seed < Seeds; seed++)
        {
            var drawn = Draw(seed, Catalogue, tools: Tools);
            var before = Draw(seed);

            Assert.True(drawn.Count(slot => slot.Kind == TriggerKind.Event) <= 1);
            Assert.Equal(ChapterDraw.Slots, drawn.Count);

            if (drawn.Any(slot => slot.Kind == TriggerKind.Event))
            {
                withEvent++;
                Assert.Equal(TriggerKind.Event, drawn[^1].Kind);
                Assert.Equal(before.Take(ChapterDraw.Slots - 1), drawn.Take(ChapterDraw.Slots - 1));
            }
            else
            {
                Assert.Equal(before, drawn);
            }
        }

        Assert.InRange(withEvent, Seeds / 5, Seeds / 2);
    }

    [Fact]
    public void ADrawWithNoCatalogueDrawsNoEvent() =>
        Assert.Empty(Events(seed => Draw(seed)));

    [Fact]
    public void AnEventSlotNamesADrawableEventWhoseGatesAreHeld()
    {
        var slots = Events(seed => Draw(seed, Catalogue, tools: Tools)).ToList();

        Assert.NotEmpty(slots);
        Assert.All(slots, slot => Assert.Contains(slot.Event, new[] { "Docked", "ToolRun" }));
        Assert.Contains(slots, slot => slot.Event == "Docked");
        Assert.Contains(slots, slot => slot.Event == "ToolRun");
    }

    [Fact]
    public void AnEventWhoseGatesAreNotHeldIsNeverDrawn()
    {
        Assert.DoesNotContain(Events(seed => Draw(seed, Catalogue, tools: Tools)), slot => slot.Event is "AsteroidCracked" or "Bounty");

        var mining = new ShipLoadout
        {
            Type = "cobramkiii",
            CargoCapacity = 64,
            Modules = [new ShipModule("Slot", "hpt_mininglaser_fixed_small", true, 100, 1), new ShipModule("Slot", "int_refinery_size2_class1", true, 100, 1)],
        };

        Assert.Contains(Events(seed => Draw(seed, Catalogue, tools: Tools, ship: mining)), slot => slot.Event == "AsteroidCracked");
    }

    [Fact]
    public void ARefusedEventOrToolIsNeverDrawnAgain()
    {
        Assert.DoesNotContain(Events(seed => Draw(seed, Catalogue, ["event:Docked"], Tools)), slot => slot.Event == "Docked");

        var slots = Events(seed => Draw(seed, Catalogue, ["event:ToolRun:add_log_entry"], Tools)).Where(slot => slot.Event == "ToolRun").ToList();

        Assert.NotEmpty(slots);
        Assert.DoesNotContain(slots, slot => RefusedActivities.ToolOf(slot.Where) == "add_log_entry");
    }

    [Fact]
    public void AToolRunSlotNamesADrawableToolThatHasACommandPhrase()
    {
        var slots = Events(seed => Draw(seed, Catalogue, tools: Tools)).Where(slot => slot.Event == "ToolRun").ToList();

        Assert.All(slots, slot => Assert.Equal(1, slot.Count));
        Assert.All(slots, slot => Assert.Contains(RefusedActivities.ToolOf(slot.Where), new[] { "add_log_entry", "set_volume" }));
        Assert.All(slots, slot => Assert.Equal(Tools.First(tool => tool.Name == RefusedActivities.ToolOf(slot.Where)).Phrase, slot.Say));
        Assert.DoesNotContain(Events(seed => Draw(seed, Catalogue, tools: [])), slot => slot.Event == "ToolRun");
    }

    [Fact]
    public void AJournalEventSlotCountsOneToThreeWithAtMostOneDrawableFieldValue()
    {
        var slots = Events(seed => Draw(seed, Catalogue, tools: Tools)).Where(slot => slot.Event == "Docked").ToList();

        Assert.All(slots, slot => Assert.InRange(slot.Count, 1, ChapterDraw.MostEventCount));
        Assert.All(slots, slot => Assert.Equal([new EventField("StationType", "Orbis")], slot.Where));
    }

    [Fact]
    public void ARefusedEventKeyNamesTheEventAndTheTool()
    {
        Assert.Equal("event:Docked", RefusedActivities.Key(TriggerKind.Event, null, "Docked"));
        Assert.Equal("event:ToolRun:add_log_entry", RefusedActivities.Key(TriggerKind.Event, null, "ToolRun", "add_log_entry"));
        Assert.True(RefusedActivities.Refuses(["event:Docked"], TriggerKind.Event, null, "Docked"));
        Assert.False(RefusedActivities.Refuses(["event:Docked"], TriggerKind.Event, null, "Undocked"));
        Assert.False(RefusedActivities.Refuses(["event:ToolRun:add_log_entry"], TriggerKind.Event, null, "ToolRun", "set_volume"));
    }

    private static readonly DrawnActivity Docking = new(TriggerKind.Event, null, 2) { Event = "Docked" };

    private static readonly DrawnActivity Logging = new(TriggerKind.Event, null, 1)
    {
        Event = D47Event.ToolRun,
        Where = new EventFilter([new EventField("Tool", "add_log_entry")]),
        Say = "log this",
    };

    private static readonly DrawnActivity Bounties = new(TriggerKind.Bounty, null, 5);

    private static AdventureAsk Chapter(IReadOnlyList<DrawnActivity>? slots) =>
        new(Story: new AdventureStory("s", "S", "public", "hidden", 3, 40, 20, Slots: slots));

    private static string BeatsWith(string? task, params DrawnActivity[] others)
    {
        var beats = new JsonArray
        {
            new JsonObject
            {
                ["title"] = "The Lantern", ["function"] = "setup", ["kind"] = "arrive", ["reason"] = "Someone there knows about the burst.",
                ["system"] = "Ossen's Lantern", ["line"] = "Scoop here.",
            },
        };

        foreach (var other in others)
        {
            beats.Add(new JsonObject { ["title"] = "The Bounty", ["function"] = "turn", ["kind"] = "bounty", ["count"] = other.Count, ["line"] = "Do it." });
        }

        if (task is not null)
        {
            beats.Add(new JsonObject { ["title"] = "The Task", ["function"] = "turn", ["kind"] = "event", ["task"] = task, ["count"] = 9, ["line"] = "Do it." });
        }

        beats.Add(new JsonObject
        {
            ["title"] = "The Anchorage", ["function"] = "resolution", ["kind"] = "dock", ["reason"] = "Someone there knows about the burst.",
            ["system"] = "Dyson's Hollow", ["station"] = "Maren Anchorage", ["line"] = "To one name.",
        });

        return new JsonObject { ["opening"] = "Again.", ["reply"] = "Here.", ["beats"] = beats }.ToJsonString();
    }

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

    private static bool HasTask(RoundScriptedLlmProvider provider, int round) =>
        JsonDocument.Parse(provider.Requests[round].ResponseSchema!.SchemaJson).RootElement
            .GetProperty("properties").GetProperty("beats").GetProperty("items").GetProperty("properties").TryGetProperty("task", out _);

    private static string Described(string name) =>
        StepEventCatalogue.Default.Drawable.First(row => row.Event == name).Description.TrimEnd('.');

    [Fact]
    public async Task TheBeatsPromptHoldsTheEventSlotLineAndOffersEvent()
    {
        var (outcome, provider) = await Written(Chapter([Bounties, Docking]), SpineText, BeatsWith("dock at five stations", Bounties));

        Assert.True(outcome.Succeeded, outcome.Refusal);

        foreach (var round in new[] { 0, 1 })
        {
            Assert.Contains($"an \"event\" objective for the event Docked: {Described("Docked")}, count 2", PromptOf(provider, round));
        }

        Assert.Contains("event", KindsOf(provider, 1));
        Assert.True(HasTask(provider, 1));
        Assert.Contains("\"event\": the Commander does what the drawn event objective above describes", PromptOf(provider, 1));
    }

    [Fact]
    public async Task AChapterWithoutAnEventSlotDoesNotMentionEventAsAKind()
    {
        var (_, provider) = await Written(Chapter([Bounties]), SpineText, BeatsWith(null, Bounties));

        Assert.DoesNotContain("event", KindsOf(provider, 1));
        Assert.False(HasTask(provider, 1));
        Assert.DoesNotContain("\"event\"", PromptOf(provider, 1));
        Assert.DoesNotContain("\"task\"", PromptOf(provider, 1));
    }

    [Fact]
    public async Task AnEventAnswerBecomesAnEventStepWithTheSlotsEventFieldsAndCount()
    {
        var slot = Docking with { Where = new EventFilter([new EventField("StationType", "Orbis")]) };
        var (outcome, _) = await Written(Chapter([Bounties, slot]), SpineText, BeatsWith("dock at two Orbis stations", Bounties));

        Assert.True(outcome.Succeeded, outcome.Refusal);

        var trigger = Assert.Single(outcome.Draft!.Beats, beat => beat.Trigger.Kind == TriggerKind.Event).Trigger;

        Assert.Equal("Docked", trigger.Event);
        Assert.Equal(slot.Where, trigger.Where);
        Assert.Equal(2, trigger.Count);
        Assert.Equal("dock at two Orbis stations", trigger.Task);
    }

    [Fact]
    public async Task AnAnswerThatLeavesTheEventSlotUnmetIsRepaired()
    {
        var without = BeatsWith(null, Bounties);
        var (outcome, provider) = await Written(Chapter([Bounties, Docking]), SpineText, without, Repairs.Replacing(without, 2), Repairs.Replacing(without, 2));

        Assert.False(outcome.Succeeded);
        Assert.Contains("has no objective for this one: an \"event\" objective for the event Docked", PromptOf(provider, 2));
    }

    [Fact]
    public async Task AToolRunStepsHandOffHoldsTheToolsFirstCommandPhrase()
    {
        var (outcome, provider) = await Written(Chapter([Bounties, Logging]), SpineText, BeatsWith("say \"log this\" to note it", Bounties));

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Contains("The Commander runs it by saying \"log this\": the \"task\" must tell them to say it", PromptOf(provider, 1));

        var step = Assert.Single(outcome.Draft!.Beats, beat => beat.Trigger.Kind == TriggerKind.Event);

        Assert.Equal(1, step.Trigger.Count);
        Assert.Contains("log this", step.HandOff());
    }

    [Fact]
    public async Task AToolRunTaskThatLeavesOutThePhraseIsRefused()
    {
        var answer = BeatsWith("note something in the log", Bounties);
        var (outcome, _) = await Written(Chapter([Bounties, Logging]), SpineText, answer, Repairs.Replacing(answer, 2), Repairs.Replacing(answer, 2));

        Assert.False(outcome.Succeeded);
        Assert.Contains("has to hold the phrase the Commander says, \"log this\"", outcome.Refusal);
    }
}
