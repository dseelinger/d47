using D47.Core.Storage;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core;
using D47.Core.Adventures;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The Adventures tab, a story's reading level, the editor and the ask form, rendered on each theme and at
/// the three panel sizes and saved to <see cref="TestSurface.CaptureDirectory"/> for comparison with
/// brief 03 (#404).
/// </summary>
[Trait("Category", "Integration")]
public class TheAdventuresScreenIsDrawnOnTheKitTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 20, 0, 0, TimeSpan.Zero);

    private const string Here = "Shinrarta Dezhra";

    private static CommanderGameState State()
    {
        return JournalScript.State(
            """{ "timestamp":"2026-08-22T19:00:00Z", "event":"Commander", "FID":"F1", "Name":"Jameson" }""",
            """{ "timestamp":"2026-08-22T19:01:00Z", "event":"Location", "StarSystem":"Shinrarta Dezhra", "SystemAddress":3932277478106, "Docked":true, "StationName":"Jameson Memorial", "StationType":"Coriolis", "MarketID":128666762 }""");
    }

    /// <summary>A live story whose next beat is in the Commander's own system.</summary>
    private static Adventure Live() => new()
    {
        Key = "the-lantern-route",
        Name = "The Lantern Route",
        Source = AdventureSource.Commander,
        Spine = new AdventureSpine { Premise = "An outpost abandoned in 3302 still runs a beacon." },
        Opening = "Beacons cost money.",
        Beats =
        [
            new AdventureBeat
            {
                Title = "The Lantern",
                Function = "setup",
                Trigger = new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = 3932277478106, System = Here },
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
        AcceptedAt = Now.AddHours(-1),
    };

    private static Adventure Draft() => Live() with
    {
        Key = "the-draft",
        Name = "The Unrecoverable Column",
        Source = AdventureSource.Generated,
        WrittenBy = "archivist",
        Spine = new AdventureSpine { Premise = "A ledger will not balance." },
        AcceptedAt = null,
    };

    private static Adventure Abandoned() => Live() with
    {
        Key = "the-abandoned",
        Name = "The Quiet Relay",
        AbandonedAt = Now.AddMinutes(-30),
    };

    private sealed record Surface(Window Window, PanelView Panel);

    private static Surface Open(double width, double height)
    {
        var paths = new AppPaths(TempFolders.Create("d47-adventures-capture"));
        paths.EnsureCreated();

        var book = new AdventureBook(
            new AdventureStore(Path.Combine(paths.Data, "adventures.json"), new DiskFileSystem(), NullLogger<AdventureStore>.Instance),
            NullLogger<AdventureBook>.Instance);

        foreach (var adventure in new[] { Live(), Draft(), Abandoned() })
        {
            Assert.Null(book.Write("F1", adventure));
        }

        var state = State();

        var generator = new AdventureGenerator(
            () => null, () => null, () => null, () => null, () => null, () => state,
            () => null, () => null, null, null, NullLogger.Instance);

        var surface = new AdventureSurface(
            book, generator, () => state, () => "F1", () => Now, _ => { }, () => false, () => null, () => { });

        var panel = new PanelView
        {
            DataContext = new PanelViewModel(),
            Mode = height < 400 ? PanelMode.Mini : PanelMode.Full,
        };

        panel.EnableAdventures(surface);

        var window = new Window { Content = panel, Width = width, Height = height };
        window.Show();

        panel.Tab = PanelTab.Stories;
        Dispatcher.UIThread.RunJobs();

        return new Surface(window, panel);
    }

    [Trait("Category", "Integration")]
    [AvaloniaTheory]
    [MemberData(nameof(CaptureMatrix.Rows), MemberType = typeof(CaptureMatrix))]
    public void TheAdventuresScreensAreCaptured(string themeId, double width, double height)
    {
        using var look = AppLook.PutForCapture(themeId);

        var surface = Open(width, height);
        var panel = surface.Panel;
        var saved = new List<string> { surface.Window.SaveFrame($"adventures-{themeId}-{width}x{height}.png") };

        if (panel.Mode == PanelMode.Full)
        {
            foreach (var (key, label, name) in new[]
                     {
                         (AdventuresPage.ReadPrefix + "the-lantern-route", "The Lantern Route", "reading"),
                         (AdventuresPage.ReadPrefix + "the-draft", "The Unrecoverable Column", "reading-draft"),
                         (AdventuresPage.AskKey, "Ask", "ask"),
                     })
            {
                panel.Nav.ToRoot();
                panel.Nav.GoTo(new NavCrumb(key, label));
                saved.Add(surface.Window.SaveFrame($"adventures-{name}-{themeId}-{width}x{height}.png"));
            }
        }

        surface.Window.Close();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>The Commander's current system is Cyan inside a trigger, and the rest of the trigger A.</summary>
    [Trait("Category", "Integration")]
    [AvaloniaFact]
    public void TheCurrentSystemIsCyanInATrigger()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var surface = Open(1280, 860);

        var trigger = surface.Panel.GetVisualDescendants().OfType<TextBlock>()
            .Single(block => block.Inlines is { Count: 3 } runs && ((Run)runs[1]).Text == Here);

        Assert.Equal(Ink(ThemeManager.AKey), (trigger.Foreground as ISolidColorBrush)?.Color);
        Assert.Equal(Ink(ThemeManager.CyanKey), (((Run)trigger.Inlines![1]).Foreground as ISolidColorBrush)?.Color);
        Assert.Equal("Next: arrive at ", ((Run)trigger.Inlines[0]).Text);

        surface.Window.Close();
    }

    /// <summary>A system name matches only as a whole name, so "Sol" is not found inside "Solati".</summary>
    [Fact]
    public void ASystemIsFoundOnlyAsAWholeName()
    {
        Assert.Equal(10, AdventuresPage.Naming("arrive at Sol", "sol"));
        Assert.Null(AdventuresPage.Naming("arrive at Solati", "Sol"));
        Assert.Equal(18, AdventuresPage.Naming("dock at Solati in Sol.", "Sol"));
        Assert.Null(AdventuresPage.Naming("arrive at Sol", null));
    }

    /// <summary>Stories are list rows with no border, and Decline and Remove are destructive.</summary>
    [Trait("Category", "Integration")]
    [AvaloniaFact]
    public void StoriesAreListRowsAndRemovingIsDestructive()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var surface = Open(1280, 860);
        var panel = surface.Panel;

        var rows = panel.GetVisualDescendants().OfType<Border>()
            .Where(border => border.Classes.Contains(ListRow.Class))
            .ToList();

        Assert.Equal(2, rows.Count);
        Assert.All(rows, row => Assert.Equal(default, row.BorderThickness));
        Assert.All(rows, row => Assert.Equal(Ink(ThemeManager.TileKey), (row.Background as ISolidColorBrush)?.Color));

        Assert.Contains("destructive", panel.GetVisualDescendants().OfType<Button>()
            .Single(button => Equals(button.Content, "Decline")).Classes);

        panel.Nav.GoTo(new NavCrumb(AdventuresPage.ReadPrefix + "the-lantern-route", "The Lantern Route"));
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("destructive", panel.GetVisualDescendants().OfType<Button>()
            .Single(button => Equals(button.Content, "Remove")).Classes);

        surface.Window.Close();
    }

    private static Color Ink(string key) =>
        ((ISolidColorBrush)Avalonia.Application.Current!.Resources[key]!).Color;

    [Theory]
    [InlineData("AdventuresPage.cs")]
    [InlineData("AdventureMini.cs")]
    [InlineData("AdventureSurface.cs")]
    [InlineData("AdventureThinking.cs")]
    [InlineData("ToldEntries.cs")]
    [Trait("Category", "Gate")]
    public void TheAdventuresSourceDrawsOnlyInTheNewTokens(string file)
    {
        var source = File.ReadAllText(Path.Combine(AppSource.RepositoryRoot(), "src", "D47.App", "Panel", file));

        KitTokens.AssertDrawsOnlyInTheNewTokens(source, new KitTokenRules(true, false, true));
    }
}
