using D47.Core.Storage;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core;
using D47.Core.Adventures;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>A system name drawn on the Adventures and Bookmarks pages carries a copy glyph (#864).</summary>
public class SystemNamesOnAdventuresAndBookmarksCopyTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 20, 0, 0, TimeSpan.Zero);

    private const string Far = "Dyson's Hollow";

    private sealed record Surface(Window Window, PanelView Panel, RecordingClipboard Clipboard);

    private static Adventure Story(string here) => new()
    {
        Key = "the-lantern-route",
        Name = "The Lantern Route",
        Source = AdventureSource.Commander,
        Spine = new AdventureSpine { Premise = "An outpost still runs a beacon." },
        Opening = "Beacons cost money.",
        Beats =
        [
            new AdventureBeat
            {
                Title = "The Lantern",
                Function = "setup",
                Trigger = new AdventureTrigger { Kind = TriggerKind.Arrive, System = Far },
                Line = "Scoop here.",
            },
            new AdventureBeat
            {
                Title = "The Anchorage",
                Function = "midpoint",
                Trigger = new AdventureTrigger { Kind = TriggerKind.Arrive, System = here },
                Line = "Dock.",
            },
        ],
        Told =
        [
            new AdventureTold
            {
                Kind = AdventureToldKind.Beat,
                Text = "Scoop here.",
                At = Now,
                Beat = 0,
                Trigger = $"arrive at {Far}",
                Title = "The Lantern",
            },
        ],
        AcceptedAt = Now.AddHours(-1),
    };

    private static CommanderGameState State()
    {
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{ "timestamp":"2026-08-22T19:00:00Z", "event":"Commander", "FID":"F1", "Name":"Jameson" }""",
                     """{ "timestamp":"2026-08-22T19:01:00Z", "event":"Location", "StarSystem":"Shinrarta Dezhra", "SystemAddress":3932277478106 }""",
                 })
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store.Active!;
    }

    private static Surface Open(bool copy = true)
    {
        var paths = new AppPaths(TempFolders.Create("d47-system-copy"));
        paths.EnsureCreated();

        var book = new AdventureBook(
            new AdventureStore(Path.Combine(paths.Data, "adventures.json"), new DiskFileSystem(), NullLogger<AdventureStore>.Instance),
            NullLogger<AdventureBook>.Instance);

        Assert.Null(book.Write("F1", Story("Shinrarta Dezhra")));

        var state = State();

        var generator = new AdventureGenerator(
            () => null, () => null, () => null, () => null, () => null, () => state,
            () => null, () => null, null, null, NullLogger.Instance);

        var surface = new AdventureSurface(
            book, generator, () => state, () => "F1", () => Now, _ => { }, () => false, () => false, () => null, () => { });

        var clipboard = new RecordingClipboard();
        var panel = new PanelView { DataContext = new PanelViewModel(), Mode = PanelMode.Full };

        if (copy)
        {
            panel.EnableCopy(clipboard);
        }

        panel.EnableAdventures(surface);

        var window = new Window { Content = panel, Width = 1280, Height = 860 };
        window.Show();

        panel.Tab = PanelTab.Stories;
        Dispatcher.UIThread.RunJobs();

        return new Surface(window, panel, clipboard);
    }

    private static List<Button> Glyphs(PanelView panel) =>
        [.. panel.GetVisualDescendants().OfType<Button>().Where(button => CopyGlyph.GetCopies(button) is not null)];

    private static void Click(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    [Trait("Category", "Integration")]
    [AvaloniaFact]
    public void TheCardCopiesTheNextSystemAndDoesNotOpenTheStory()
    {
        var surface = Open();

        var glyph = Assert.Single(Glyphs(surface.Panel));
        Assert.Equal(Far, CopyGlyph.GetCopies(glyph));

        Click(glyph);

        Assert.Equal([Far], surface.Clipboard.Written);
        Assert.True(surface.Panel.Nav.AtRoot);

        surface.Window.Close();
    }

    [Trait("Category", "Integration")]
    [AvaloniaFact]
    public void TheStoryPageCopiesTheNextSystemAndEachToldBeat()
    {
        var surface = Open();

        surface.Panel.Nav.Drill(new NavCrumb(AdventuresPage.ReadPrefix + "the-lantern-route", "The Lantern Route"));
        Dispatcher.UIThread.RunJobs();

        var glyphs = Glyphs(surface.Panel);

        Assert.Equal([Far, Far, Far], glyphs.Select(glyph => CopyGlyph.GetCopies(glyph)));

        Click(glyphs[0]);

        Assert.Equal([Far], surface.Clipboard.Written);

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void ASystemThatIsNotWhereTheCommanderIsStaysInA()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        Brush? Name(string? here) =>
            ((Run)((TextBlock)((WrapPanel)AdventuresPage.Trigger($"Arrive at {Far}.", here, Far, _ => Task.FromResult(true)))
                .Children[0]).Inlines![1]).Foreground as Brush;

        var cyan = Assert.IsAssignableFrom<ISolidColorBrush>(Name(Far));
        var plain = Name("Shinrarta Dezhra") as ISolidColorBrush;

        Assert.NotEqual(cyan.Color, plain?.Color);
    }

    [AvaloniaFact]
    public void TheSystemTheCommanderIsInStaysCyanBesideItsGlyph()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var trigger = Assert.IsType<WrapPanel>(
            AdventuresPage.Trigger("Arrive at Sol.", "Sol", "Sol", _ => Task.FromResult(true)));

        var head = Assert.IsType<TextBlock>(trigger.Children[0]);
        Assert.IsAssignableFrom<ISolidColorBrush>(((Run)head.Inlines![1]).Foreground);
        Assert.IsType<Button>(trigger.Children[1]);
    }

    [AvaloniaFact]
    public void TheTextOnEitherSideOfAStoriesGlyphIsCentredOnIt()
    {
        var trigger = Assert.IsType<WrapPanel>(
            AdventuresPage.Trigger($"Arrive at {Far}.", "Sol", Far, _ => Task.FromResult(true)));

        var glyph = Assert.IsType<Button>(trigger.Children[1]);
        Assert.Equal(VerticalAlignment.Center, glyph.VerticalAlignment);
        Assert.Equal(VerticalAlignment.Center, Assert.IsType<TextBlock>(trigger.Children[0]).VerticalAlignment);
        Assert.Equal(VerticalAlignment.Center, Assert.IsType<TextBlock>(trigger.Children[2]).VerticalAlignment);
    }

    [AvaloniaFact]
    public void ASentenceThatDoesNotNameItsSystemDrawsNoGlyph()
    {
        Assert.IsType<TextBlock>(
            AdventuresPage.Trigger("Kill bonds: 2 of 3.", null, Far, _ => Task.FromResult(true)));

        Assert.IsType<TextBlock>(
            AdventuresPage.Trigger("Reach rank Scout.", null, null, _ => Task.FromResult(true)));
    }

    [Trait("Category", "Integration")]
    [AvaloniaFact]
    public void ASurfaceWithNoCopyDrawsNoGlyphOnAdventures()
    {
        var surface = Open(copy: false);

        Assert.Empty(Glyphs(surface.Panel));

        surface.Window.Close();
    }
}
