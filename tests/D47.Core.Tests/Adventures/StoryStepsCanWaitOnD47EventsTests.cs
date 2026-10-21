using D47.Core.Adventures;
using D47.Core.Capabilities;
using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

/// <summary>A step waits on a tool the Commander ran or a line put on a checklist; the event is stored on the story and replayed with the journal.</summary>
public sealed class StoryStepsCanWaitOnD47EventsTests
{
    private const string Key = "the-lantern-route";

    private const string LogTool = "add_log_entry";

    private static readonly string Folder = Path.Combine(MemoryInstall.FakeRoot, "d47-events");
    private static readonly string AdventuresFile = Path.Combine(Folder, "adventures.json");

    private readonly MemoryFileSystem _files = new();
    private AdventureStore _store;
    private AdventureBook _book;

    public StoryStepsCanWaitOnD47EventsTests()
    {
        (_store, _book) = Open();
    }

    private (AdventureStore, AdventureBook) Open()
    {
        var store = new AdventureStore(AdventuresFile, _files, NullLogger<AdventureStore>.Instance);
        store.Poll();
        return (store, new AdventureBook(store, NullLogger<AdventureBook>.Instance));
    }

    private static DateTimeOffset At(int minutes) => Accepted.AddMinutes(minutes);

    private static AdventureTrigger Runs(string tool, int? count = 1) => new()
    {
        Kind = TriggerKind.Event,
        Event = D47Event.ToolRun,
        Where = new EventFilter([new EventField("Tool", tool)]),
        Task = "log an entry",
        Count = count,
    };

    private static AdventureTrigger LineOn(string group) => new()
    {
        Kind = TriggerKind.Event,
        Event = D47Event.ChecklistLineAdded,
        Where = new EventFilter([new EventField("Group", group)]),
        Task = "put a line on the checklist",
        Count = 1,
    };

    /// <summary>Arrive at the Lantern; then the step under test, with its <c>failsOn</c>; then fly home.</summary>
    private static Adventure Waiting(AdventureTrigger trigger, params AdventureTrigger[] failsOn)
    {
        var route = LanternRoute(Accepted);

        return route with
        {
            Beats =
            [
                route.Beats[0],
                Beat("The Log", "catalyst", trigger, "Logged.") with { FailsOn = failsOn },
                route.Beats[4],
            ],
        };
    }

    private static D47Event Ran(string tool, int minutes) =>
        D47Event.Ran(new ToolInvocation(tool, Succeeded: true, ToolCaller.Commander, Ran: true), At(minutes))!;

    /// <summary>Written, caught up, and at the Lantern a minute after acceptance, so the step under test is the one waited on.</summary>
    private AdventureStanding AtTheStep(Adventure adventure)
    {
        Assert.Null(_book.Write("F1", adventure));
        _book.CatchUp([]);
        _book.Observe(Jump(Lantern, At(1)), "F1");
        return Standing();
    }

    private AdventureStanding Standing() => _book.Standing("F1", Key)!;

    private string Journal(params JournalEvent[] events)
    {
        var path = Path.Combine(Folder, "Journal.2026-08-22T190000.01.log");
        _files.WriteText(path, string.Join('\n', events.Select(e => e.Raw.GetRawText())) + "\n");
        return path;
    }

    [Fact]
    public void ACommanderRunOfTheToolFiresTheStepAndIsStoredOnTheStory()
    {
        Assert.Equal(1, AtTheStep(Waiting(Runs(LogTool))).Current);

        _book.Record("F1", Ran(LogTool, 2));

        Assert.Equal(2, Standing().Current);
        Assert.Equal(D47Event.ToolRun, Standing().FiredBy[^1]);
        Assert.Equal([D47Event.ToolRun], _store.Find("F1", Key)!.Events.Select(made => made.Name));
        Assert.Equal(LogTool, _store.Find("F1", Key)!.Events[0].Fields.Single(field => field.Field == "Tool").Value);
    }

    [Fact]
    public void AModelRunOrARefusedOrFailedRunIsNoEvent()
    {
        Assert.Null(D47Event.Ran(new ToolInvocation(LogTool, Succeeded: true, ToolCaller.Model, Ran: true), At(2)));
        Assert.Null(D47Event.Ran(new ToolInvocation(LogTool, Succeeded: true, ToolCaller.Commander, Ran: false), At(2)));
        Assert.Null(D47Event.Ran(new ToolInvocation(LogTool, Succeeded: false, ToolCaller.Commander, Ran: false), At(2)));
    }

    [Fact]
    public async Task OnlyTheCommandersCleanRunThroughTheRegistryAdvancesTheStory()
    {
        AtTheStep(Waiting(Runs(LogTool)));

        var registry = CapabilityRegistry.Build(
        [
            new CapabilityDescriptor
            {
                Id = "captains-log-test",
                Group = "Test",
                Name = "Log",
                Summary = "A log.",
                Tools =
                [
                    new ToolDefinition
                    {
                        Name = LogTool,
                        Description = "Logs a line.",
                        Parameters = [new ToolParameter { Name = "text", Type = ToolParameterType.String, Description = "The line.", Required = true }],
                        Handler = (_, _) => Task.FromResult(ToolResult.Ok("Logged.")),
                    },
                ],
            },
        ]);

        registry.ToolInvoked += invocation =>
        {
            if (D47Event.Ran(invocation, At(2)) is { } made)
            {
                _book.Record("F1", made);
            }
        };

        var text = new ToolArguments(new Dictionary<string, string>(StringComparer.Ordinal) { ["text"] = "Arrived." });
        var cancel = TestContext.Current.CancellationToken;

        await registry.InvokeAsync(LogTool, text, cancel, ToolCaller.Model);
        await registry.InvokeAsync(LogTool, ToolArguments.Empty, cancel, ToolCaller.Commander);

        Assert.Equal(1, Standing().Current);
        Assert.Empty(_store.Find("F1", Key)!.Events);

        await registry.InvokeAsync(LogTool, text, cancel, ToolCaller.Commander);

        Assert.Equal(2, Standing().Current);
    }

    [Fact]
    public void ARunOfAToolNoStepNamesIsStoredNowhere()
    {
        AtTheStep(Waiting(Runs(LogTool)));

        _book.Record("F1", Ran("get_app_status", 2));

        Assert.Equal(1, Standing().Current);
        Assert.Empty(_store.Find("F1", Key)!.Events);
    }

    [Fact]
    public void ALineTypedOnTheChecklistFiresAStepWaitingOnACustomLine()
    {
        AtTheStep(Waiting(LineOn("custom")));

        var checklists = TestSurface.Checklists(new MemoryInstall().Paths);
        checklists.LineAdded += scope => _book.Record("F1", D47Event.LineAdded(scope, At(2)));

        checklists.AddTyped(ChecklistScope.Universal, "buy limpets");

        Assert.Equal(2, Standing().Current);
        Assert.Single(_store.Find("F1", Key)!.Events);
    }

    [Fact]
    public void ALineIsAnnouncedOnceWhetherTypedOrAcceptedAndNotForAChangeToALine()
    {
        var checklists = TestSurface.Checklists(new MemoryInstall().Paths);
        var added = new List<ChecklistScope>();
        checklists.LineAdded += added.Add;

        checklists.AddTyped(ChecklistScope.Universal, "buy limpets");
        Assert.Single(added);

        checklists.ProposeAdd(ChecklistScope.Universal, ["fit a scoop", "sell the cargo"]);
        checklists.Accept();
        Assert.Equal(3, added.Count);

        var line = checklists.Document.Items.First(item => item.Text == "buy limpets");
        checklists.Complete(line.Id);
        checklists.Reword(line.Id, "buy more limpets");
        Assert.Equal(3, added.Count);
    }

    [Fact]
    public void EachGroupIsSpelledAsTheChecklistToolSpellsIt()
    {
        Assert.Equal("custom", D47Event.LineAdded(ChecklistScope.Universal, At(2)).Fields.Single().Value);
        Assert.Equal("ship", D47Event.LineAdded(ChecklistScope.Ship(3), At(2)).Fields.Single().Value);
        Assert.Equal("system", D47Event.LineAdded(ChecklistScope.System("Sol"), At(2)).Fields.Single().Value);
        Assert.Equal("suit", D47Event.LineAdded(ChecklistScope.Suit(7), At(2)).Fields.Single().Value);
        Assert.Equal("weapon", D47Event.LineAdded(ChecklistScope.Weapon(9), At(2)).Fields.Single().Value);
    }

    [Fact]
    public void TheStepEventTableHoldsBothEventsAndTheirFields()
    {
        var catalogue = StepEventCatalogue.Default;

        Assert.Contains("Tool", catalogue.FieldsOf(D47Event.ToolRun));
        Assert.Contains("Group", catalogue.FieldsOf(D47Event.ChecklistLineAdded));

        foreach (var group in Enum.GetValues<ChecklistGroup>())
        {
            Assert.True(catalogue.AllowsValue(D47Event.ChecklistLineAdded, "Group", ChecklistScope.Word(group)), group.ToString());
        }
    }

    [Fact]
    public void ACatchUpWalkGivesTheStandingTheLiveRunGave()
    {
        AtTheStep(Waiting(Runs(LogTool, count: 3)));

        _book.Record("F1", Ran(LogTool, 2));
        _book.Record("F1", Ran(LogTool, 3));
        _book.Observe(Docked(Anchorage, At(4)), "F1");

        Assert.Equal(1, Standing().Current);
        Assert.Equal(2, Standing().Counted);

        _book.Record("F1", Ran(LogTool, 5));

        var live = Standing();
        Assert.Equal(2, live.Current);

        var journal = Journal(Commander("F1", Accepted.AddMinutes(-1)), Jump(Lantern, At(1)), Docked(Anchorage, At(4)));
        (_store, _book) = Open();
        _book.CatchUp([journal]);

        var walked = Standing();
        Assert.Equal(live.Current, walked.Current);
        Assert.Equal(live.StepFires.Select(fire => (fire.Beat, fire.Step, fire.At)), walked.StepFires.Select(fire => (fire.Beat, fire.Step, fire.At)));
        Assert.Equal(At(5), walked.StepFires[^1].At);
    }

    /// <summary>The fold is run before every journal event; an event it already applied must not count again.</summary>
    [Fact]
    public void AStoredEventCountsOnceHoweverManyJournalEventsFollowIt()
    {
        AtTheStep(Waiting(Runs(LogTool, count: 3)));

        _book.Record("F1", Ran(LogTool, 2));
        _book.Observe(Docked(Anchorage, At(3)), "F1");
        _book.Record("F1", Ran(LogTool, 4));
        _book.Observe(Docked(Anchorage, At(5)), "F1");

        Assert.Equal(1, Standing().Current);
        Assert.Equal(2, Standing().Counted);

        var journal = Journal(Commander("F1", Accepted.AddMinutes(-1)), Jump(Lantern, At(1)), Docked(Anchorage, At(3)), Docked(Anchorage, At(5)));
        (_store, _book) = Open();
        _book.CatchUp([journal]);

        Assert.Equal(1, Standing().Current);
        Assert.Equal(2, Standing().Counted);
    }

    [Fact]
    public void AnEventAFailsOnWaitsForFailsTheChapterAndSaysSo()
    {
        AtTheStep(Waiting(new AdventureTrigger { Kind = TriggerKind.Dock, MarketId = Anchorage, System = "Dyson's Hollow", Station = "Maren Anchorage" }, Runs(LogTool, count: null)));
        _book.Drain();

        _book.Record("F1", Ran(LogTool, 2));

        Assert.True(Standing().IsFailed);
        Assert.Contains(_book.Drain(), moment => moment.IsFailure);
        Assert.Single(_store.Find("F1", Key)!.Events);
    }

    [Fact]
    public void TheStoreKeepsTheEventsAndRefusesTheTwoHundredAndFirst()
    {
        var events = Enumerable.Range(0, AdventureLimits.MaxEvents).Select(index => Ran(LogTool, 2) with { At = At(2).AddSeconds(index) }).ToList();
        var adventure = Waiting(Runs(LogTool, count: 500)) with { Events = events };

        Assert.Null(_store.Save("F1", adventure));
        Assert.NotNull(_store.Save("F1", adventure with { Events = [.. events, Ran(LogTool, 9)] }));

        var (reopened, _) = Open();
        var read = reopened.Find("F1", Key)!.Events;

        Assert.Equal(events.Select(made => (made.Name, made.At)), read.Select(made => (made.Name, made.At)));
        Assert.Equal([new EventField("Tool", LogTool)], read[0].Fields);
    }

    [Fact]
    public void AFullStoryRefusesTheNextEvent()
    {
        var events = Enumerable.Range(0, AdventureLimits.MaxEvents).Select(index => Ran(LogTool, 2) with { At = At(2).AddSeconds(index) }).ToList();
        AtTheStep(Waiting(Runs(LogTool, count: 500)) with { Events = events });

        _book.Record("F1", Ran(LogTool, 9));

        Assert.Equal(AdventureLimits.MaxEvents, _store.Find("F1", Key)!.Events.Count);
    }

    [Fact]
    public void BeginningAStoryClearsItsEvents()
    {
        var adventure = Waiting(Runs(LogTool)) with { AcceptedAt = null, Events = [Ran(LogTool, 2)] };
        Assert.Null(_book.Write("F1", adventure));

        Assert.Null(_book.Begin("F1", Key, At(3)));

        Assert.Empty(_store.Find("F1", Key)!.Events);
    }
}
