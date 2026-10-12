using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core.Capabilities;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The faction fields on the systems and stations searches list the faction names that start with what is
/// typed, a pick only fills the field, and the drawn keyboard offers the same names (#1120).
/// </summary>
[Trait("Category", "Integration")]
public sealed class TheFactionFieldsSuggestFactionNamesTests
{
    private static readonly string[] Factions = ["Mother Gaia", "Mother Earth", "Motherlode Miners"];
    private static readonly string[] Names = ["Diaguandri"];

    private sealed class Systems : IStarSystemService
    {
        private readonly List<string> _prefixes = [];

        public string[] Prefixes()
        {
            lock (_prefixes)
            {
                return [.. _prefixes];
            }
        }

        public Task<StarSystemProfile?> ProfileAsync(long systemAddress, CancellationToken cancellationToken) =>
            Task.FromResult<StarSystemProfile?>(null);

        public Task<IReadOnlyList<SystemNameMatch>> MatchNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public Task<IReadOnlyList<SystemNameMatch>> PrefixNamesAsync(string typed, CancellationToken cancellationToken)
        {
            lock (_prefixes)
            {
                _prefixes.Add(typed);
            }

            return Task.FromResult<IReadOnlyList<SystemNameMatch>>(
                [.. Names
                    .Where(name => name.StartsWith(typed, StringComparison.OrdinalIgnoreCase))
                    .Select(name => new SystemNameMatch(name, name.Length, new StarPosition(0, 0, 0)))]);
        }

        public Task<IReadOnlyList<string>> FactionNamesAsync(string typed, CancellationToken cancellationToken)
        {
            lock (_prefixes)
            {
                _prefixes.Add(typed);
            }

            return Task.FromResult<IReadOnlyList<string>>([.. Factions.Where(name => name.StartsWith(typed, StringComparison.OrdinalIgnoreCase))]);
        }

        public Task<PowerplayNeighbourhood> PowerplayNearAsync(string system, double lightYears, CancellationToken cancellationToken) =>
            Task.FromResult(new PowerplayNeighbourhood(0, []));
    }

    private sealed record Shown(PanelView Panel, Window Window, Systems Systems, GalaxySearchBoard Board)
    {
        public TextBox Box(string name) =>
            Panel.GetVisualDescendants().OfType<TextBox>().Last(box => AutomationProperties.GetName(box) == name);

        public string[] Listed =>
        [
            .. Panel.GetVisualDescendants()
                .OfType<Border>()
                .Where(border => border.Name == NameSuggestions.ListName && border.IsEffectivelyVisible)
                .SelectMany(border => border.GetVisualDescendants().OfType<Button>())
                .Select(button => AutomationProperties.GetName(button) ?? string.Empty),
        ];

        public void Pick(string name) =>
            Panel.GetVisualDescendants()
                .OfType<Border>()
                .Where(border => border.Name == NameSuggestions.ListName && border.IsEffectivelyVisible)
                .SelectMany(border => border.GetVisualDescendants().OfType<Button>())
                .Single(button => AutomationProperties.GetName(button) == name)
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    private static Shown Open(string root, bool lookups = true)
    {
        var systems = new Systems();
        var board = new GalaxySearchBoard();
        var registry = CapabilityRegistry.Build([]);

        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableStarSystem(new StarSystemSurface(
            systems,
            () => new JournalLocation("LTT 7786", null, false, null),
            () => lookups,
            () => { })
        {
            Searches = new GalaxySearchSurface(registry, board, null, () => string.Empty, () => [])
            {
                PrefixNames = systems.PrefixNamesAsync,
                FactionNames = systems.FactionNamesAsync,
                SuggestionPause = TimeSpan.Zero,
            },
        });

        var window = new Window { Content = panel, Width = 1280, Height = 960 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        panel.Tab = PanelTab.Search;
        panel.Nav.SelectRoot(root);
        Dispatcher.UIThread.RunJobs();

        return new Shown(panel, window, systems, board);
    }

    private static void Until(Func<bool> done)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);

        while (!done())
        {
            Assert.True(DateTime.UtcNow < deadline, "the page did not settle in time");
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }

        Dispatcher.UIThread.RunJobs();
    }

    private static void Settle()
    {
        Dispatcher.UIThread.RunJobs();
        Thread.Sleep(50);
        Dispatcher.UIThread.RunJobs();
    }

    private static void TypeAndPick(Shown shown, string field)
    {
        shown.Box(field).Text = "Mot";
        Until(() => shown.Listed.Length > 0);

        Assert.Equal(Factions, shown.Listed);

        shown.Pick("Mother Gaia");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Mother Gaia", shown.Box(field).Text);
        Assert.Empty(shown.Listed);
        Assert.Equal(["Mot"], shown.Systems.Prefixes());
    }

    [AvaloniaFact]
    public void PickingAFactionFillsControllingFactionOnTheSystemsSearch()
    {
        var shown = Open(SystemSearchPage.RootKey);

        TypeAndPick(shown, "controlling faction");

        Assert.Null(shown.Board.Last(GalaxySearchKind.Systems));
        shown.Window.Close();
    }

    [AvaloniaFact]
    public void PickingAFactionFillsAFactionPresentOnTheSystemsSearch()
    {
        var shown = Open(SystemSearchPage.RootKey);

        TypeAndPick(shown, "a faction present");

        shown.Window.Close();
    }

    [AvaloniaFact]
    public void PickingAFactionFillsControllingFactionOnTheStationsSearch()
    {
        var shown = Open(StationSearchPage.RootKey);

        TypeAndPick(shown, "controlling faction");

        Assert.Null(shown.Board.Last(GalaxySearchKind.Stations));
        shown.Window.Close();
    }

    [AvaloniaFact]
    public void WithGalaxySearchOffTheFactionFieldsListNothingAndSendNothing()
    {
        var shown = Open(SystemSearchPage.RootKey, lookups: false);

        shown.Box("controlling faction").Text = "Mot";
        Settle();

        Assert.Empty(shown.Listed);
        Assert.Empty(shown.Systems.Prefixes());
        shown.Window.Close();
    }

    [AvaloniaFact]
    public void TheDrawnKeyboardShowsFactionTilesForAFactionField()
    {
        var picked = new List<string>();
        var box = new TextBox();
        _ = new SystemNameSuggestions<string>(
            box,
            (typed, _) => Task.FromResult<IReadOnlyList<string>>([.. Factions.Where(name => name.StartsWith(typed, StringComparison.OrdinalIgnoreCase))]),
            name => name,
            () => true,
            name =>
            {
                picked.Add(name);
                box.Text = name;
            },
            TimeSpan.Zero);

        var view = new PanelView { DataContext = new PanelViewModel() };
        var surface = new OffscreenSurface(view, new PixelSize(1024, 900));
        surface.Type(box);
        surface.Render(view.KeepUp);

        IEnumerable<Button> Buttons() => surface.View.GetVisualDescendants().OfType<Button>();

        foreach (var key in "mot")
        {
            Buttons().First(b => Equals(b.Content, key.ToString())).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
        }

        Until(() => Buttons().Any(b => Equals(b.Content, "Mother Gaia")));

        Buttons().First(b => Equals(b.Content, "Mother Gaia")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Mother Gaia", box.Text);
        Assert.Equal(["Mother Gaia"], picked);
    }
}
