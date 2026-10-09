using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Capabilities.Builtin;
using D47.Core.Checklists;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Ships;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// One copy glyph draws every system name the panel offers, through the shared control on the
/// <see cref="D47.Core.Capabilities.Builtin.IClipboard"/> seam, rather than three mechanisms of
/// their own (#157).
/// </summary>
public class OneCopyGlyphIsTheOnlyClipboardMechanismTests
{
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "d47.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }

    /// <summary>
    /// The pages that drew a system name never reach for a mechanism of their own: no glyph text, no
    /// bare copy mark, and no clipboard read straight off the
    /// visual root. The Transcript bar's copy glyph copies the whole page rather than a system name and
    /// is not scanned here.
    /// </summary>
    [Fact]
    [Trait("Category", "Gate")]
    public void NoPageThatDrawsASystemNameReachesForItsOwnClipboardMechanism()
    {
        var folder = Path.Combine(RepositoryRoot(), "src", "D47.App", "Panel");

        foreach (var name in new[]
                 {
                     "LoadoutPages.cs",
                     "RoutePlanResultPage.cs",
                     "RouteProgressPage.cs",
                     "RouteMarketPage.cs",
                     "CarrierPage.cs",
                     "EngineersPages.cs",
                     "RoutingPages.cs",
                 })
        {
            var source = File.ReadAllText(Path.Combine(folder, name));

            Assert.DoesNotContain("⧉", source);
            Assert.DoesNotContain("Glyphs.Copy", source, StringComparison.Ordinal);
            Assert.DoesNotContain("TopLevel.GetTopLevel", source, StringComparison.Ordinal);
        }
    }

    private static CommanderGameState Flying()
    {
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"StoredShips","StarSystem":"Shinrarta Dezhra","StationName":"Jameson Memorial","ShipsHere":[{"ShipID":7,"ShipType":"anaconda","ShipType_Localised":"Anaconda","Name":"Big Slow","Value":150000000}],"ShipsRemote":[]}""",
                     """{"timestamp":"2026-08-18T09:00:00Z","event":"Loadout","Ship":"python","ShipID":12,"ShipName":"Bad Idea","ShipIdent":"BI-01","MaxJumpRange":34.25,"Modules":[]}""",
                 })
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store.Active!;
    }

    private static (Window Window, PanelView Panel, RecordingClipboard Clipboard) Open(bool enableCopy)
    {
        var root = TempFolders.Create("d47-copy-glyph-tests");

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(root, "checklist.json"), new MemoryFileSystem(), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(root, "checklist-proposals.json"),
                new MemoryFileSystem(),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        var store = new ShipBuildStore(Path.Combine(root, "ships.json"), new MemoryFileSystem(), NullLogger<ShipBuildStore>.Instance);
        var state = Flying();
        var ships = new ShipPlanService(store, checklists, () => state);
        var clipboard = new RecordingClipboard();

        var panel = new PanelView { DataContext = new PanelViewModel() };

        if (enableCopy)
        {
            panel.EnableCopy(clipboard);
        }

        panel.EnableLoadout(ships, checklists, () => state);

        var window = new Window { Content = panel, Width = 900, Height = 700 };
        window.Show();

        panel.Tab = PanelTab.Assets;
        Dispatcher.UIThread.RunJobs();

        return (window, panel, clipboard);
    }

    private static Button Row(PanelView panel, string label) =>
        panel.GetVisualDescendants().OfType<Button>()
            .First(button => AutomationProperties.GetName(button) == label
                             || button.GetVisualDescendants().OfType<TextBlock>()
                                 .Any(text => text.Text == label));

    /// <summary>A surface with no <c>EnableCopy</c> draws no glyph and no page throws.</summary>
    [Trait("Category", "Integration")]
    [AvaloniaFact]
    public void ASurfaceWithNoEnableCopyDrawsNoGlyphAndDoesNotThrow()
    {
        var (window, panel, _) = Open(enableCopy: false);

        var exception = Record.Exception(() =>
        {
            Row(panel, "Big Slow (Anaconda)").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
        });

        Assert.Null(exception);

        Assert.Empty(SystemGlyphs(panel));

        window.Close();
    }

    /// <summary>
    /// The copy glyphs beside system names. The Transcript bar's own copy glyph copies the whole page
    /// and stays in the tree regardless.
    /// </summary>
    private static List<Button> SystemGlyphs(PanelView panel) =>
        panel.GetVisualDescendants().OfType<Button>()
            .Where(button => D47.App.Controls.CopyGlyph.GetCopies(button) is not null)
            .ToList();

    /// <summary>
    /// Clicking the copy glyph names it <c>Copy failed</c> when the clipboard refuses; two seconds later it
    /// is named <c>Copy</c> again, and its face is the two squares throughout.
    /// </summary>
    [Trait("Category", "Integration")]
    [AvaloniaFact]
    public void ClickingItSaysWhatHappenedAndThenGoesBack()
    {
        var clock = new ManualClock();
        using var _ = clock.UseForDispatcher();
        var (window, panel, clipboard) = Open(enableCopy: true);

        clipboard.Works = false;

        Row(panel, "Big Slow (Anaconda)").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        var copy = Assert.Single(SystemGlyphs(panel));
        var face = copy.Content;

        Assert.Equal(D47.App.Controls.CopyGlyph.Name, AutomationProperties.GetName(copy));

        copy.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(D47.App.Controls.CopyGlyph.Failed, AutomationProperties.GetName(copy));
        Assert.Same(face, copy.Content);

        clock.Advance(TimeSpan.FromMilliseconds(2200));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(D47.App.Controls.CopyGlyph.Name, AutomationProperties.GetName(copy));

        window.Close();
    }
}
