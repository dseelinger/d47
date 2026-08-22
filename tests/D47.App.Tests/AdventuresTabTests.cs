using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core;
using D47.Core.Adventures;
using D47.Core.Conversation;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The Adventures tab draws, and every level below it builds (list.md Phase 47).
/// <para>
/// Construction tests rather than behaviour ones: the fold, the store, the context and the callout
/// are held in Core, and what the surface can get wrong on its own is a level that throws when it
/// is built or a card that says a number. Both are cheap to hold here and expensive to find in a
/// headset.
/// </para>
/// </summary>
public class AdventuresTabTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 20, 0, 0, TimeSpan.Zero);

    private static CommanderGameState State()
    {
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{ "timestamp":"2026-08-22T19:00:00Z", "event":"Commander", "FID":"F1", "Name":"Jameson" }""",
                     """{ "timestamp":"2026-08-22T19:01:00Z", "event":"Location", "StarSystem":"Shinrarta Dezhra", "SystemAddress":3932277478106, "Docked":true, "StationName":"Jameson Memorial", "StationType":"Coriolis", "MarketID":128666762, "Body":"Shinrarta Dezhra A", "BodyID":1 }""",
                 })
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store.Active!;
    }

    private static Adventure Story(DateTimeOffset? acceptedAt, AdventureSource source = AdventureSource.Commander) => new()
    {
        Key = "the-lantern-route",
        Name = "The Lantern Route",
        Source = source,
        WrittenBy = source == AdventureSource.Generated ? "archivist" : null,
        Spine = new AdventureSpine { Premise = "An outpost abandoned in 3302 still runs a beacon.", Turn = "To one name.", Ending = "Forty kilometres short." },
        Opening = "Beacons cost money.",
        Beats =
        [
            new AdventureBeat
            {
                Title = "The Lantern",
                Function = "setup",
                Trigger = new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = 1, System = "Ossen's Lantern" },
                Line = "Scoop here.",
            },
            new AdventureBeat
            {
                Title = "The Anchorage",
                Function = "midpoint",
                Trigger = new AdventureTrigger { Kind = TriggerKind.Dock, MarketId = 2, System = "Dyson's Hollow", Station = "Maren Anchorage" },
                Line = "To one name.",
            },
        ],
        AcceptedAt = acceptedAt,
    };

    private static (PanelView Panel, AdventureBook Book, List<string> Said) Open(params Adventure[] adventures) =>
        Open(900, null, null, adventures);

    /// <param name="width">The window's, which decides how many panes the strip shows: 900 is two, 1400 is three.</param>
    private static (PanelView Panel, AdventureBook Book, List<string> Said) Open(
        double width,
        ILlmProvider? provider,
        IGalaxyService? galaxy,
        params Adventure[] adventures)
    {
        var paths = new AppPaths(TempFolders.Create("d47-adventures-tab-tests"));
        paths.EnsureCreated();

        var store = new AdventureStore(Path.Combine(paths.Data, "adventures.json"), NullLogger<AdventureStore>.Instance);
        var book = new AdventureBook(store, NullLogger<AdventureBook>.Instance);

        foreach (var adventure in adventures)
        {
            Assert.Null(book.Write("F1", adventure));
        }

        var state = State();
        var said = new List<string>();

        var generator = new AdventureGenerator(
            () => provider, () => null, () => null, () => null, () => null, () => state,
            () => galaxy, () => null, null, null, NullLogger.Instance);

        var surface = new AdventureSurface(
            book,
            generator,
            () => state,
            () => "F1",
            () => Now,
            said.Add,
            () => provider is not null,
            () => galaxy is not null,
            () => null,
            () => { });

        // The palette, so a themed brush is a colour rather than unset — the trigger's highlight is
        // one of the things asserted here.
        new D47.App.Theming.ThemeManager(Application.Current!, NullLogger<D47.App.Theming.ThemeManager>.Instance)
            .Apply(TestSurface.Settings().Current.Ui.Theme);

        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableAdventures(surface);

        var window = new Window { Content = panel, Width = width, Height = 700 };
        window.Show();

        panel.Tab = PanelTab.Adventures;
        Dispatcher.UIThread.RunJobs();

        return (panel, book, said);
    }

    private static IReadOnlyList<string> Drawn(PanelView panel) =>
        [.. panel.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];

    /// <summary>What is actually on screen — mini hides the big page rather than unbuilding it.</summary>
    private static IReadOnlyList<string> Visible(PanelView panel) =>
    [
        .. panel.GetVisualDescendants().OfType<TextBlock>()
            .Where(block => block.IsEffectivelyVisible)
            .Select(block => block.Text ?? string.Empty),
    ];

    private static IReadOnlyList<AdventureThinking> Pulses(PanelView panel) =>
        [.. panel.GetVisualDescendants().OfType<AdventureThinking>().Where(pulse => pulse.IsEffectivelyVisible)];

    /// <summary>The Commander arriving at the first beat's system.</summary>
    private static void Arrive(AdventureBook book)
    {
        Assert.True(JournalEvent.TryParse(
            """{ "timestamp":"2026-08-22T19:30:00Z", "event":"FSDJump", "StarSystem":"Ossen's Lantern", "SystemAddress":1 }""",
            NullLogger.Instance,
            out var jump));

        book.Observe(jump!, "F1");
    }

    private static AdventureTold Beat(string text) => new()
    {
        Kind = AdventureToldKind.Beat,
        Text = text,
        At = Now,
        Beat = 0,
        Title = "The Lantern",
        Trigger = "arrive at Ossen's Lantern",
    };

    /// <summary>
    /// The root card says where the story is, how far through it is, and what to do next.
    /// <para>
    /// <b>The step is here on the Commander's instruction, 2026-08-22</b>, and this test asserted
    /// the opposite until that day — Phase 47's rule was that no count reaches the Commander. See
    /// <see cref="AdventureStanding"/> for why it changed and what did not: the beats are still
    /// titles rather than numbered stops, and <c>Step</c> is the only place a count is spelled.
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void TheRootDrawsCardsWithTheStepAndTheNextTrigger()
    {
        var (panel, _, _) = Open(Story(Now.AddHours(-1)), Story(null, AdventureSource.Generated) with { Key = "draft", Name = "The Draft" });

        var drawn = Drawn(panel);

        Assert.Contains(drawn, text => text.Contains("The Lantern Route"));
        Assert.Contains(drawn, text => text.Contains("yours") && text.Contains("The Lantern"));
        Assert.Contains(drawn, text => text.Contains("written by Archivist") && text.Contains("waiting for your yes"));
        Assert.Contains(drawn, text => text.Contains("Step 1 of 2"));
        Assert.Contains(drawn, text => text.Contains("Next: arrive at Ossen's Lantern"));

        // A draft has no step: it is not being flown, and "Step 1 of 2" on a story nobody has
        // agreed to reads as one already under way.
        Assert.DoesNotContain(drawn, text => text.Contains("The Draft") && text.Contains("Step"));
        Assert.Contains(panel.GetVisualDescendants().OfType<Button>(), button => Equals(button.Content, "Change something"));
    }

    [AvaloniaFact]
    public void TheReadingLevelShowsTheStorySoFarAndNothingAhead()
    {
        var (panel, book, _) = Open(Story(Now.AddHours(-1)));

        Assert.True(JournalEvent.TryParse(
            """{ "timestamp":"2026-08-22T19:30:00Z", "event":"FSDJump", "StarSystem":"Ossen's Lantern", "SystemAddress":1 }""",
            NullLogger.Instance,
            out var jump));

        book.Observe(jump!, "F1");

        panel.Nav.GoTo(new NavCrumb(AdventuresPage.ReadPrefix + "the-lantern-route", "The Lantern Route"));
        Dispatcher.UIThread.RunJobs();

        var drawn = Drawn(panel);

        Assert.Contains(drawn, text => text.Contains("Scoop here."));
        Assert.Contains(drawn, text => text.Contains("Step 2 of 2"));

        // What to do next, headed "Next" and phrased as an instruction (asked for 2026-08-22).
        // The beat's title still appears — Place() has always named it — but it is no longer the
        // heading over the instruction, where it read as another chapter of the story.
        Assert.Contains(drawn, text => text.Contains("Next"));
        Assert.Contains(drawn, text => text.Contains("Dock at Maren Anchorage in Dyson's Hollow."));

        Assert.DoesNotContain(drawn, text => text.Contains("To one name."));
        Assert.DoesNotContain(drawn, text => text.Contains("Forty kilometres"));
        Assert.Contains(panel.GetVisualDescendants().OfType<Button>(), button => Equals(button.Content, "Abandon"));
    }

    [AvaloniaFact]
    public void TheEditorBuildsForANewStoryAndPrintsWhyBeginIsShut()
    {
        var (panel, _, _) = Open();

        panel.Nav.GoTo(new NavCrumb(AdventuresPage.EditPrefix + AdventuresPage.NewKey, "Write"));
        Dispatcher.UIThread.RunJobs();

        var drawn = Drawn(panel);

        Assert.Contains(drawn, text => text.Contains("Write an adventure"));
        Assert.Contains(drawn, text => text.Contains("An adventure needs a name."));
        Assert.Contains(drawn, text => text.Contains("at least one beat"));

        var begin = panel.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Save and begin"));
        Assert.False(begin.IsEnabled);
    }

    [AvaloniaFact]
    public void TheAskFormBuildsAndSaysWhyItIsShut()
    {
        var (panel, _, _) = Open();

        panel.Nav.GoTo(new NavCrumb(AdventuresPage.AskKey, "Ask"));
        Dispatcher.UIThread.RunJobs();

        var drawn = Drawn(panel);

        Assert.Contains(drawn, text => text.Contains("Ask for an adventure"));
        Assert.Contains(drawn, text => text.Contains("needs a language model"));

        var go = panel.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Go"));
        Assert.False(go.IsEnabled);
    }

    [AvaloniaFact]
    public void BeginFromTheReadingLevelStampsTheStory()
    {
        var (panel, book, said) = Open(Story(null));

        panel.Nav.GoTo(new NavCrumb(AdventuresPage.ReadPrefix + "the-lantern-route", "The Lantern Route"));
        Dispatcher.UIThread.RunJobs();

        var begin = panel.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Begin"));
        begin.Command?.Execute(null);
        begin.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Now, book.Store.Find("F1", "the-lantern-route")?.AcceptedAt);
        Assert.Empty(said);

        // The opening is the callout's to say, on the next tick, in the core's voice.
        Assert.True(Assert.Single(book.Drain()).IsOpening);
    }

    /// <summary>
    /// Pressing Go on a strip wide enough for three panes, which is where the first generated
    /// story offered from the field took the process down (2026-08-22): the offer navigated with
    /// the root supplied as well as kept, the trail held the root twice, and the strip tried to
    /// host the root page in two panes. The model and the galaxy are scripted; the press, the
    /// background turn, the store write, the spoken reply and the navigation are the real ones.
    /// </summary>
    [AvaloniaFact]
    public void AnOfferedStoryOpensItsReadingLevelOnAWideStrip()
    {
        var (panel, book, said) = Open(1400, new ScriptedModel(Spine, Beats), new Galaxy());

        Assert.Equal(3, panel.GetVisualDescendants().OfType<DrillView>().Single().Panes);

        panel.Nav.GoTo(new NavCrumb(AdventuresPage.AskKey, "Ask"));
        Dispatcher.UIThread.RunJobs();

        var go = panel.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Go"));
        Assert.True(go.IsEnabled);
        go.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        // The turn runs off the UI thread and posts the offer back; pump until it lands.
        var deadline = DateTime.UtcNow.AddSeconds(10);

        while (panel.Nav.Trail[^1].Key == AdventuresPage.AskKey && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(20);
            Dispatcher.UIThread.RunJobs();
        }

        Dispatcher.UIThread.RunJobs();

        // A refusal leaves the form up with the reason under Go, which is what the message shows.
        Assert.True(
            panel.Nav.Trail[^1].Key == AdventuresPage.ReadPrefix + "the-unrecoverable-column",
            string.Join(" | ", panel.Nav.Trail.Select(crumb => crumb.Key)) + " — " + string.Join(" | ", Drawn(panel)));
        Assert.Equal(["adventures", AdventuresPage.ReadPrefix + "the-unrecoverable-column"], panel.Nav.Trail.Select(crumb => crumb.Key));
        Assert.NotNull(book.Store.Find("F1", "the-unrecoverable-column"));
        Assert.Contains("Here it is.", said);

        var drawn = Drawn(panel);
        Assert.Contains(drawn, text => text.Contains("The Unrecoverable Column"));
        Assert.Contains(panel.GetVisualDescendants().OfType<Button>(), button => Equals(button.Content, "Accept"));
    }

    private const string Spine = """
        {"name": "The Unrecoverable Column", "premise": "A ledger will not balance.", "want": "To find the freight.",
         "stake": "Whether a debt can be owed to nobody.", "turn": "It was never loaded.", "ending": "The column balances."}
        """;

    private const string Beats = """
        {"opening": "Somebody is paying.", "reply": "Here it is.", "beats": [
          {"title": "The Lantern", "function": "setup", "kind": "arrive", "system": "Ossen's Lantern", "line": "Scoop here."},
          {"title": "The Anchorage", "function": "turn", "kind": "dock", "system": "Dyson's Hollow", "station": "Maren Anchorage", "line": "To one name."},
          {"title": "The Column", "function": "resolution", "kind": "rank", "career": "Trade", "rank": 1, "line": "It balances."}
        ]}
        """;

    /// <summary>One reply per request, in order; a third request is the test's failure, not a repeat.</summary>
    private sealed class ScriptedModel(params string[] replies) : ILlmProvider
    {
        private int _calls;

        public string Id => "anthropic";

        public string DisplayName => "Scripted";

        public string DefaultModel => "claude-opus-5";

        public LlmProviderCapabilities CapabilitiesFor(string model) => new()
        {
            SupportsPromptCaching = true,
            SupportsThinkingEffort = true,
            SupportsOperatorSystemMessages = true,
            MinimumCacheablePrefixTokens = 512,
        };

        public async IAsyncEnumerable<LlmStreamEvent> StreamAsync(
            LlmRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var round = _calls++;

            if (round >= replies.Length)
            {
                throw new InvalidOperationException($"Round {round + 1} was asked for and {replies.Length} were scripted.");
            }

            yield return new LlmStreamEvent.TextDelta(replies[round]);
            yield return new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.Completed);
            await Task.CompletedTask;
        }
    }

    /// <summary>Three systems, one station, every distance twelve light years.</summary>
    private sealed class Galaxy : IGalaxyService
    {
        private static readonly Dictionary<string, long> Systems = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Shinrarta Dezhra"] = 3932277478106,
            ["Ossen's Lantern"] = 1,
            ["Dyson's Hollow"] = 3,
        };

        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken)
        {
            var canonical = Systems.Keys.FirstOrDefault(name => string.Equals(name, query.ReferenceSystem, StringComparison.OrdinalIgnoreCase));

            return Task.FromResult(canonical is null
                ? new GalaxySearchResult(query.ReferenceSystem, 0, [])
                : new GalaxySearchResult(canonical, 1, [new SystemSummary { Name = canonical, SystemAddress = Systems[canonical], Distance = 0 }]));
        }

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) => Task.FromResult<double?>(12);

        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken)
        {
            IReadOnlyList<StationSummary> all =
            [
                new() { Name = "Maren Anchorage", SystemName = "Dyson's Hollow", SystemAddress = 3, MarketId = 2, Distance = 12, HasLargePad = true },
            ];

            var stations = query.MaxDistance <= 1
                ? all.Where(station => string.Equals(station.SystemName, query.ReferenceSystem, StringComparison.OrdinalIgnoreCase)).ToList()
                : all;

            return Task.FromResult(new StationSearchResult(query.ReferenceSystem, stations.Count, stations));
        }

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new BodySearchResult(query.ReferenceSystem, 0, []));

        public Task<ColonisationScan> ScanForColonisationAsync(ColonisationQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    /// <summary>
    /// The reading level shows what was actually said, not the authored line — and the trigger
    /// beside it is in the highlight colour (asked for 2026-08-22).
    /// </summary>
    [AvaloniaFact]
    public void TheReadingLevelShowsWhatWasSaidWithTheTriggerHighlighted()
    {
        var (panel, book, _) = Open(Story(Now.AddHours(-1)));

        Arrive(book);
        book.Told("F1", "the-lantern-route", Beat("The lantern is still turning, and somebody is paying for it."));
        book.Told("F1", "the-lantern-route", new AdventureTold
        {
            Kind = AdventureToldKind.Aside,
            Asked = "Who keeps the lantern lit?",
            Text = "Nobody I can name yet. The bills are being paid from somewhere.",
            At = Now,
        });

        panel.Nav.GoTo(new NavCrumb(AdventuresPage.ReadPrefix + "the-lantern-route", "The Lantern Route"));
        Dispatcher.UIThread.RunJobs();

        var drawn = Drawn(panel);

        // What was heard, in the core's voice — and the authored line it replaced is not drawn.
        Assert.Contains(drawn, text => text.Contains("The lantern is still turning"));
        Assert.DoesNotContain(drawn, text => text == "Scoop here.");

        // The aside, as an exchange rather than as a paragraph from nowhere.
        Assert.Contains(drawn, text => text.Contains("Who keeps the lantern lit?"));
        Assert.Contains(drawn, text => text.Contains("The bills are being paid"));

        var accent = panel.FindResource(D47.App.Theming.ThemeManager.AccentKey);
        var body = panel.FindResource(D47.App.Theming.ThemeManager.TextKey);

        var trigger = panel.GetVisualDescendants().OfType<TextBlock>()
            .Single(block => block.Text == "Arrive at Ossen's Lantern.");

        Assert.NotEqual(accent, body);
        Assert.Equal(accent, trigger.Foreground);
    }

    /// <summary>
    /// The wait, drawn (asked for 2026-08-22). A beat fires and the line is up to twenty-three
    /// seconds behind it, which is where "I am not sure I did the thing" comes from.
    /// </summary>
    [AvaloniaFact]
    public void TheTabShowsThatD47IsComposingUntilTheLineArrives()
    {
        var (panel, book, _) = Open(Story(Now.AddHours(-1)));

        Assert.Empty(Pulses(panel));

        Arrive(book);
        Dispatcher.UIThread.RunJobs();

        Assert.NotEmpty(Pulses(panel));

        // Driven by the tick rather than by a timer of its own, and honest about whether the frame
        // moved — the headset sets its dirty flag from this, and a flag held true every frame is
        // what made the panel flicker the last time something set one unconditionally.
        Assert.False(panel.TickAdventures());
        Assert.False(panel.TickAdventures());
        Assert.True(panel.TickAdventures());

        book.Told("F1", "the-lantern-route", Beat("The lantern is still turning."));
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(Pulses(panel));
        Assert.False(panel.TickAdventures());
    }

    /// <summary>
    /// Mini reads the tab the panel is on (asked for 2026-08-22): the story, how far through, what
    /// was just done, what to do next, and the last thing the ship's AI said about it. Nothing
    /// pressable — mini is read at a glance with hands on a stick.
    /// </summary>
    [AvaloniaFact]
    public void TheMiniPanelShowsTheStoryAtAGlance()
    {
        var (panel, book, _) = Open(Story(Now.AddHours(-1)));

        Arrive(book);
        book.Told("F1", "the-lantern-route", Beat("The lantern is still turning."));

        panel.Mode = PanelMode.Mini;
        Dispatcher.UIThread.RunJobs();

        var drawn = Visible(panel);

        Assert.Contains(drawn, text => text.Contains("The Lantern Route"));
        Assert.Contains(drawn, text => text.Contains("Step 2 of 2"));
        Assert.Contains(drawn, text => text.Contains("An outpost abandoned in 3302"));
        Assert.Contains(drawn, text => text.Contains("Done: Arrive at Ossen's Lantern."));
        Assert.Contains(drawn, text => text.Contains("Next: Dock at Maren Anchorage in Dyson's Hollow."));
        Assert.Contains(drawn, text => text.Contains("The lantern is still turning."));

        // The spine's turn and its ending are withheld on every surface, this one included.
        Assert.DoesNotContain(drawn, text => text.Contains("Forty kilometres"));
        Assert.DoesNotContain(drawn, text => text.Contains("To one name."));

        // And the tab strip is still not there. Mini cannot afford one, which is why it follows the
        // big panel's tab rather than carrying its own.
        Assert.DoesNotContain(drawn, text => text == "Adventures");
    }

    /// <summary>
    /// Mini with no story under way says so, rather than drawing an empty pane that reads as a
    /// panel that failed to load.
    /// </summary>
    [AvaloniaFact]
    public void TheMiniPanelSaysWhenThereIsNoStory()
    {
        var (panel, _, _) = Open(Story(null, AdventureSource.Generated) with { Key = "draft", Name = "The Draft" });

        panel.Mode = PanelMode.Mini;
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(Visible(panel), text => text.Contains("No adventure under way."));
    }
}
