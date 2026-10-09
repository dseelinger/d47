using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
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

/// <summary>The Bookmarks page on the Routing tab: listing, renaming and deleting a bookmark (#490).</summary>
[Trait("Category", "Integration")]
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
        IReadOnlyCollection<string>? taken = null,
        D47.Core.Capabilities.Builtin.IClipboard? clipboard = null,
        GameStatus? target = null)
    {
        var root = TempFolders.Create("d47-bookmarks-page-tests");

        var store = new BookmarkStore(Path.Combine(root, "bookmarks.json"), new MemoryFileSystem(), NullLogger<BookmarkStore>.Instance);

        if (seed)
        {
            store.Add(fid, "Current CG", "Deciat", At);
        }

        var gameState = commanderKnown ? CommanderState(fid, "Jameson") : null;

        var panel = new PanelView { DataContext = new PanelViewModel() };

        var registry = CapabilityRegistry.Build(
        [
            BookmarksCapability.Create(
                store,
                () => fid,
                () => target ?? GameStatus.Unknown,
                () => null,
                () => PhraseBook.From(CapabilityRegistry.Build([]), []),
                () => At),
        ]);

        panel.EnableRouting(
            new RoutingSurface(
                () => new NavRoute(),
                () => null,
                registry,
                Bookmarks: store,
                Commander: () => gameState?.Active,
                BookmarkPhrasesTaken: () => taken ?? [],
                Clipboard: clipboard));

        var window = new Window { Content = panel, Width = 1100, Height = 900 };

        window.Show();

        panel.Tab = PanelTab.Navigation;
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

    /// <summary>Each row's glyph writes the bookmark's system (#864).</summary>
    [AvaloniaFact]
    public void ARowCopiesItsSystem()
    {
        var clipboard = new D47.Core.Capabilities.Builtin.RecordingClipboard();
        var surface = Open(clipboard: clipboard);

        var glyph = Assert.Single(
            surface.Panel.GetVisualDescendants().OfType<Button>(),
            button => D47.App.Controls.CopyGlyph.GetCopies(button) is not null);

        Click(glyph);

        Assert.Equal(["Deciat"], clipboard.Written);

        surface.Window.Close();
    }

    /// <summary>A surface with no clipboard draws no glyph (#864).</summary>
    [AvaloniaFact]
    public void ASurfaceWithNoClipboardDrawsNoGlyph()
    {
        var surface = Open();

        Assert.DoesNotContain(
            surface.Panel.GetVisualDescendants().OfType<Button>(),
            button => D47.App.Controls.CopyGlyph.GetCopies(button) is not null);

        surface.Window.Close();
    }

    private static GameStatus Targeting(string system) =>
        new() { Destination = new StatusDestination(1, 0, system) };

    private static void Type(Surface surface, string text)
    {
        var box = surface.Panel.GetVisualDescendants().OfType<TextBox>().Last();

        box.Text = text;
        Dispatcher.UIThread.RunJobs();

        Click(Named(surface.Panel, "Done"));
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Add with no name bookmarks the target under Elite's name, as "bookmark this" does (#540).</summary>
    [AvaloniaFact]
    public void AddWithNoNameBookmarksTheTargetUnderElitesName()
    {
        var surface = Open(target: Targeting("LTT 7786"));

        Click(Named(surface.Panel, "Add"));
        Type(surface, string.Empty);

        Assert.Equal("LTT 7786", surface.Store.Find("F1", "LTT 7786")?.System);
        Assert.Contains(Drawn(surface.Panel), text => text.Contains("LTT 7786", StringComparison.Ordinal));

        surface.Window.Close();
    }

    /// <summary>Add with a name gives the bookmark that name (#540).</summary>
    [AvaloniaFact]
    public void AddWithANameTakesThatName()
    {
        var surface = Open(target: Targeting("LTT 7786"));

        Click(Named(surface.Panel, "Add"));
        Type(surface, "Mining Spot");

        Assert.Equal("LTT 7786", surface.Store.Find("F1", "Mining Spot")?.System);

        surface.Window.Close();
    }

    /// <summary>A name that breaks the rules is refused in the prompt and nothing is made (#540).</summary>
    [AvaloniaFact]
    public void AddWithABadNameIsRefusedInThePrompt()
    {
        var surface = Open(target: Targeting("LTT 7786"));

        Click(Named(surface.Panel, "Add"));
        Type(surface, "Current CG");

        Assert.Null(surface.Store.Find("F1", "LTT 7786"));
        Assert.Contains(Drawn(surface.Panel), text => text.Contains("Current CG", StringComparison.Ordinal));
        Assert.Single(surface.Store.For("F1"));

        surface.Window.Close();
    }

    /// <summary>With nothing targeted, Add shows the tool's refusal and creates nothing (#540).</summary>
    [AvaloniaFact]
    public void AddWithNothingTargetedSaysSoAndMakesNothing()
    {
        var surface = Open();

        Click(Named(surface.Panel, "Add"));
        Type(surface, string.Empty);

        Assert.Contains(
            Drawn(surface.Panel),
            text => text.Contains("Nothing is targeted. Select a system or a station first.", StringComparison.Ordinal));

        Assert.Single(surface.Store.For("F1"));

        surface.Window.Close();
    }

    /// <summary>The empty state names the button as well as the phrase (#540).</summary>
    [AvaloniaFact]
    public void TheEmptyStateNamesTheAddButton()
    {
        var surface = Open(seed: false);

        Assert.Contains(Drawn(surface.Panel), text => text.Contains("press Add", StringComparison.Ordinal));
        Assert.NotNull(Named(surface.Panel, "Add"));

        surface.Window.Close();
    }

    /// <summary>The root is on the Routing tab, beside Course.</summary>
    [AvaloniaFact]
    public void TheTabHasABookmarksRoot()
    {
        var surface = Open();

        Assert.Contains(
            surface.Panel.Nav.Roots(PanelTab.Navigation),
            root => root.Key == BookmarksPage.RootKey && root.Word == "Bookmarks");

        surface.Window.Close();
    }

    /// <summary>The page lists the flying Commander's bookmarks, and not another Commander's.</summary>
    [AvaloniaFact]
    public void ThePageListsTheFlyingCommandersBookmarksAndNotAnothers()
    {
        var root = TempFolders.Create("d47-bookmarks-page-tests");
        var store = new BookmarkStore(Path.Combine(root, "bookmarks.json"), new MemoryFileSystem(), NullLogger<BookmarkStore>.Instance);

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

        panel.Tab = PanelTab.Navigation;
        Assert.True(panel.Nav.SelectRoot(BookmarksPage.RootKey));
        Dispatcher.UIThread.RunJobs();

        var drawn = Drawn(panel);

        Assert.Contains(drawn, text => text.Contains("CURRENT CG", StringComparison.Ordinal));
        Assert.DoesNotContain(drawn, text => text.Contains("SOMEWHERE ELSE", StringComparison.Ordinal));

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
        Assert.Contains(Drawn(surface.Panel), text => text.Contains("NEW NAME", StringComparison.Ordinal));

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

        Assert.DoesNotContain(Drawn(surface.Panel), text => text.Contains("FRESH BOOKMARK", StringComparison.Ordinal));

        surface.Store.Add("F1", "Fresh Bookmark", "Sol", At);
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(Drawn(surface.Panel), text => text.Contains("FRESH BOOKMARK", StringComparison.Ordinal));

        surface.Window.Close();
    }

    /// <summary>After leaving the page and coming back, a bookmark added to the store still appears.</summary>
    [AvaloniaFact]
    public void ABookmarkAddedAfterReturningToThePageAppears()
    {
        var surface = Open(seed: false);

        Assert.True(surface.Panel.Nav.SelectRoot(RoutingPages.PlanRoot));
        Dispatcher.UIThread.RunJobs();
        Assert.True(surface.Panel.Nav.SelectRoot(BookmarksPage.RootKey));
        Dispatcher.UIThread.RunJobs();

        surface.Store.Add("F1", "Fresh Bookmark", "Sol", At);
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(Drawn(surface.Panel), text => text.Contains("FRESH BOOKMARK", StringComparison.Ordinal));

        surface.Window.Close();
    }

    /// <summary>The page with three bookmarks, and the empty state, checked against a saved capture (#490).</summary>
    [AvaloniaFact]
    public void TheStatesAreCaptured()
    {
        var root = TempFolders.Create("d47-bookmarks-page-tests");
        var store = new BookmarkStore(Path.Combine(root, "bookmarks.json"), new MemoryFileSystem(), NullLogger<BookmarkStore>.Instance);

        store.Add("F1", "Current CG", "Deciat", At);
        store.Add("F1", "Home", "Shinrarta Dezhra", At.AddDays(-1));
        store.Add("F1", "Engineer", "Farseer Inc", At.AddDays(-2));

        var gameState = CommanderState("F1", "Jameson");

        using var full = AppLook.Capture(
            Full(store, () => gameState.Active),
            "bookmarks-page-three-bookmarks.png");

        using var empty = AppLook.Capture(
            Full(new BookmarkStore(
                Path.Combine(TempFolders.Create("d47-bookmarks-page-tests"), "bookmarks.json"),
                new MemoryFileSystem(),
                NullLogger<BookmarkStore>.Instance), () => gameState.Active),
            "bookmarks-page-empty.png");

        Assert.True(full.PixelSize.Width > 0);
        Assert.True(empty.PixelSize.Width > 0);
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

        panel.Tab = PanelTab.Navigation;
        Assert.True(panel.Nav.SelectRoot(BookmarksPage.RootKey));

        return panel;
    }
}
