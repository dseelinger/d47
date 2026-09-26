using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Conversation;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Bookmarks page on the Routing tab: listing, renaming and deleting a bookmark (#490).</summary>
public class TheBookmarksPageTests
{
    private static readonly DateTimeOffset At = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

    private sealed record Surface(Window Window, PanelView Panel, BookmarkStore Store);

    private static GameStateStore CommanderState(string fid, string name)
    {
        var gameState = new GameStateStore();

        var line = "{\"timestamp\":\"2026-08-25T09:00:00Z\",\"event\":\"Commander\",\"FID\":\""
            + fid + "\",\"Name\":\"" + name + "\"}";

        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var commander));

        gameState.Apply(commander!);

        return gameState;
    }

    private static Surface Open(
        bool seed = true,
        string fid = "F1",
        bool commanderKnown = true,
        IReadOnlyCollection<string>? taken = null)
    {
        var root = TempFolders.Create("d47-bookmarks-page-tests");

        var store = new BookmarkStore(Path.Combine(root, "bookmarks.json"), NullLogger<BookmarkStore>.Instance);

        if (seed)
        {
            store.Add(fid, "Current CG", "Deciat", At);
        }

        var gameState = commanderKnown ? CommanderState(fid, "Jameson") : null;

        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableRouting(
            new RoutingSurface(
                () => new NavRoute(),
                () => null,
                Bookmarks: store,
                Commander: () => gameState?.Active,
                BookmarkPhrasesTaken: () => taken ?? []));

        var window = new Window { Content = panel, Width = 1100, Height = 900 };

        window.Show();

        panel.Tab = PanelTab.Routing;
        panel.Nav.SelectRoot(BookmarksPage.RootKey);
        Dispatcher.UIThread.RunJobs();

        return new Surface(window, panel, store);
    }

    private static IReadOnlyList<string> Drawn(PanelView panel) =>
        [.. panel.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];

    private static Button Named(PanelView panel, string label) =>
        panel.GetVisualDescendants()
            .OfType<Button>()
            .First(button => (button.Content as string) == label);

    private static void Click(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>The root is on the Routing tab, beside Course.</summary>
    [AvaloniaFact]
    public void TheTabHasABookmarksRoot()
    {
        var surface = Open();

        Assert.Contains(
            surface.Panel.Nav.Roots(PanelTab.Routing),
            root => root.Key == BookmarksPage.RootKey && root.Word == "Bookmarks");

        surface.Window.Close();
    }

    /// <summary>The page lists the flying Commander's bookmarks, and not another Commander's.</summary>
    [AvaloniaFact]
    public void ThePageListsTheFlyingCommandersBookmarksAndNotAnothers()
    {
        var root = TempFolders.Create("d47-bookmarks-page-tests");
        var store = new BookmarkStore(Path.Combine(root, "bookmarks.json"), NullLogger<BookmarkStore>.Instance);

        store.Add("F1", "Current CG", "Deciat", At);
        store.Add("F2", "Somewhere Else", "Sol", At);

        var gameState = CommanderState("F1", "Jameson");

        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableRouting(
            new RoutingSurface(
                () => new NavRoute(),
                () => null,
                Bookmarks: store,
                Commander: () => gameState.Active,
                BookmarkPhrasesTaken: () => []));

        var window = new Window { Content = panel, Width = 1100, Height = 900 };

        window.Show();

        panel.Tab = PanelTab.Routing;
        Assert.True(panel.Nav.SelectRoot(BookmarksPage.RootKey));
        Dispatcher.UIThread.RunJobs();

        var drawn = Drawn(panel);

        Assert.Contains(drawn, text => text.Contains("Current CG", StringComparison.Ordinal));
        Assert.DoesNotContain(drawn, text => text.Contains("Somewhere Else", StringComparison.Ordinal));

        window.Close();
    }

    /// <summary>With no bookmarks, the page gives the sentence "list_bookmarks" uses rather than an empty list.</summary>
    [AvaloniaFact]
    public void WithNoBookmarksThePageSaysHowToMakeOne()
    {
        var surface = Open(seed: false);

        Assert.Contains(
            Drawn(surface.Panel),
            text => text.Contains("Say 'bookmark this'", StringComparison.Ordinal));

        surface.Window.Close();
    }

    /// <summary>With no Commander known, the page says so rather than showing an empty list.</summary>
    [AvaloniaFact]
    public void WithNoCommanderKnownThePageSaysSo()
    {
        var surface = Open(seed: false, commanderKnown: false);

        Assert.Contains(
            Drawn(surface.Panel),
            text => text.Contains("Nobody is flying", StringComparison.Ordinal));

        surface.Window.Close();
    }

    /// <summary>A rename to a taken phrase is refused, naming the phrase, and the bookmark keeps its name.</summary>
    [AvaloniaFact]
    public void ARenameToATakenPhraseIsRefusedAndTheNameSticks()
    {
        var surface = Open(taken: ["set course for taken name"]);

        Click(Named(surface.Panel, "Rename"));

        var box = surface.Panel.GetVisualDescendants()
            .OfType<TextBox>()
            .First(candidate => candidate.Text == "Current CG");

        box.Text = "Taken Name";
        Dispatcher.UIThread.RunJobs();

        Click(Named(surface.Panel, "Done"));
        Dispatcher.UIThread.RunJobs();

        var state = surface.Panel.GetVisualDescendants()
            .OfType<TextBlock>()
            .FirstOrDefault(block => (block.Text ?? string.Empty).Contains(
                "taken name", StringComparison.OrdinalIgnoreCase));

        Assert.True(state is not null, $"drawn: {string.Join(" | ", Drawn(surface.Panel))}");
        Assert.NotNull(surface.Store.Find("F1", "Current CG"));

        surface.Window.Close();
    }

    /// <summary>A rename succeeds and the store carries the new name.</summary>
    [AvaloniaFact]
    public void ARenameSucceeds()
    {
        var surface = Open();

        Click(Named(surface.Panel, "Rename"));

        var box = surface.Panel.GetVisualDescendants()
            .OfType<TextBox>()
            .First(candidate => candidate.Text == "Current CG");

        box.Text = "New Name";
        Dispatcher.UIThread.RunJobs();

        Click(Named(surface.Panel, "Done"));

        Assert.Null(surface.Store.Find("F1", "Current CG"));
        Assert.NotNull(surface.Store.Find("F1", "New Name"));
        Assert.Contains(Drawn(surface.Panel), text => text.Contains("New Name", StringComparison.Ordinal));

        surface.Window.Close();
    }

    /// <summary>Delete removes the row and the bookmark from the store.</summary>
    [AvaloniaFact]
    public void DeleteRemovesTheRow()
    {
        var surface = Open();

        Click(Named(surface.Panel, "Delete"));

        Assert.Null(surface.Store.Find("F1", "Current CG"));
        Assert.Contains(Drawn(surface.Panel), text => text.Contains("Say 'bookmark this'", StringComparison.Ordinal));

        surface.Window.Close();
    }

    /// <summary>A bookmark added to the store while the page is open appears without navigating away.</summary>
    [AvaloniaFact]
    public void ABookmarkAddedWhileThePageIsOpenAppears()
    {
        var surface = Open(seed: false);

        Assert.DoesNotContain(Drawn(surface.Panel), text => text.Contains("Fresh Bookmark", StringComparison.Ordinal));

        surface.Store.Add("F1", "Fresh Bookmark", "Sol", At);
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(Drawn(surface.Panel), text => text.Contains("Fresh Bookmark", StringComparison.Ordinal));

        surface.Window.Close();
    }

    /// <summary>After leaving the page and coming back, a bookmark added to the store still appears.</summary>
    [AvaloniaFact]
    public void ABookmarkAddedAfterReturningToThePageAppears()
    {
        var surface = Open(seed: false);

        Assert.True(surface.Panel.Nav.SelectRoot(RoutingPages.CourseRoot));
        Dispatcher.UIThread.RunJobs();
        Assert.True(surface.Panel.Nav.SelectRoot(BookmarksPage.RootKey));
        Dispatcher.UIThread.RunJobs();

        surface.Store.Add("F1", "Fresh Bookmark", "Sol", At);
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(Drawn(surface.Panel), text => text.Contains("Fresh Bookmark", StringComparison.Ordinal));

        surface.Window.Close();
    }

    /// <summary>The page with three bookmarks, and the empty state, checked against a saved capture (#490).</summary>
    [AvaloniaFact]
    public void TheStatesAreCaptured()
    {
        var root = TempFolders.Create("d47-bookmarks-page-tests");
        var store = new BookmarkStore(Path.Combine(root, "bookmarks.json"), NullLogger<BookmarkStore>.Instance);

        store.Add("F1", "Current CG", "Deciat", At);
        store.Add("F1", "Home", "Shinrarta Dezhra", At.AddDays(-1));
        store.Add("F1", "Engineer", "Farseer Inc", At.AddDays(-2));

        var gameState = CommanderState("F1", "Jameson");

        var full = AppLook.Capture(
            Full(store, () => gameState.Active),
            "bookmarks-page-three-bookmarks.png");

        var empty = AppLook.Capture(
            Full(new BookmarkStore(
                Path.Combine(TempFolders.Create("d47-bookmarks-page-tests"), "bookmarks.json"),
                NullLogger<BookmarkStore>.Instance), () => gameState.Active),
            "bookmarks-page-empty.png");

        Assert.True(File.Exists(full));
        Assert.True(File.Exists(empty));
    }

    private static PanelView Full(BookmarkStore store, Func<CommanderGameState?> commander)
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableRouting(
            new RoutingSurface(
                () => new NavRoute(),
                () => null,
                Bookmarks: store,
                Commander: commander,
                BookmarkPhrasesTaken: () => []));

        panel.Tab = PanelTab.Routing;
        Assert.True(panel.Nav.SelectRoot(BookmarksPage.RootKey));

        return panel;
    }
}
