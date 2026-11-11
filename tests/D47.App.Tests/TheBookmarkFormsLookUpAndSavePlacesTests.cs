using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Bookmarks list rows and the add and edit pages (#2303).</summary>
public class TheBookmarkFormsLookUpAndSavePlacesTests
{
    private static readonly DateTimeOffset At = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

    private sealed class Spansh : StubStarSystemService, IStarSystemService
    {
        public override Task<IReadOnlyList<SystemNameMatch>> PrefixNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>(
                [new SystemNameMatch("Deciat", 100, new StarPosition(0, 0, 0))]);

        public Task<IReadOnlyList<BodyNameMatch>> BodyNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<BodyNameMatch>>(
                [new BodyNameMatch("Deciat 6 a", 61, 100, new StarPosition(0, 0, 0))]);

        public Task<IReadOnlyList<StationNameMatch>> StationNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StationNameMatch>>(
                [new StationNameMatch("Deciat Hub", 7, 100, new StarPosition(0, 0, 0))]);

        public override Task<StarSystemProfile?> ProfileAsync(long systemAddress, CancellationToken cancellationToken) =>
            Task.FromResult<StarSystemProfile?>(new StarSystemProfile { Name = "Deciat", SystemAddress = 100 });
    }

    private sealed record Page(Window Window, PanelView Panel, BookmarkStore Store, List<string> Plotted);

    private static GameStateStore CommanderState()
    {
        var gameState = new GameStateStore();

        Assert.True(JournalEvent.TryParse(
            "{\"timestamp\":\"2026-08-25T09:00:00Z\",\"event\":\"Commander\",\"FID\":\"F1\",\"Name\":\"Jameson\"}",
            NullLogger.Instance,
            out var commander));

        gameState.Apply(commander!);

        return gameState;
    }

    private static CapabilityDescriptor Course(List<string> plotted) => new()
    {
        Id = "test-course",
        Group = "Acting on the game",
        Name = "Course",
        Summary = "Plots.",
        Display = new CapabilityDisplay { PanelTitle = "Course", Order = 67 },
        Tools =
        [
            new ToolDefinition
            {
                Name = "plot_course",
                Description = "Plot.",
                Exercise = "Plot.",
                Parameters = [ToolParameter.Text("system", "The system.", required: true)],
                Handler = (arguments, _) =>
                {
                    arguments.TryGetString("system", out var system);
                    plotted.Add(system ?? string.Empty);

                    return Task.FromResult(ToolResult.Ok("Course set."));
                },
            },
        ],
    };

    private static Page Open(bool spansh = true, GameStatus? target = null, bool seed = true)
    {
        var store = new BookmarkStore(
            Path.Combine(TestSurface.MemoryFolder("d47-bookmark-forms-tests"), "bookmarks.json"),
            new MemoryFileSystem(),
            NullLogger<BookmarkStore>.Instance);

        if (seed)
        {
            store.Add("F1", new Bookmark("Current CG", "Deciat", At, BookmarkKind.System, null, 100, "Hold here"));
            store.Add("F1", "Plain", "Sol", At.AddDays(-1));
        }

        var gameState = CommanderState();
        var plotted = new List<string>();
        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableRouting(
            new RoutingSurface(
                () => new NavRoute(),
                () => null,
                CapabilityRegistry.Build([Course(plotted)]),
                Bookmarks: store,
                Commander: () => gameState.Active,
                BookmarkPhrasesTaken: () => [],
                Status: () => target ?? GameStatus.Unknown)
            {
                StarSystems = spansh ? new Spansh() : null,
                SuggestionPause = TimeSpan.Zero,
            });

        var window = new Window { Content = panel, Width = 1100, Height = 900 };

        window.Show();
        panel.Tab = PanelTab.Navigation;
        panel.Nav.SelectRoot(BookmarksPage.RootKey);
        Dispatcher.UIThread.RunJobs();

        return new Page(window, panel, store, plotted);
    }

    private static Button Named(Page page, string label) =>
        page.Panel.GetVisualDescendants().OfType<Button>().First(button => (button.Content as string) == label);

    private static void Click(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static IReadOnlyList<string> Drawn(Page page) =>
        [.. page.Panel.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];

    private static TextBox Box(Page page, string name) =>
        page.Panel.GetVisualDescendants().OfType<TextBox>()
            .First(box => Avalonia.Automation.AutomationProperties.GetName(box) == name);

    private static void Until(Func<bool> done)
    {
        for (var i = 0; i < 100 && !done(); i++)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(20);
        }

        Dispatcher.UIThread.RunJobs();
        Assert.True(done());
    }

    private static void TypeLocation(Page page, string text)
    {
        Box(page, "Location").Text = text;
        Until(() => Rows(page).Count > 0);
    }

    private static IReadOnlyList<Button> Rows(Page page) =>
        [.. page.Panel.GetVisualDescendants()
            .OfType<Border>()
            .Where(border => border.Name == NameSuggestions.ListName)
            .SelectMany(border => border.GetVisualDescendants().OfType<Button>())];

    private static void Pick(Page page, string name) =>
        Click(Rows(page).First(row => Avalonia.Automation.AutomationProperties.GetName(row) == name));

    /// <summary>A row shows its note, or says there is none.</summary>
    [AvaloniaFact]
    public void ARowShowsItsNoteOrSaysThereIsNone()
    {
        var page = Open();

        Assert.Contains("Hold here", Drawn(page));
        Assert.Contains(BookmarksPage.NoNote, Drawn(page));

        page.Window.Close();
    }

    /// <summary>The lookup lists systems, bodies and stations, each tagged with its kind.</summary>
    [AvaloniaFact]
    public void TheLookupListsEachKindTagged()
    {
        var page = Open();

        Click(Named(page, "ADD"));
        TypeLocation(page, "Dec");

        var drawn = Drawn(page);

        Assert.Contains("SYSTEM", drawn);
        Assert.Contains("BODY", drawn);
        Assert.Contains("STATION", drawn);

        page.Window.Close();
    }

    /// <summary>A body match stores its kind, the body, and its system's name and address.</summary>
    [AvaloniaFact]
    public void ABodyMatchIsStoredWithItsSystem()
    {
        var page = Open();

        Click(Named(page, "ADD"));
        Assert.False(Named(page, "ADD").IsEnabled);

        TypeLocation(page, "Dec");
        Pick(page, "Deciat 6 a");
        Until(() => Named(page, "ADD").IsEnabled);

        Assert.Contains("Body in Deciat", Drawn(page));

        Click(Named(page, "ADD"));

        var made = Assert.Single(page.Store.For("F1"), bookmark => bookmark.Name == "Deciat 6 a");

        Assert.Equal(BookmarkKind.Body, made.Kind);
        Assert.Equal("Deciat 6 a", made.Place);
        Assert.Equal("Deciat", made.System);
        Assert.Equal(100, made.SystemAddress);

        page.Window.Close();
    }

    /// <summary>Saving an edit changes the location, name and note, and the list shows it.</summary>
    [AvaloniaFact]
    public void AnEditChangesTheNameAndNote()
    {
        var page = Open();

        Click(page.Panel.GetVisualDescendants().OfType<Button>()
            .First(button => Avalonia.Automation.AutomationProperties.GetName(button) == "Edit Plain"));

        Box(page, "Name").Text = "Outpost";
        Box(page, "Note").Text = "Where I start";
        Click(Named(page, "SAVE"));

        var saved = Assert.Single(page.Store.For("F1"), bookmark => bookmark.Name == "Outpost");

        Assert.Equal("Where I start", saved.Note);
        Assert.Equal("Sol", saved.System);
        Assert.Contains("Where I start", Drawn(page));

        page.Window.Close();
    }

    /// <summary>A name that collides is refused with the validation message and nothing changes.</summary>
    [AvaloniaFact]
    public void ACollidingNameIsRefusedUnderTheField()
    {
        var page = Open();

        Click(page.Panel.GetVisualDescendants().OfType<Button>()
            .First(button => Avalonia.Automation.AutomationProperties.GetName(button) == "Edit Plain"));

        Box(page, "Name").Text = "Current CG";
        Click(Named(page, "SAVE"));

        Assert.NotNull(BookmarkValidation.Problem("Current CG", ["Current CG"], []));
        Assert.Contains(BookmarkValidation.Problem("Current CG", ["Current CG"], [])!, Drawn(page));
        Assert.Contains(page.Store.For("F1"), bookmark => bookmark.Name == "Plain");

        page.Window.Close();
    }

    /// <summary>With no Spansh the location takes no lookups, and the target still fills it.</summary>
    [AvaloniaFact]
    public void WithoutSpanshTheTargetStillFillsTheLocation()
    {
        var page = Open(spansh: false, target: new GameStatus { Destination = new StatusDestination(100, 0, "Lave") });

        Click(Named(page, "ADD"));

        Assert.DoesNotContain(page.Panel.GetVisualDescendants().OfType<Border>(), border => border.Name == NameSuggestions.ListName);

        Click(Named(page, "USE MY TARGET"));
        Until(() => Named(page, "ADD").IsEnabled);

        Assert.Equal("Lave", Box(page, "Location").Text);

        Click(Named(page, "ADD"));

        Assert.Contains(page.Store.For("F1"), bookmark => bookmark.Name == "Lave" && bookmark.System == "Lave");

        page.Window.Close();
    }

    /// <summary>With nothing targeted, USE MY TARGET shows the refusal and sets no location.</summary>
    [AvaloniaFact]
    public void NothingTargetedIsShownAndSetsNoLocation()
    {
        var page = Open();

        Click(Named(page, "ADD"));
        Click(Named(page, "USE MY TARGET"));
        Until(() => Drawn(page).Contains(BookmarkPlace.NothingTargeted));

        Assert.False(Named(page, "ADD").IsEnabled);

        page.Window.Close();
    }

    /// <summary>SET COURSE plots to the bookmark's system and shows the reply under ADD.</summary>
    [AvaloniaFact]
    public void SetCoursePlotsToTheSystemAndShowsTheReply()
    {
        var page = Open();

        Click(Named(page, "SET COURSE"));
        Until(() => page.Plotted.Count > 0);

        Assert.Equal("Deciat", page.Plotted[0]);
        Until(() => Drawn(page).Contains("Course set."));

        page.Window.Close();
    }
}
