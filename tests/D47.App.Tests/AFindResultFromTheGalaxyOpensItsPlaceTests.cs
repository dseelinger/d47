using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>A Find result from the galaxy opens its system, its station filtered, or the Systems search for its faction (#1973).</summary>
public sealed class AFindResultFromTheGalaxyOpensItsPlaceTests
{
    private const long Wheel = 3932277478106;

    private sealed class Systems : IStarSystemService
    {
        public Task<StarSystemProfile?> ProfileAsync(long systemAddress, CancellationToken cancellationToken) =>
            Task.FromResult<StarSystemProfile?>(new StarSystemProfile
            {
                Name = "Dark Wheel's system",
                SystemAddress = systemAddress,
                Stations =
                [
                    new StationProfile { Name = "Dark Wheel", Type = "Orbis Starport" },
                    new StationProfile { Name = "Other Port", Type = "Outpost" },
                ],
            });

        public Task<IReadOnlyList<SystemNameMatch>> MatchNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public Task<IReadOnlyList<SystemNameMatch>> PrefixNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public Task<PowerplayNeighbourhood> PowerplayNearAsync(string system, double lightYears, CancellationToken cancellationToken) =>
            Task.FromResult(new PowerplayNeighbourhood(0, []));
    }

    private sealed class Galaxy : IGalaxyService
    {
        public List<GalaxyQuery> Asked { get; } = [];

        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken)
        {
            Asked.Add(query);
            return Task.FromResult(new GalaxySearchResult(null, 0, []));
        }

        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ColonisationScan> ScanForColonisationAsync(ColonisationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<MissionStationScan> FindMissionStationsAsync(MissionStationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<BodySummary>> SystemBodiesAsync(long systemAddress, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private static (PanelView Panel, Window Window, Galaxy Galaxy) Shown()
    {
        var settings = TestSurface.Settings();
        settings.Apply(GalaxyCapability.EnabledKey, "true", SettingsCaller.Panel);
        var store = new SavedSearchStore("saved-searches.json", new MemoryFileSystem(), NullLogger<SavedSearchStore>.Instance);
        var board = new GalaxySearchBoard();
        var galaxy = new Galaxy();
        var registry = CapabilityRegistry.Build([GalaxyCapability.Create(galaxy, () => "LTT 7786", settings, searches: board)]);

        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableStarSystem(new StarSystemSurface(
            new Systems(),
            () => new JournalLocation("LTT 7786", null, false, null),
            () => settings.Current.Knowledge.GalaxySearch,
            () => { })
        {
            Searches = new GalaxySearchSurface(registry, board, store, () => "F1", () => [])
            {
                PrefixNames = new Systems().PrefixNamesAsync,
                SuggestionPause = TimeSpan.Zero,
            },
        });

        var window = new Window { Content = panel, Width = 1280, Height = 960 };
        window.Show();
        Jobs();

        panel.Tab = PanelTab.Search;
        Jobs();

        return (panel, window, galaxy);
    }

    private static void Jobs()
    {
        for (var i = 0; i < 4; i++)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(20);
        }
    }

    [AvaloniaFact]
    public void AStationOpensItsSystemOnStationsWithItsNameInThePanelSearchBox()
    {
        var (panel, window, _) = Shown();

        panel.OpenFound(new PanelFindResult(PanelFindKind.Station, "Dark Wheel", "Station", "Dark Wheel", 2, false, Wheel, 64.3));
        Jobs();

        Assert.Equal(StarSystemPage.RootKey, panel.Nav.RootKeyOf(PanelTab.Search));
        var search = panel.GetVisualDescendants().OfType<TextBox>().Single(box => box.Name == "SearchInput");
        Assert.Equal("Dark Wheel", search.Text);
        Assert.Contains(panel.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == "Dark Wheel");
        Assert.DoesNotContain(panel.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == "Other Port");

        window.Close();
    }

    [AvaloniaFact]
    public void AFactionRunsTheSystemsSearchWithItAsAFactionPresent()
    {
        var (panel, window, galaxy) = Shown();

        panel.OpenFound(new PanelFindResult(PanelFindKind.Faction, "Dark Echo", "Minor faction", "Dark Echo", 2));
        Jobs();

        Assert.Equal(SystemSearchPage.RootKey, panel.Nav.RootKeyOf(PanelTab.Search));
        var criterion = Assert.Single(Assert.Single(galaxy.Asked).Criteria);
        Assert.Equal(["Dark Echo"], criterion.Choices);

        window.Close();
    }
}
