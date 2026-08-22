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

    private static (PanelView Panel, AdventureBook Book, List<string> Said) Open(params Adventure[] adventures)
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
            () => null, () => null, () => null, () => null, () => null, () => state,
            () => null, () => null, null, null, NullLogger.Instance);

        var surface = new AdventureSurface(
            book,
            generator,
            () => state,
            () => "F1",
            () => Now,
            said.Add,
            () => false,
            () => false,
            () => null,
            () => { });

        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableAdventures(surface);

        var window = new Window { Content = panel, Width = 900, Height = 700 };
        window.Show();

        panel.Tab = PanelTab.Adventures;
        Dispatcher.UIThread.RunJobs();

        return (panel, book, said);
    }

    private static IReadOnlyList<string> Drawn(PanelView panel) =>
        [.. panel.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];

    [AvaloniaFact]
    public void TheRootDrawsCardsWithoutCounts()
    {
        var (panel, _, _) = Open(Story(Now.AddHours(-1)), Story(null, AdventureSource.Generated) with { Key = "draft", Name = "The Draft" });

        var drawn = Drawn(panel);

        Assert.Contains(drawn, text => text.Contains("The Lantern Route"));
        Assert.Contains(drawn, text => text.Contains("yours") && text.Contains("The Lantern"));
        Assert.Contains(drawn, text => text.Contains("written by Archivist") && text.Contains("waiting for your yes"));
        Assert.DoesNotContain(drawn, text => text.Contains(" of 2"));
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

        panel.Nav.GoTo([new NavCrumb(AdventuresPage.RootKey, "Adventures"), new NavCrumb(AdventuresPage.ReadPrefix + "the-lantern-route", "The Lantern Route")]);
        Dispatcher.UIThread.RunJobs();

        var drawn = Drawn(panel);

        Assert.Contains(drawn, text => text.Contains("Scoop here."));
        Assert.Contains(drawn, text => text.Contains("Waiting to dock at Maren Anchorage"));
        Assert.DoesNotContain(drawn, text => text.Contains("To one name."));
        Assert.DoesNotContain(drawn, text => text.Contains("Forty kilometres"));
        Assert.Contains(panel.GetVisualDescendants().OfType<Button>(), button => Equals(button.Content, "Abandon"));
    }

    [AvaloniaFact]
    public void TheEditorBuildsForANewStoryAndPrintsWhyBeginIsShut()
    {
        var (panel, _, _) = Open();

        panel.Nav.GoTo([new NavCrumb(AdventuresPage.RootKey, "Adventures"), new NavCrumb(AdventuresPage.EditPrefix + AdventuresPage.NewKey, "Write")]);
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

        panel.Nav.GoTo([new NavCrumb(AdventuresPage.RootKey, "Adventures"), new NavCrumb(AdventuresPage.AskKey, "Ask")]);
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

        panel.Nav.GoTo([new NavCrumb(AdventuresPage.RootKey, "Adventures"), new NavCrumb(AdventuresPage.ReadPrefix + "the-lantern-route", "The Lantern Route")]);
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
}
