using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>A chapter's last slot is sometimes an event step drawn by d47, and the writer is told only that slot.</summary>
public sealed class EventStepsAreDrawnIntoChaptersTests
{
    private const int Seeds = 1000;

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
            CareerWeights.From(null, CareerStatistics.Empty), RankState.Empty, ship ?? Ship(), odyssey: true, [], refused, comfort: false, CareerStatistics.Empty, 0, seed,
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

    private static AdventureAsk Chapter(params DrawnActivity[] slots) => PlannedChapterWorld.Ask(PlannedChapterWorld.Story with { Slots = slots });

    /// <summary>The chapter written with the task each answer gives the event objective, the last answer followed by no fidelity objection.</summary>
    private static async Task<(AdventureOutcome Outcome, RoundScriptedLlmProvider Provider)> Written(AdventureAsk ask, params string?[] tasks)
    {
        var plan = await PlannedChapterWorld.PlanAsync(ask);
        var at = plan.Objectives.Select((objective, index) => (objective, index)).FirstOrDefault(pair => pair.objective.Steps.Any(step => step.Kind == TriggerKind.Event)).index + 1;
        var provider = new RoundScriptedLlmProvider(
        [
            .. tasks.Select(task => RoundScriptedLlmProvider.Saying(PlannedChapterWorld.Lines(plan.Objectives.Count, (number, objective) => objective["task"] = number == at ? task : null))),
            RoundScriptedLlmProvider.Saying(PlannedChapterWorld.NoObjections),
        ]);
        var outcome = await PlannedChapterWorld.Generator(provider).GenerateAsync(ask, Now, TestContext.Current.CancellationToken);

        return (outcome, provider);
    }

    private static string PromptOf(RoundScriptedLlmProvider provider, int round) => provider.Requests[round].Prompt.History[0].Text;

    private static bool HasTask(RoundScriptedLlmProvider provider, int round) => PlannedChapterWorld.Properties(provider.Requests[round].ResponseSchema!.SchemaJson).Contains("task");

    private static string Described(string name) =>
        StepEventCatalogue.Default.Drawable.First(row => row.Event == name).Description.TrimEnd('.');

    [Fact]
    public async Task TheLinesPromptHoldsTheEventSlotLineAndAsksForATask()
    {
        var (outcome, provider) = await Written(Chapter(Bounties, Docking), "dock at two stations");

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Contains($"an \"event\" objective for the event Docked: {Described("Docked")}, count 2", PromptOf(provider, 0));
        Assert.True(HasTask(provider, 0));
        Assert.Contains("An objective with an event step also has a \"task\"", PromptOf(provider, 0));
    }

    [Fact]
    public async Task AChapterWithoutAnEventSlotAsksForNoTask()
    {
        var (_, provider) = await Written(Chapter(Bounties), [null]);

        Assert.False(HasTask(provider, 0));
        Assert.DoesNotContain("\"task\"", PromptOf(provider, 0));
    }

    [Fact]
    public async Task AnEventObjectiveCarriesTheSlotsEventFieldsAndCountAndTheWrittenTask()
    {
        var slot = Docking with { Where = new EventFilter([new EventField("StationType", "Orbis")]) };
        var (outcome, _) = await Written(Chapter(Bounties, slot), "dock at two Orbis stations");

        Assert.True(outcome.Succeeded, outcome.Refusal);

        var trigger = Assert.Single(outcome.Draft!.Beats.SelectMany(beat => beat.Steps), step => step.Trigger.Kind == TriggerKind.Event).Trigger;

        Assert.Equal("Docked", trigger.Event);
        Assert.Equal(slot.Where, trigger.Where);
        Assert.Equal(2, trigger.Count);
        Assert.Equal("dock at two Orbis stations", trigger.Task);
    }

    [Fact]
    public async Task AToolRunStepsHandOffHoldsTheToolsFirstCommandPhrase()
    {
        var (outcome, provider) = await Written(Chapter(Bounties, Logging), "say \"log this\" to note it");

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Contains("The Commander runs it by saying \"log this\": the \"task\" must tell them to say it", PromptOf(provider, 0));

        var step = Assert.Single(outcome.Draft!.Beats.SelectMany(beat => beat.Steps), step => step.Trigger.Kind == TriggerKind.Event);

        Assert.Equal(1, step.Trigger.Count);
        Assert.Contains("log this", step.HandOff());
    }

    [Fact]
    public async Task AToolRunTaskThatLeavesOutThePhraseIsWrittenAgain()
    {
        var (outcome, provider) = await Written(Chapter(Bounties, Logging), "note something in the log", "say \"log this\" to note it");

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal("story_line_repairs", provider.Requests[1].ResponseSchema!.Name);
        Assert.Contains("has to hold the phrase the Commander says, \"log this\"", PromptOf(provider, 1));
    }

    [Fact]
    public async Task AToolRunTaskThatStillLeavesOutThePhraseIsRefused()
    {
        var (outcome, _) = await Written(Chapter(Bounties, Logging), "note something in the log", "note something in the log", "note something in the log");

        Assert.False(outcome.Succeeded);
        Assert.Contains("has to hold the phrase the Commander says, \"log this\"", outcome.Refusal);
    }
}
