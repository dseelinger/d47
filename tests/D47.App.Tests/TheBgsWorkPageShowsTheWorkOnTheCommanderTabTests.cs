using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Capabilities.Builtin;
using D47.Core.Checklists;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Commander › Ledgers › BGS work as drawn: the empty page, the unknown-tick notice, the sort heading and the search (#1287).</summary>
public class TheBgsWorkPageShowsTheWorkOnTheCommanderTabTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-10T20:00:00Z", CultureInfo.InvariantCulture);
    private static readonly DateTimeOffset Tick = DateTimeOffset.Parse("2026-10-10T16:14:00Z", CultureInfo.InvariantCulture);

    private static readonly string[] Work =
    [
        """{"timestamp":"2026-10-10T17:00:00Z","event":"Docked","StationName":"Hutton Orbital","MarketID":100,"StarSystem":"Alpha","SystemAddress":1,"StationFaction":{"Name":"Alpha Mining"}}""",
        """{"timestamp":"2026-10-10T17:05:00Z","event":"RedeemVoucher","Type":"bounty","Factions":[{"Faction":"Gamma Pirates","Amount":3000}]}""",
        """{"timestamp":"2026-10-10T17:08:00Z","event":"SellExplorationData","TotalEarnings":123456}""",
        """{"timestamp":"2026-10-10T17:09:00Z","event":"MissionCompleted","FactionEffects":[{"Faction":"Beta Union","Influence":[{"SystemAddress":2,"Trend":"UpGood","Influence":"+++"}]}]}""",
    ];

    private static CommanderGameState State(params string[] lines)
    {
        var state = new CommanderGameState(new CommanderIdentity("F735466", "John Deparagon"));

        foreach (var line in lines)
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            state.Apply(parsed!);
        }

        return state;
    }

    private static (Window Window, BgsWorkPage Page) Open(
        CommanderGameState state, DateTimeOffset? tick, Action<string>? openSettings = null, bool headset = false)
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var page = new BgsWorkPage(() => state, () => tick, () => Now, openSettings, headset);
        var window = new Window
        {
            Content = page,
            Width = 1280,
            Height = 900,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, page);
    }

    private static IEnumerable<T> In<T>(Control root) where T : Visual => root.GetVisualDescendants().OfType<T>();

    private static bool Shows(Control root, string text) => In<TextBlock>(root).Any(block => block.Text == text);

    private static Button Named(Control root, string name) => In<Button>(root).Single(button => button.Name == name);

    [AvaloniaFact]
    public void WithATickTimeAndNothingAfterItThePageSaysSoAndHasNoSortHeader()
    {
        var (window, page) = Open(State(), Tick);

        Assert.True(Shows(page, "No BGS work since the tick at 16:14 UTC."));
        Assert.True(Shows(page, BgsWorkPage.Hint));
        Assert.True(Shows(page, "0 FACTIONS"));
        Assert.DoesNotContain(In<Button>(page), button => button.Name?.StartsWith("BgsWorkHead.", StringComparison.Ordinal) == true);
        Assert.Empty(In<Notice>(page));

        window.Close();
    }

    [AvaloniaFact]
    public void WithNoTickTimeTheNoticeOffersTheOutsideSourcesSettingOnTheDesktop()
    {
        string? opened = null;
        var (window, page) = Open(State(), null, id => opened = id);

        var notice = Assert.Single(In<Notice>(page));
        Assert.Equal(NoticeLevel.Warning, notice.Level);
        Assert.Equal(BgsWorkPage.UnknownTickLabel, notice.Label);
        Assert.Equal(BgsWorkPage.UnknownTickText, notice.Text);
        Assert.True(Shows(page, "No BGS work in the last 24 hours."));

        var tile = In<Button>(notice).Single();
        Assert.Equal("OUTSIDE SOURCES", tile.Content);
        tile.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(GalaxyCapability.Id, opened);

        window.Close();
    }

    [AvaloniaFact]
    public void OnTheBigPanelTheNoticeHasNoTile()
    {
        var (window, page) = Open(State(), null, headset: true);

        Assert.Single(In<Notice>(page));
        Assert.DoesNotContain(In<Button>(page), button => Equals(button.Content, "OUTSIDE SOURCES"));

        window.Close();
    }

    [AvaloniaFact]
    public void ATickTimeDrawsTheTableWithItsInfluenceHeadingAndWithoutTheNotice()
    {
        var (window, page) = Open(State(Work), Tick);

        Assert.Empty(In<Notice>(page));
        Assert.True(Shows(page, "3 FACTIONS"));

        foreach (var heading in new[] { "System", "Missions", "Combat", "Trade", "Data", "Influence" })
        {
            Assert.Single(In<Button>(page), button => button.Name == "BgsWorkHead." + heading);
        }

        Assert.True(Shows(page, "A system not in your recent journals"));
        Assert.Equal("The work you have done for minor factions since the BGS tick.", page.Summary);

        window.Close();
    }

    [AvaloniaFact]
    public void WithNoTickTimeThereIsNoInfluenceColumn()
    {
        var (window, page) = Open(State(Work), null);

        Assert.DoesNotContain(In<Button>(page), button => button.Name == "BgsWorkHead.Influence");
        Assert.Single(In<Button>(page), button => button.Name == "BgsWorkHead.System");
        Assert.Equal("The work you have done for minor factions in the last 24 hours.", page.Summary);

        window.Close();
    }

    [AvaloniaFact]
    public void PressingAHeadingMarksItAndASearchNarrowsTheCount()
    {
        var (window, page) = Open(State(Work), Tick);

        Assert.True(Shows(page, "MISSIONS"));
        Named(page, "BgsWorkHead.Missions").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Assert.True(Shows(page, "MISSIONS ▼"));

        page.Filter("gamma");
        Dispatcher.UIThread.RunJobs();
        Assert.True(Shows(page, "1 OF 3 FACTIONS"));

        page.Filter("nowhere");
        Dispatcher.UIThread.RunJobs();
        Assert.True(Shows(page, "No system or faction since the tick matches that search."));
        Assert.True(Shows(page, "0 OF 3 FACTIONS"));

        window.Close();
    }

    [AvaloniaFact]
    public void TheBigPanelOpensBgsWorkUnderLedgersAndGivesItsNoticeNoTile()
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var root = TestSurface.MemoryFolder("d47-bgs-work-page-tests");
        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(root, "checklist.json"), new MemoryFileSystem(), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(root, "checklist-proposals.json"),
                new MemoryFileSystem(),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        var state = State();
        var view = new PanelView { DataContext = new PanelViewModel() };
        view.Classes.Add("headset");
        new MiniPanelServices { Checklists = checklists, GameState = () => state }.Furnish(view);

        Assert.Equal(
            ["Checklist", "Missions", "Standing", "Statistics", "This session", "Ledgers"],
            view.Nav.Roots(PanelTab.Commander).Select(root => root.Word));

        Assert.True(view.Nav.Show(BgsWorkPage.PageKey));
        var window = new Window { Content = view, Width = 1280, Height = 900 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Ledgers", "BGS work"], view.Nav.Trail.Select(crumb => crumb.Word));

        var page = In<BgsWorkPage>(view).Single();
        Assert.Single(In<Notice>(page));
        Assert.DoesNotContain(In<Button>(page), button => Equals(button.Content, "OUTSIDE SOURCES"));

        window.Close();
    }
}
