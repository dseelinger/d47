using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;
using static D47.App.Tests.UiWait;

namespace D47.App.Tests;

/// <summary>Search › Faction in each state it draws, on the desktop and the headset (#2174).</summary>
[Trait("Category", "Integration")]
public sealed class TheFactionPageDrawsEachStateTests
{
    private sealed class Systems : StubStarSystemService, IStarSystemService
    {
        public override Task<StarSystemProfile?> ProfileAsync(long systemAddress, CancellationToken cancellationToken) =>
            Task.FromResult<StarSystemProfile?>(null);

        public override Task<IReadOnlyList<SystemNameMatch>> MatchNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public override Task<IReadOnlyList<SystemNameMatch>> PrefixNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public Task<IReadOnlyList<string>> FactionNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>(
                [.. new[] { "Kestrel Reach Compact", "Kestrel Reach Liberals", "Kestrel Partners" }
                    .Where(name => name.StartsWith(typed, StringComparison.OrdinalIgnoreCase))]);

        public override Task<PowerplayNeighbourhood> PowerplayNearAsync(string system, double lightYears, CancellationToken cancellationToken) =>
            Task.FromResult(new PowerplayNeighbourhood(0, []));
    }

    private sealed class Fetches
    {
        public List<(string Faction, bool Refresh, TaskCompletionSource<FactionReport> Answer)> Asked { get; } = [];

        public Task<FactionReport> Ask(string faction, bool refresh, IProgress<FactionProgress> progress, CancellationToken token)
        {
            var answer = new TaskCompletionSource<FactionReport>();
            lock (Asked)
            {
                Asked.Add((faction, refresh, answer));
            }

            return answer.Task;
        }

        public int Count
        {
            get
            {
                lock (Asked)
                {
                    return Asked.Count;
                }
            }
        }
    }

    private static readonly DateTimeOffset Fetched = DateTimeOffset.UtcNow.AddMinutes(-4);

    private static FactionReport Report(string name, int systems = 60)
    {
        var rows = Enumerable.Range(0, systems)
            .Select(i => new FactionSystem
            {
                Name = $"Hollin {i:000}",
                SystemAddress = 1000 + i,
                Influence = 0.7 - i * 0.01,
                ActiveStates = i % 2 == 0 ? ["Expansion"] : i == 1 ? ["War"] : [],
                PendingStates = i == 3 ? ["Election"] : [],
                RecoveringStates = i == 4 ? ["Outbreak", "Infrastructure Failure"] : [],
                Controls = i % 3 == 0,
                ReportedAt = i == 5 ? Fetched.AddDays(-145) : Fetched.AddHours(-i),
            })
            .ToList();

        return new FactionReport(name, "Confederacy", "Independent", rows, [new FactionStation("Hub", "Coriolis", null, 100, "Hollin 000")])
        {
            FetchedAt = Fetched,
        };
    }

    private sealed record Shown(PanelView Panel, Window Window, FactionPage Page, Fetches Fetches);

    private static Shown Open(bool headset, string? squadron, CommanderGameState? state = null, string root = FactionPage.RootKey) =>
        OpenWith(headset, () => squadron, state, root);

    private static Shown OpenWith(bool headset, Func<string?> squadron, CommanderGameState? state = null, string root = FactionPage.RootKey)
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var fetches = new Fetches();
        var panel = new PanelView { DataContext = new PanelViewModel() };

        if (headset)
        {
            panel.Classes.Add("headset");
        }

        panel.EnableStarSystem(new StarSystemSurface(new Systems(), () => new JournalLocation("Hollin 002", null, false, null))
        {
            Searches = new GalaxySearchSurface(CapabilityRegistry.Build([]), new GalaxySearchBoard(), null, () => string.Empty, () => [])
            {
                FactionNames = new Systems().FactionNamesAsync,
                SuggestionPause = TimeSpan.Zero,
                Faction = fetches.Ask,
                SquadronFaction = squadron,
                GameState = () => state,
            },
        });

        var window = headset
            ? new Window { Content = panel, Width = 1024, Height = 640 }
            : new Window { Content = panel, Width = 1280, Height = 960 };
        window.Bind(Window.BackgroundProperty, Application.Current!.Resources.GetResourceObservable(ThemeManager.BgKey));
        window.Show();
        Dispatcher.UIThread.RunJobs();

        panel.Tab = PanelTab.Search;
        panel.Nav.SelectRoot(root);
        Dispatcher.UIThread.RunJobs();

        var page = panel.GetVisualDescendants().OfType<FactionPage>().SingleOrDefault()!;

        return new Shown(panel, window, page, fetches);
    }

    private static void Save(Window window, string name)
    {
        Dispatcher.UIThread.RunJobs();
        using var frame = window.CaptureRenderedFrame()!;
        frame.SaveCapture(name);
    }

    private static T Named<T>(Shown shown, string name)
        where T : Control =>
        shown.Page.GetVisualDescendants().OfType<T>().First(control => control.Name == name);

    private static async Task Answer(Shown shown, int index, FactionReport report)
    {
        shown.Fetches.Asked[index].Answer.SetResult(report);
        Until(() => shown.Page.View == FactionView.Answered);
        await Task.CompletedTask;
    }

    [AvaloniaFact]
    public void TheFactionRootSitsBeforeStationsAndOpensEmptyWithoutASquadronFaction()
    {
        var shown = Open(false, null);
        var roots = shown.Panel.Nav.Roots(PanelTab.Search).Select(root => root.Word).ToList();

        Assert.True(roots.IndexOf("Faction") == roots.IndexOf("Systems") + 1);
        Assert.Equal("Stations", roots[roots.IndexOf("Faction") + 1]);
        Assert.Equal(FactionView.Empty, shown.Page.View);
        Assert.Equal(0, shown.Fetches.Count);
        Save(shown.Window, "faction-desktop-empty.png");
        shown.Window.Close();
    }

    [AvaloniaFact]
    public async Task ItOpensOnTheSquadronFactionAndDrawsOverviewAndSystems()
    {
        var shown = Open(false, "Kestrel Reach Compact");

        Assert.Equal(FactionView.Loading, shown.Page.View);
        Assert.Equal(("Kestrel Reach Compact", false), (shown.Fetches.Asked[0].Faction, shown.Fetches.Asked[0].Refresh));
        Save(shown.Window, "faction-desktop-loading.png");

        await Answer(shown, 0, Report("Kestrel Reach Compact"));
        Save(shown.Window, "faction-desktop-overview.png");

        shown.Page.ShowSection(1);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(60, shown.Page.RowsDrawn);
        Save(shown.Window, "faction-desktop-systems.png");

        Named<TextBox>(shown, FactionPage.SystemsFindName).Text = "hollin 01";
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(10, shown.Page.RowsDrawn);
        shown.Window.Close();
    }

    [AvaloniaFact]
    public async Task TheHeadsetShowsTwentyFiveSystemsAPageAndPagesThroughThem()
    {
        var shown = Open(true, "Kestrel Reach Compact");
        await Answer(shown, 0, Report("Kestrel Reach Compact"));
        Save(shown.Window, "faction-headset-overview.png");

        shown.Page.ShowSection(1);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(25, shown.Page.RowsDrawn);
        Save(shown.Window, "faction-headset-systems.png");

        Named<Button>(shown, FactionPage.NextName).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Named<Button>(shown, FactionPage.NextName).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(10, shown.Page.RowsDrawn);
        Assert.False(Named<Button>(shown, FactionPage.NextName).IsEnabled);
        shown.Window.Close();
    }

    [AvaloniaFact]
    public void AFailedFetchShowsTheServicesSentenceAndRetryAsksAgain()
    {
        var shown = Open(false, "Kestrel Reach Compact");

        shown.Fetches.Asked[0].Answer.SetException(new GalaxyUnavailableException("Spansh is not answering."));
        Until(() => shown.Page.View == FactionView.Failed);

        Assert.Equal("Spansh is not answering.", shown.Page.FailureText);
        Save(shown.Window, "faction-desktop-failed.png");

        Named<Button>(shown, FactionPage.RetryName).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, shown.Fetches.Count);
        Assert.True(shown.Fetches.Asked[1].Refresh);
        Assert.Equal(FactionView.Loading, shown.Page.View);
        shown.Window.Close();
    }

    [AvaloniaFact]
    public async Task AnAnswerForAFactionThatWasReplacedIsIgnored()
    {
        var shown = Open(false, "Kestrel Reach Compact");

        shown.Page.Show("Kestrel Partners");
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(2, shown.Fetches.Count);

        shown.Fetches.Asked[0].Answer.SetResult(Report("Kestrel Reach Compact", 5));
        Thread.Sleep(100);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(FactionView.Loading, shown.Page.View);

        await Answer(shown, 1, Report("Kestrel Partners", 7));
        Assert.Equal("Kestrel Partners", shown.Page.Faction);
        shown.Window.Close();
    }

    [AvaloniaFact]
    public void FindOpeningAFactionBeforeThePageIsBuiltAsksOnceForThatFaction()
    {
        var shown = Open(false, "Kestrel Reach Compact", root: SystemSearchPage.RootKey);

        shown.Panel.OpenFound(new PanelFindResult(PanelFindKind.Faction, "Kestrel Partners", "Minor faction", "Kestrel Partners", 2));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(FactionPage.RootKey, shown.Panel.Nav.RootKeyOf(PanelTab.Search));
        Assert.Equal(["Kestrel Partners"], shown.Fetches.Asked.Select(asked => asked.Faction));
        shown.Window.Close();
    }

    [AvaloniaFact]
    public void FindOpeningAFactionWhileThePageIsOpenReplacesWhatItShows()
    {
        var shown = Open(false, null);

        shown.Panel.OpenFound(new PanelFindResult(PanelFindKind.Faction, "Kestrel Partners", "Minor faction", "Kestrel Partners", 2));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Kestrel Partners"], shown.Fetches.Asked.Select(asked => asked.Faction));
        Assert.Equal("Kestrel Partners", shown.Page.Faction);
        shown.Window.Close();
    }

    [AvaloniaFact]
    public async Task AReportWithNoSystemsSaysSoInsteadOfDrawingEmptySections()
    {
        var shown = Open(false, "Nobody");

        await Answer(shown, 0, Report("Nobody", 0));

        Assert.Equal(0, shown.Page.RowsDrawn);
        Assert.Contains(shown.Page.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == FactionPage.NoReportText);
        Assert.DoesNotContain(shown.Page.GetVisualDescendants().OfType<TextBox>(), box => box.Name == FactionPage.SystemsFindName);
        shown.Window.Close();
    }

    [AvaloniaFact]
    public async Task ARedrawFromTheTickKeepsTheScrollPosition()
    {
        string? squadron = "Kestrel Reach Compact";
        var shown = OpenWith(false, () => squadron);
        await Answer(shown, 0, Report("Kestrel Reach Compact", 200));
        shown.Page.ShowSection(1);
        Dispatcher.UIThread.RunJobs();

        var scroll = shown.Page.GetVisualDescendants().OfType<ScrollViewer>().First(view => view.Extent.Height > view.Viewport.Height);
        scroll.Offset = new Vector(0, 600);
        Dispatcher.UIThread.RunJobs();

        squadron = null;
        Assert.True(shown.Page.Tick());

        for (var i = 0; i < 4; i++)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(20);
        }

        var after = shown.Page.GetVisualDescendants().OfType<ScrollViewer>().First(view => view.Extent.Height > view.Viewport.Height);
        Assert.Equal(600, after.Offset.Y);
        shown.Window.Close();
    }

    [AvaloniaFact]
    public void ASquadronWithNoKnownFactionSaysHowItIsLearnedAndOpensEmpty()
    {
        var state = JournalScript.Commander(
            "F1",
            "Fixture",
            ["""{"timestamp":"2026-10-10T16:00:00Z","event":"SquadronStartup","SquadronName":"Greybeards","SquadronID":7,"CurrentRank":1}"""]);
        var shown = Open(false, null, state);

        Assert.Equal(FactionView.Empty, shown.Page.View);
        Assert.Contains(shown.Page.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == FactionPage.UnknownText && block.IsVisible);
        Save(shown.Window, "faction-desktop-unknown.png");
        shown.Window.Close();

        var headset = Open(true, null, state);
        Save(headset.Window, "faction-headset-unknown.png");
        headset.Window.Close();
    }

    [AvaloniaFact]
    public void TheHeadsetDrawsEmptyLoadingAndFailed()
    {
        var empty = Open(true, null);
        Save(empty.Window, "faction-headset-empty.png");
        empty.Window.Close();

        var loading = Open(true, "Kestrel Reach Compact");
        Save(loading.Window, "faction-headset-loading.png");
        loading.Fetches.Asked[0].Answer.SetException(new GalaxyUnavailableException("The galaxy search took too long to answer."));
        Until(() => loading.Page.View == FactionView.Failed);
        Save(loading.Window, "faction-headset-failed.png");
        loading.Window.Close();
    }

    [AvaloniaFact]
    public void ThePageSummaryIsTheDesignsLine() =>
        Assert.Equal("Where a minor faction is present, and the state it is in.", FactionPage.SummaryText);

    [AvaloniaFact]
    public void TheProgressLineSaysHowManySystemsAreRead()
    {
        Assert.Equal("ASKING THE GALAXY SEARCH", FactionPage.ProgressText(null));
        Assert.Equal("SYSTEMS READ 1,200 OF 6,000", FactionPage.ProgressText(new FactionProgress(1200, 6000)));
        _ = CultureInfo.InvariantCulture;
    }
}
