using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.App.Windowing;
using D47.Core.Interface;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The title bar's Commander switcher and Commander › Commanders (#894).</summary>
public class SwitchCommandersFromTheTitleBarTests
{
    private static DateTimeOffset Local(int month, int day, int hour, int minute) =>
        new(new DateTime(2026, month, day, hour, minute, 0, DateTimeKind.Local));

    private static readonly CommanderSighting John =
        new("F735466", "John Deparagon", Local(10, 5, 19, 40), "Giryak", "Mandalay");

    private static readonly CommanderSighting Kestrel =
        new("F100200", "Kestrel Vane", Local(9, 28, 21, 14), "Colonia", "Krait Phantom");

    private static readonly CommanderSighting Oda =
        new("F300400", "Oda Merrin", Local(8, 2, 18, 40), "Shinrarta Dezhra", "Type-9 Heavy");

    private sealed class Journals
    {
        public Dictionary<string, CommanderSighting>? Found { get; set; }

        public string? Shown { get; set; } = John.FrontierId;

        public List<CommanderIdentity> Picked { get; } = [];

        public CommanderRoster Roster => new(() => Found, () => 41, () => Shown, Picked.Add);

        public static Journals Of(params CommanderSighting[] sightings) =>
            new() { Found = sightings.ToDictionary(sighting => sighting.FrontierId) };
    }

    private static void Theme() =>
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

    private static Color Published(string key) => ((ISolidColorBrush)Application.Current!.Resources[key]!).Color;

    private static Color Ink(IBrush? brush) => ((ISolidColorBrush)brush!).Color;

    private static void Press(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static IEnumerable<T> In<T>(Control root) => root.GetVisualDescendants().OfType<T>();

    private static TextBlock Text(Control root, string text) => In<TextBlock>(root).First(block => block.Text == text);

    private static bool Shows(Control root, string text) => In<TextBlock>(root).Any(block => block.Text == text);

    private static (Window Window, CommanderSwitcher Switcher, PanelView Panel) Window(Journals journals)
    {
        Theme();

        var panel = new PanelView { DataContext = new PanelViewModel() };
        var roster = journals.Roster;
        panel.EnableCommanders(roster);

        var switcher = new CommanderSwitcher(roster, () => panel.Nav.Show(CommandersPage.RootKey), CaptionStrip.StripHeight);

        var window = new Window
        {
            Title = "Directive 47 — 0.110.0",
            Width = 1280,
            Height = 860,
            Content = panel,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };
        CaptionStrip.Apply(window, beforeButtons: switcher);
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, switcher, panel);
    }

    private static Control MenuOf(CommanderSwitcher switcher)
    {
        switcher.Open();
        Dispatcher.UIThread.RunJobs();
        return (Control)switcher.Menu.Child!;
    }

    [AvaloniaFact]
    public void OneCommanderShowsNoSwitcher()
    {
        var (window, switcher, _) = Window(Journals.Of(John));

        Assert.False(switcher.IsVisible);

        window.Close();
    }

    [AvaloniaFact]
    public void BeforeTheWalkHasListedAnyoneThereIsNoSwitcher()
    {
        var (window, switcher, _) = Window(new Journals());

        Assert.False(switcher.IsVisible);

        window.Close();
    }

    [AvaloniaFact]
    public void TheSwitcherAppearsOnceASecondCommanderIsFound()
    {
        var journals = Journals.Of(John);
        var (window, switcher, _) = Window(journals);

        journals.Found = new Dictionary<string, CommanderSighting>(journals.Found!) { [Kestrel.FrontierId] = Kestrel };
        switcher.Tick();

        Assert.True(switcher.IsVisible);
        Assert.Equal("CMDR JOHN DEPARAGON", switcher.Label);

        window.Close();
    }

    [AvaloniaFact]
    public void TheMenuListsEveryCommanderNewestFirstThenManage()
    {
        var (window, switcher, _) = Window(Journals.Of(Oda, John, Kestrel));

        var menu = MenuOf(switcher);
        var labels = In<TextBlock>(menu).Select(block => block.Text).Where(text => text?.StartsWith("CMDR", StringComparison.Ordinal) == true).ToList();

        Assert.Equal(["CMDR JOHN DEPARAGON", "CMDR KESTREL VANE", "CMDR ODA MERRIN"], labels);
        Assert.True(Shows(menu, "2026-09-28 · 21:14"));
        Assert.Equal(CommanderSwitcher.Manage, (string)In<Button>(menu).Last().Content!);

        window.Close();
    }

    [AvaloniaFact]
    public void TheCurrentCommanderIsTickedAndCyanOnTheCyanGround()
    {
        var (window, switcher, _) = Window(Journals.Of(John, Kestrel));

        var menu = MenuOf(switcher);
        var current = In<Button>(menu).Single(button => button.Name == "CurrentCommander");

        Assert.True(Shows(current, "✓"));
        Assert.Equal(Published(ThemeManager.CyanKey), Ink(Text(current, "CMDR JOHN DEPARAGON").Foreground));
        Assert.Equal(Published(ThemeManager.CyanGroundKey), Ink(current.Background));
        Assert.Equal(Published(ThemeManager.WhiteKey), Ink(Text(menu, "CMDR KESTREL VANE").Foreground));

        window.Close();
    }

    [AvaloniaFact]
    public void ChoosingARowPicksThatCommanderAndTheLabelFollows()
    {
        var journals = Journals.Of(John, Kestrel);
        var (window, switcher, _) = Window(journals);

        var menu = MenuOf(switcher);
        Press(In<Button>(menu).Single(button => button.Name == "OtherCommander"));

        Assert.Equal([new CommanderIdentity(Kestrel.FrontierId, Kestrel.Name)], journals.Picked);
        Assert.False(switcher.Menu.IsOpen);

        journals.Shown = Kestrel.FrontierId;
        switcher.Tick();

        Assert.Equal("CMDR KESTREL VANE", switcher.Label);

        window.Close();
    }

    [AvaloniaFact]
    public void ManageCommandersOpensTheCommandersRoot()
    {
        var (window, switcher, panel) = Window(Journals.Of(John, Kestrel));

        var menu = MenuOf(switcher);
        Press(In<Button>(menu).Single(button => button.Name == "ManageCommanders"));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(PanelTab.Commander, panel.Tab);
        Assert.Equal(CommandersPage.RootKey, panel.Nav.RootKeyOf(PanelTab.Commander));
        Assert.True(Shows(panel, CommandersPage.Helper));

        window.Close();
    }

    [AvaloniaFact]
    public void TheCommandersPageListsEachWithASwitchAndTheTally()
    {
        var (window, _, panel) = Window(Journals.Of(John, Kestrel, Oda));
        panel.Nav.Show(CommandersPage.RootKey);
        Dispatcher.UIThread.RunJobs();

        Assert.True(Shows(panel, CommandersPage.Helper));
        Assert.True(Shows(panel, "3 commanders · found in 41 journal files"));
        Assert.True(Shows(panel, "✓ CURRENT"));
        Assert.True(Shows(panel, "Krait Phantom"));
        Assert.True(Shows(panel, "Shinrarta Dezhra"));
        Assert.True(Shows(panel, "2026-10-05 · 19:40"));
        Assert.Equal(2, In<Button>(panel).Count(button => button.Name == "CommanderSwitch"));
        Assert.Equal(Published(ThemeManager.CyanKey), Ink(Text(panel, "Giryak").Foreground));
        Assert.Equal(Published(ThemeManager.AKey), Ink(Text(panel, "Colonia").Foreground));

        window.Close();
    }

    [AvaloniaFact]
    public void ASwitchOnThePagePicksAndThePageRedrawsOnTheTick()
    {
        var journals = Journals.Of(John, Kestrel);
        var (window, _, panel) = Window(journals);
        panel.Nav.Show(CommandersPage.RootKey);
        Dispatcher.UIThread.RunJobs();

        Press(In<Button>(panel).Single(button => button.Name == "CommanderSwitch"));
        Assert.Equal([new CommanderIdentity(Kestrel.FrontierId, Kestrel.Name)], journals.Picked);

        Assert.False(panel.TickCommander());
        journals.Shown = Kestrel.FrontierId;
        Assert.True(panel.TickCommander());
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Published(ThemeManager.CyanKey), Ink(Text(panel, "CMDR KESTREL VANE").Foreground));

        window.Close();
    }

    [AvaloniaFact]
    public void TheCommandersRootIsLastOnTheCommanderTab()
    {
        var (window, _, panel) = Window(Journals.Of(John, Kestrel));

        var roots = panel.Nav.Roots(PanelTab.Commander);
        Assert.Equal(CommandersPage.RootKey, roots[^1].Key);
        Assert.Equal("Commanders", roots[^1].Word);

        window.Close();
    }

    [AvaloniaFact]
    public void TheTallyCountsInTheSingularToo() =>
        Assert.Equal("1 commander · found in 1 journal file", CommandersPage.Tally(1, 1));

    [AvaloniaFact]
    public void TheSwitcherAndThePageAreCaptured()
    {
        using var look = AppLook.Put();
        var (window, switcher, panel) = Window(Journals.Of(John, Kestrel, Oda));
        panel.Nav.Show(CommandersPage.RootKey);
        Dispatcher.UIThread.RunJobs();

        using (var page = window.CaptureRenderedFrame()!)
        {
            page.SaveCapture("commander-commanders.png");
        }

        switcher.Open();
        Dispatcher.UIThread.RunJobs();

        using var menu = window.CaptureRenderedFrame()!;
        var path = "commander-switcher-open.png";
        menu.SaveCapture(path);

        window.Close();
    }
}
