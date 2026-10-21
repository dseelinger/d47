using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Journal;
using D47.Core.Interface;
using D47.Core.Knowledge;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Construction page's Planned group and the planner's figures in a depot's Needs table (#1417).</summary>
public class ThePlannedGroupListsSitesNoDepotHasSeenTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 18, 0, 0, TimeSpan.Zero);

    private const long Vogel = 3900000001;
    private const string Home = "HIP 47126";

    private static JournalEvent Event(TimeSpan at, string json)
    {
        var root = JsonDocument.Parse(json).RootElement;

        return new JournalEvent(Now + at, root.GetProperty("event").GetString()!, root);
    }

    private static CommanderGameState State()
    {
        var state = new CommanderGameState(new CommanderIdentity("F735466", "John Deparagon"));
        var at = TimeSpan.FromHours(-2);

        state.Apply(Event(at, $$"""{"event":"Docked","StationName":"Vogel Relay","StationType":"SpaceConstructionDepot","StarSystem":"{{Home}}","MarketID":{{Vogel}}}"""));
        state.Apply(Event(at, $$"""{"event":"ColonisationConstructionDepot","MarketID":{{Vogel}},"ConstructionProgress":0.62,"ConstructionComplete":false,"ConstructionFailed":false,"ResourcesRequired":[{"Name":"$steel_name;","Name_Localised":"Steel","RequiredAmount":20000,"ProvidedAmount":5180,"Payment":1000},{"Name":"$aluminium_name;","Name_Localised":"Aluminium","RequiredAmount":9410,"ProvidedAmount":0,"Payment":1000}]}"""));

        return state;
    }

    private static SystemPlan Plan(DateTimeOffset fetched) =>
        new(
            Home,
            [
                new PlannedSite("a", "Vogel Relay", PlanStatus.Build)
                {
                    MarketId = Vogel,
                    BuildType = "coriolis",
                    BodyName = "HIP 47126 A 3",
                    BuildId = "secret-build-id",
                    Needed = new Dictionary<string, int> { ["steel"] = 12000, ["aluminium"] = 9999 },
                },
                new PlannedSite("b", "Rift Dock", PlanStatus.Plan) { BuildType = "outpost", BodyName = "HIP 47126 B 1" },
                new PlannedSite("c", string.Empty, PlanStatus.Build)
                {
                    BuildType = "orbital_starport",
                    BodyName = "HIP 47126 A 4",
                    MarketId = 3900000099,
                    Needed = new Dictionary<string, int> { ["steel"] = 4000, ["titanium"] = 700 },
                },
                new PlannedSite("d", "Old Pier", PlanStatus.Complete) { BuildType = "outpost" },
            ],
            fetched);

    private sealed class Held(SystemPlan? plan)
    {
        public SystemPlan? Plan { get; set; } = plan;

        public List<string> Wanted { get; } = [];

        public IReadOnlyDictionary<string, SystemPlan> Snapshot =>
            Plan is { } held
                ? new Dictionary<string, SystemPlan>(StringComparer.OrdinalIgnoreCase) { [Home] = held }
                : new Dictionary<string, SystemPlan>(StringComparer.OrdinalIgnoreCase);
    }

    private static (Window Window, ConstructionPage Page) Open(
        Held plans, double width = 1280, double height = 900, bool headset = false, Action<string>? browse = null)
    {
        var state = State();

        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.Classes.Set("headset", headset);
        panel.EnableConstruction(
            new ConstructionSurface(
                () => state,
                () => Now,
                null,
                null,
                () => false,
                () => plans.Snapshot,
                (system, _) => plans.Wanted.Add(system)));

        if (browse is not null)
        {
            panel.EnableHelp(browse);
        }

        var window = new Window
        {
            Content = panel,
            Width = width,
            Height = height,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };
        window.Show();

        panel.Tab = PanelTab.Assets;
        panel.Nav.SelectRoot(PanelTab.Assets, ConstructionPage.RootKey);
        Dispatcher.UIThread.RunJobs();

        return (window, panel.GetVisualDescendants().OfType<ConstructionPage>().Single());
    }

    private static readonly string Seen = Now.AddHours(-2).ToLocalTime().ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);

    private static IReadOnlyList<string> Text(Visual root) =>
        [.. root.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];

    private static bool Shows(Visual root, string text) =>
        Text(root).Any(shown => string.Equals(shown, text, StringComparison.OrdinalIgnoreCase));

    private static void Capture(Window window, string name)
    {
        Dispatcher.UIThread.RunJobs();
        using var frame = window.CaptureRenderedFrame()!;
        frame.SaveCapture(name);
    }

    private static List<Button> PlannedRows(ConstructionPage page) =>
        [.. page.GetVisualDescendants().OfType<Button>().Where(button => button.Tag is string)];

    [AvaloniaFact]
    public void PlannedSitesWithNoDepotFormAGroupAfterTheTwoTheListHad()
    {
        var plans = new Held(Plan(Now.AddMinutes(-5)));
        var (window, page) = Open(plans);

        Assert.True(Shows(page, "BUILDING · 1"));
        Assert.True(Shows(page, "PLANNED · 2"));
        Assert.Equal([$"{Home}|b", $"{Home}|c"], PlannedRows(page).Select(row => (string)row.Tag!));

        Assert.True(Shows(page, "Rift Dock"));
        Assert.True(Shows(page, "Outpost · HIP 47126 B 1 · HIP 47126"));
        Assert.True(Shows(page, "planned"));
        Assert.True(Shows(page, "Orbital Starport"));
        Assert.True(Shows(page, "building"));
        Assert.False(Shows(page, "Old Pier"));

        window.Close();
    }

    [AvaloniaFact]
    public void ADepotRowAndDetailGainTheBuildTypeAndBodyWhenAPlanMatches()
    {
        var (window, page) = Open(new Held(Plan(Now.AddMinutes(-5))));

        Assert.True(Shows(page, "HIP 47126 · Coriolis · HIP 47126 A 3 · 62% built · seen " + Seen));
        Assert.True(Shows(page, "Coriolis · HIP 47126 A 3"));

        window.Close();
    }

    [AvaloniaFact]
    public void ADepotNeedsTableUsesTheLowerPlannerFigureAndSaysWhere()
    {
        using var look = AppLook.Put();
        var (window, page) = Open(new Held(Plan(Now.AddMinutes(-5))));

        Assert.True(Shows(page, "12,000"));
        Assert.False(Shows(page, "14,820"));
        Assert.True(Shows(page, "9,410"));
        Assert.False(Shows(page, "9,999"));

        var notice = Assert.Single(page.GetVisualDescendants().OfType<Notice>(), n => n.IsEffectivelyVisible);
        Assert.Equal("Some figures are from Raven Colonial, read 5 minutes ago.", notice.Text);

        Capture(window, "construction-planner-figure-desktop.png");
        window.Close();
    }

    [AvaloniaFact]
    public void APlannedSiteShowsItsTypeBodyAndStatusAndTheNeedsOfABuildingOne()
    {
        using var look = AppLook.Put();
        var (window, page) = Open(new Held(Plan(Now.AddMinutes(-5))));

        page.SelectPlanned($"{Home}|c");
        Dispatcher.UIThread.RunJobs();

        Assert.True(Shows(page, "Orbital Starport"));
        Assert.True(Shows(page, "HIP 47126 A 4"));
        Assert.True(Shows(page, "Building"));
        Assert.True(Shows(page, "4,000"));
        Assert.True(Shows(page, "Titanium"));
        Assert.DoesNotContain(page.GetVisualDescendants().OfType<Button>(), button => Equals(button.Content, ConstructionPage.SearchLabel));

        var notice = Assert.Single(page.GetVisualDescendants().OfType<Notice>(), n => n.IsEffectivelyVisible);
        Assert.Equal("These figures are from Raven Colonial, read 5 minutes ago.", notice.Text);

        Capture(window, "construction-planned-site-desktop.png");

        page.SelectPlanned($"{Home}|b");
        Dispatcher.UIThread.RunJobs();

        Assert.True(Shows(page, "Planned"));
        Assert.False(Shows(page, "secret-build-id"));

        window.Close();
    }

    [AvaloniaFact]
    public void AnEmptySnapshotDrawsThePageAsItWas()
    {
        var (window, page) = Open(new Held(null));

        Assert.DoesNotContain(Text(page), text => text.StartsWith("PLANNED", StringComparison.Ordinal));
        Assert.DoesNotContain(page.GetVisualDescendants().OfType<Button>(), button => button.Tag is string);
        Assert.DoesNotContain(page.GetVisualDescendants().OfType<Notice>(), n => n.IsEffectivelyVisible);
        Assert.True(Shows(page, "HIP 47126 · 62% built · seen " + Seen));

        window.Close();
    }

    [AvaloniaFact]
    public void OpeningThePageAsksForEachSystemItShows()
    {
        var plans = new Held(null);
        var (window, _) = Open(plans);

        Assert.Contains(Home, plans.Wanted);

        window.Close();
    }

    [AvaloniaFact]
    public void ThePageRedrawsWhenAPlanArrivesAndNotBefore()
    {
        var plans = new Held(null);
        var (window, page) = Open(plans);

        Assert.False(page.Tick());
        Assert.False(page.Tick());

        plans.Plan = Plan(Now);

        Assert.True(page.Tick());
        Assert.False(page.Tick());
        Dispatcher.UIThread.RunJobs();
        Assert.True(Shows(page, "PLANNED · 2"));

        window.Close();
    }

    [AvaloniaFact]
    public void TheHeadsetPanelShowsThePlannedGroupAndASitesDetail()
    {
        using var look = AppLook.Put();
        var (window, page) = Open(new Held(Plan(Now.AddMinutes(-5))), 860, 1500, headset: true);

        Capture(window, "construction-planner-figure-headset.png");

        page.SelectPlanned($"{Home}|c");
        Dispatcher.UIThread.RunJobs();

        Capture(window, "construction-planned-site-headset.png");
        Assert.True(Shows(page, "PLANNED · 2"));

        window.Close();
    }

    private static Button? PlannerButton(ConstructionPage page) =>
        page.GetVisualDescendants().OfType<Button>().FirstOrDefault(button => button.Name == "PlannerLink");

    [AvaloniaFact]
    public void ADepotWhosePlannedSiteHasABuildIdOpensTheProject()
    {
        var opened = new List<string>();
        var (window, page) = Open(new Held(Plan(Now.AddMinutes(-5))), browse: opened.Add);

        var button = PlannerButton(page);
        Assert.NotNull(button);
        Assert.Equal("https://ravencolonial.com/#build=secret-build-id", button.Tag);
        Assert.Equal("Opens this project on Raven Colonial, in your browser", ToolTip.GetTip(button));

        button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.Equal(["https://ravencolonial.com/#build=secret-build-id"], opened);
        window.Close();
    }

    [AvaloniaFact]
    public void ADepotWithNoPlannedSiteOpensTheSystem()
    {
        var opened = new List<string>();
        var (window, page) = Open(new Held(null), browse: opened.Add);

        var button = PlannerButton(page);
        Assert.NotNull(button);
        Assert.Equal($"Opens {Home} on Raven Colonial, in your browser", ToolTip.GetTip(button));

        button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.Equal([ConstructionPage.PlannerUrl(Home)], opened);
        window.Close();
    }

    [AvaloniaFact]
    public void APlannedSiteWithNoBuildIdOpensTheSystem()
    {
        var opened = new List<string>();
        var (window, page) = Open(new Held(Plan(Now.AddMinutes(-5))), browse: opened.Add);

        page.SelectPlanned($"{Home}|b");
        Dispatcher.UIThread.RunJobs();

        var button = PlannerButton(page);
        Assert.NotNull(button);
        Assert.Equal($"Opens {Home} on Raven Colonial, in your browser", ToolTip.GetTip(button));

        button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.Equal([ConstructionPage.PlannerUrl(Home)], opened);
        window.Close();
    }

    [AvaloniaFact]
    public void APlannedSiteWithABuildIdOpensItsProject()
    {
        using var look = AppLook.Put();
        var opened = new List<string>();
        var plan = Plan(Now.AddMinutes(-5));
        plan = plan with { Sites = [plan.Sites[0] with { MarketId = 3900000050 }, .. plan.Sites.Skip(1)] };
        var (window, page) = Open(new Held(plan), browse: opened.Add);

        page.SelectPlanned($"{Home}|a");
        Dispatcher.UIThread.RunJobs();

        var button = PlannerButton(page);
        Assert.NotNull(button);
        Assert.Equal("Opens this project on Raven Colonial, in your browser", ToolTip.GetTip(button));

        button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.Equal(["https://ravencolonial.com/#build=secret-build-id"], opened);

        Capture(window, "construction-planned-site-project-link.png");
        window.Close();
    }
}
