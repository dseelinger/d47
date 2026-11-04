using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Windowing;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.App.Tests;

/// <summary>Find's All results tile and the All results page, with the canvas's sample data. Captures go to <see cref="TestSurface.CaptureDirectory"/>.</summary>
public sealed class AllResultsIsDrawnAsItsCanvasShowsTests
{
    private sealed class Systems : StubStarSystemService
    {
        public override Task<StarSystemProfile?> ProfileAsync(long systemAddress, CancellationToken cancellationToken) =>
            Task.FromResult<StarSystemProfile?>(new StarSystemProfile { Name = $"System {systemAddress}", SystemAddress = systemAddress });

        public override Task<IReadOnlyList<SystemNameMatch>> MatchNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public override Task<IReadOnlyList<SystemNameMatch>> PrefixNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public override Task<PowerplayNeighbourhood> PowerplayNearAsync(string system, double lightYears, CancellationToken cancellationToken) =>
            Task.FromResult(new PowerplayNeighbourhood(0, []));
    }

    private static readonly string[] SystemNames =
    [
        "Darku", "Darkan", "Darkinu", "Darkates", "Beldarkri", "Darkaungu", "Darkawa", "Darkbar", "Darkena", "Darkes",
        "Darkhar", "Darkinda", "Darkova", "Darkuwa", "Darkwan", "Darkalo", "Darkanus", "Darkaruni", "Darkemi", "Darkotsi",
    ];

    private static IReadOnlyList<PanelFindResult> Dark()
    {
        var rank = 0;
        var results = new List<PanelFindResult>();

        results.AddRange(SystemNames.Select(name => new PanelFindResult(PanelFindKind.StarSystem, name, "Star system", name, ++rank, SystemAddress: rank)));
        results.AddRange(new (string Name, double Ly)[]
        {
            ("Dark Wheel", 64.3), ("Darkmoor Hub", 88.0), ("Dark Horizon Port", 104.2),
            ("Darkstar Relay", 140.9), ("Darkwater Dock", 212.5), ("Dark Harbour", 318.0),
        }.Select(s => new PanelFindResult(PanelFindKind.Station, s.Name, "Station", s.Name, ++rank, SystemAddress: rank, LightYears: s.Ly)));
        results.AddRange(new[] { "Dark Echo", "Dark Sky Collective", "Darkan Justice Party", "Darkan Empire League" }
            .Select(name => new PanelFindResult(PanelFindKind.Faction, name, "Minor faction", name, ++rank)));
        results.Add(new PanelFindResult(PanelFindKind.Bookmark, "Dark Wheel Run", "Commander › Bookmarks", "Dark Wheel Run", ++rank));

        return results;
    }

    private static IReadOnlyList<PanelFindResult> Push() =>
    [
        new(PanelFindKind.SettingsPlace, "Push-to-talk", "Settings › Voice", "voice.ptt", 1),
        new(PanelFindKind.SettingRow, "Push-to-talk key", "Settings › Voice › Push-to-talk", "voice.ptt.key", 2),
        new(PanelFindKind.HelpPage, "Push-to-talk", "Help › Voice", "ptt", 3),
    ];

    [AvaloniaFact]
    public void TheFindTileAndBothPagesAreCaptured()
    {
        using var look = AppLook.Put();

        var findWindow = new Window { Content = new Grid(), Width = 760, Height = 780 };
        findWindow.Show();
        var dialog = new FindDialog(q => q.Length == 0 ? [] : Dark(), _ => { }, "Ctrl+Shift+F", null, (_, _) => { });
        _ = dialog.Over(findWindow);
        dialog.Query = "dark";
        Save(findWindow, "all-results-find-tile.png");
        dialog.Move(-1);
        Save(findWindow, "all-results-find-tile-selected.png");
        findWindow.Close();

        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableStarSystem(new StarSystemSurface(
            new Systems(),
            () => new JournalLocation("LTT 7786", null, false, null)));

        var window = new Window { Content = panel, Width = 1280, Height = 2000 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        panel.OpenAllResults("dark", Dark());
        Save(window, "all-results-page-dark.png");

        window.Height = 960;
        var page = panel.GetVisualDescendants().OfType<AllResultsPage>().Single();
        page.Rows[1].RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, Source = page.Rows[1] });
        Save(window, "all-results-drilled-system.png");

        window.Height = 640;
        panel.OpenAllResults("push", Push());
        Save(window, "all-results-page-one-group.png");

        window.Close();
    }

    private static void Save(Window window, string name)
    {
        for (var i = 0; i < 3; i++)
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
        }

        using var frame = window.CaptureRenderedFrame()!;
        frame.SaveCapture(name);
    }
}
