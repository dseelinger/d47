using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.App.Tests;

/// <summary>All results opens under Search › System and draws the query it was opened with, not an earlier one.</summary>
public sealed class AllResultsShowsTheLatestQueryTests
{
    private sealed class Systems : IStarSystemService
    {
        public Task<StarSystemProfile?> ProfileAsync(long systemAddress, CancellationToken cancellationToken) =>
            Task.FromResult<StarSystemProfile?>(null);

        public Task<IReadOnlyList<SystemNameMatch>> MatchNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public Task<IReadOnlyList<SystemNameMatch>> PrefixNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public Task<PowerplayNeighbourhood> PowerplayNearAsync(string system, double lightYears, CancellationToken cancellationToken) =>
            Task.FromResult(new PowerplayNeighbourhood(0, []));
    }

    private static IReadOnlyList<PanelFindResult> Results(string title) =>
        [new PanelFindResult(PanelFindKind.StarSystem, title, "System", title, 1)];

    [AvaloniaFact]
    public void ASecondQueryReplacesTheFirstQuerysPage()
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableStarSystem(new StarSystemSurface(
            new Systems(),
            () => new JournalLocation("LTT 7786", null, false, null),
            () => false,
            () => { }));

        var window = new Window { Content = panel, Width = 1280, Height = 960 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        panel.OpenAllResults("dark", Results("Darkan"));
        Dispatcher.UIThread.RunJobs();

        var trail = panel.Nav.TrailOf(PanelTab.Search);
        Assert.Equal(StarSystemPage.RootKey, trail[0].Key);
        Assert.Equal(AllResultsPage.PageKey, trail[^1].Key);
        Assert.Equal("Darkan", (string?)Assert.Single(Page(panel).Rows).Tag);

        panel.OpenAllResults("sacred", Results("Sacred Heart"));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, panel.Nav.TrailOf(PanelTab.Search).Count);
        Assert.Equal("Sacred Heart", (string?)Assert.Single(Page(panel).Rows).Tag);

        window.Close();
    }

    private static AllResultsPage Page(PanelView panel) =>
        panel.GetVisualDescendants().OfType<AllResultsPage>().Single();
}
