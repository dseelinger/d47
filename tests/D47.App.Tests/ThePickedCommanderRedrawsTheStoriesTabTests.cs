using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core;
using D47.Core.Adventures;
using D47.Core.Interface;
using D47.Core.Storage;
using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Picking another Commander redraws the Stories tab for them, and a prompt left open for the last one does nothing.</summary>
[Trait("Category", "Integration")]
public class ThePickedCommanderRedrawsTheStoriesTabTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 20, 0, 0, TimeSpan.Zero);

    private static readonly StoryCard Switched = StoryFixture.Story with { Id = "story-x", Number = 1, Title = "Story X" };
    private static readonly StoryCard Wanted = StoryFixture.Story with { Id = "story-y", Number = 2, Title = "Story Y" };
    private static readonly StoryCard Held = StoryFixture.Story with { Id = "story-z", Number = 3, Title = "Story Z" };

    private sealed class Tab
    {
        public Window Window { get; set; } = null!;

        public PanelView Panel { get; set; } = null!;

        public StoryStore Stories { get; set; } = null!;

        public CommanderSignal Signal { get; set; } = null!;

        public string Shown { get; set; } = "A";

        public void Switch(string commander)
        {
            Shown = commander;
            Signal.Raise();
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static Tab Open()
    {
        var paths = new AppPaths(TempFolders.Create("d47-commander-redraw"));
        paths.EnsureCreated();

        var cards = new[] { Switched, Wanted, Held };
        var catalog = new StoryCatalog(cards, () => [.. cards.Select(card => StoryFixture.Secret with { Id = card.Id })]);

        var book = new AdventureBook(
            new AdventureStore(Path.Combine(paths.Data, "adventures.json"), new DiskFileSystem(), NullLogger<AdventureStore>.Instance),
            NullLogger<AdventureBook>.Instance);
        var stories = StoryStore.InMemory();
        var generator = new AdventureGenerator(
            () => null, () => null, () => null, () => null, () => null, () => null,
            () => null, () => null, null, null, NullLogger.Instance);
        var director = new StoryDirector(
            stories, book, () => catalog, generator.GenerateAsync, () => null, _ => { }, NullLogger.Instance);

        stories.Save("A", new Story { Id = Switched.Id, Title = Switched.Title, PublicLayer = string.Empty });
        stories.Save("B", new Story { Id = Held.Id, Title = Held.Title, PublicLayer = string.Empty });

        var signal = new CommanderSignal();
        var tab = new Tab { Stories = stories, Signal = signal };
        var surface = new AdventureSurface(
            book, generator, () => null, () => tab.Shown, () => Now, _ => { }, () => true, () => true, () => null, () => { },
            Stories: director, Switched: signal,
            Messages: new D47.Core.Messages.MessageStore(Path.Combine(paths.Data, "messages.json"), new MemoryFileSystem(), NullLogger<D47.Core.Messages.MessageStore>.Instance));

        var panel = new PanelView { DataContext = new PanelViewModel(), Mode = PanelMode.Full };
        panel.EnableAdventures(surface);

        var window = new Window { Content = panel, Width = 1280, Height = 860 };
        window.Show();

        panel.Tab = PanelTab.Stories;
        panel.Nav.GoTo(new NavCrumb(StoriesView.RootKey, "Stories"));
        Dispatcher.UIThread.RunJobs();

        tab.Window = window;
        tab.Panel = panel;
        return tab;
    }

    /// <summary>Presses the first visible button labelled <paramref name="label"/>, or the last when <paramref name="answeringPrompt"/>, since the prompt layer is drawn after the page.</summary>
    private static void Press(PanelView panel, string label, bool answeringPrompt = false)
    {
        var buttons = panel.GetVisualDescendants().OfType<Button>().Where(candidate =>
            candidate.IsEffectivelyVisible
            && (candidate.Content as string == label
                || candidate.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == label))).ToList();
        var button = answeringPrompt ? buttons[^1] : buttons[0];

        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static List<string> Drawn(PanelView panel) =>
        [.. panel.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible).Select(block => block.Text ?? string.Empty)];

    [AvaloniaFact]
    public void TheRootBarReadsStoriesShortStoryStoryLog()
    {
        var tab = Open();

        tab.Panel.Nav.GoTo(new NavCrumb(AdventuresPage.RootKey, "Stories"));
        Dispatcher.UIThread.RunJobs();

        var bar = tab.Panel.GetVisualDescendants().OfType<Button>()
            .Where(button => button.IsEffectivelyVisible)
            .Select(button => button.Content as string ?? string.Empty)
            .Where(label => label is "Stories" or "Short Story" || label.StartsWith("Story log", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(3, bar.Count);
        Assert.Equal(["Stories", "Short Story"], bar.Take(2));
        Assert.Equal("Story log", bar[2]);

        tab.Window.Close();
    }

    [AvaloniaFact]
    public void ASwitchConfirmedAfterTheCommanderChangedDoesNothing()
    {
        var tab = Open();

        tab.Panel.Nav.Drill(new NavCrumb(StoriesView.ReadPrefix + Wanted.Id, Wanted.Title));
        Dispatcher.UIThread.RunJobs();
        Press(tab.Panel, "Switch");

        tab.Switch("B");

        Press(tab.Panel, "Switch", answeringPrompt: true);

        // The switch runs on the pool, so a regression lands after the click returns.
        for (var waited = 0; waited < 25; waited++)
        {
            Thread.Sleep(20);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.Equal(Switched.Id, tab.Stories.Current("A")!.Id);
        Assert.Equal(Held.Id, tab.Stories.Current("B")!.Id);
        Assert.Contains(StoriesView.CommanderChangedRefusal, Drawn(tab.Panel));

        tab.Window.Close();
    }

    [AvaloniaFact]
    public void AStoryPageIsRedrawnForTheCommanderNowShown()
    {
        var tab = Open();

        tab.Panel.Nav.Drill(new NavCrumb(StoriesView.ReadPrefix + Wanted.Id, Wanted.Title));
        Dispatcher.UIThread.RunJobs();

        tab.Switch("B");
        Press(tab.Panel, "Switch");

        Assert.Contains($"Switch from {Held.Title} to {Wanted.Title}?", Drawn(tab.Panel));

        tab.Window.Close();
    }

    [AvaloniaFact]
    public void AChapterPageIsClosedWhenTheCommanderChanges()
    {
        var tab = Open();

        tab.Panel.Nav.Drill(new NavCrumb(AdventuresPage.ReadPrefix + "chapter-one", "Chapter one"));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(AdventuresPage.ReadPrefix + "chapter-one", tab.Panel.Nav.TrailOf(PanelTab.Stories)[^1].Key);

        tab.Switch("B");

        Assert.Equal([AdventuresPage.RootKey], tab.Panel.Nav.TrailOf(PanelTab.Stories).Select(crumb => crumb.Key));

        tab.Window.Close();
    }

    [AvaloniaFact]
    public void AChapterPageIsClosedWhenTheCommanderChangesOnAnotherTab()
    {
        var tab = Open();

        tab.Panel.Nav.Drill(new NavCrumb(AdventuresPage.ReadPrefix + "chapter-one", "Chapter one"));
        Dispatcher.UIThread.RunJobs();
        Assert.True(tab.Panel.Nav.Select(PanelTab.Transcript));

        tab.Switch("B");

        Assert.Equal([AdventuresPage.RootKey], tab.Panel.Nav.TrailOf(PanelTab.Stories).Select(crumb => crumb.Key));

        tab.Window.Close();
    }
}
