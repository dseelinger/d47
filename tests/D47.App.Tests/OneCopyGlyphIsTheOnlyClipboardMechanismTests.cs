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
    /// bare <see cref="D47.App.Controls.Glyphs.Copy"/> mark, and no clipboard read straight off the
    /// visual root. The Transcript tab's own "Copy this whole page" button is a different feature
    /// (Phase 19, copying the whole page rather than a system name) and is not scanned here.
    /// </summary>
    [Fact]
    public void NoPageThatDrawsASystemNameReachesForItsOwnClipboardMechanism()
    {
        var folder = Path.Combine(RepositoryRoot(), "src", "D47.App", "Panel");

        foreach (var name in new[]
                 {
                     "LoadoutPages.cs",
                     "RouteCommunityGoalPage.cs",
                     "RoutePlanResultPage.cs",
                     "RouteProgressPage.cs",
                     "RouteMarketPage.cs",
                     "SourcingPage.cs",
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
            new ChecklistStore(Path.Combine(root, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(root, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        var store = new ShipBuildStore(Path.Combine(root, "ships.json"), NullLogger<ShipBuildStore>.Instance);
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

        panel.Tab = PanelTab.Loadout;
        Dispatcher.UIThread.RunJobs();

        return (window, panel, clipboard);
    }

    private static Button Row(PanelView panel, string label) =>
        panel.GetVisualDescendants().OfType<Button>()
            .First(button => button.GetVisualDescendants().OfType<TextBlock>()
                .Any(text => text.Text == label));

    /// <summary>A surface with no <c>EnableCopy</c> draws no glyph and no page throws.</summary>
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

        // The Transcript tab's own "Copy this whole page to the clipboard" button is a different feature
        // (Phase 19) and stays in the tree regardless — only a system-name glyph is being asked about here.
        Assert.DoesNotContain(
            panel.GetVisualDescendants().OfType<Button>(),
            button => AutomationProperties.GetName(button) == "Copy Shinrarta Dezhra");

        window.Close();
    }

    /// <summary>
    /// Clicking the glyph shows a tick when the clipboard reports success and a cross when it does
    /// not; two seconds later the mark is the copy glyph again.
    /// </summary>
    [AvaloniaFact]
    public async Task ClickingItShowsATickOrACrossAndThenResetsTheMark()
    {
        var (window, panel, clipboard) = Open(enableCopy: true);

        clipboard.Works = false;

        Row(panel, "Big Slow (Anaconda)").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        var glyph = panel.GetVisualDescendants().OfType<Button>()
            .Single(button => AutomationProperties.GetName(button) == "Copy Shinrarta Dezhra");

        glyph.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        // Compared by the geometry's bounds against a path built from the constant, because Data is a parsed
        // StreamGeometry and does not hand back the string it came from.
        var crossBounds = D47.App.Controls.Glyphs.Draw(
            D47.App.Controls.Glyphs.Cross, D47.App.Theming.ThemeManager.DangerKey, size: 12).Data!.Bounds;
        var copyBounds = D47.App.Controls.Glyphs.Draw(
            D47.App.Controls.Glyphs.Copy, D47.App.Theming.ThemeManager.AccentKey, size: 12).Data!.Bounds;

        var cross = Assert.IsType<Avalonia.Controls.Shapes.Path>(glyph.Content);

        Assert.Equal(crossBounds, cross.Data!.Bounds);

        clipboard.Works = true;

        await Task.Delay(2200, TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();

        var reset = Assert.IsType<Avalonia.Controls.Shapes.Path>(glyph.Content);

        Assert.Equal(copyBounds, reset.Data!.Bounds);

        window.Close();
    }
}
