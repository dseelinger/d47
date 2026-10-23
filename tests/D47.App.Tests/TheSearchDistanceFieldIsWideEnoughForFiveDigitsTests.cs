using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;
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

/// <summary>The distance field on the four search pages is a full-width Amount that shows a thousands separator (#1537).</summary>
public sealed class TheSearchDistanceFieldIsWideEnoughForFiveDigitsTests
{
    private const string Here = "LTT 7786";

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

    private sealed class Galaxy : IGalaxyService
    {
        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ColonisationScan> ScanForColonisationAsync(ColonisationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<MissionStationScan> FindMissionStationsAsync(MissionStationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<BodySummary>> SystemBodiesAsync(long systemAddress, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private static (PanelView Panel, Window Window) Shown(string root, bool headset)
    {
        var settings = TestSurface.Settings();
        settings.Apply(GalaxyCapability.EnabledKey, "true", SettingsCaller.Panel);
        var board = new GalaxySearchBoard();
        var registry = CapabilityRegistry.Build([GalaxyCapability.Create(new Galaxy(), () => Here, settings, searches: board)]);

        var surface = new StarSystemSurface(
            new Systems(),
            () => new JournalLocation(Here, null, false, null),
            () => settings.Current.Knowledge.GalaxySearch,
            () => { })
        {
            Searches = new GalaxySearchSurface(
                registry,
                board,
                new SavedSearchStore("saved-searches.json", new MemoryFileSystem(), NullLogger<SavedSearchStore>.Instance),
                () => "F1",
                () => [])
            {
                PrefixNames = new Systems().PrefixNamesAsync,
                SuggestionPause = TimeSpan.Zero,
            },
        };

        var panel = new PanelView { DataContext = new PanelViewModel() };

        if (headset)
        {
            panel.Classes.Add("headset");
        }

        panel.EnableStarSystem(surface);

        var window = new Window { Content = panel, Width = 1280, Height = 960 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        panel.Tab = PanelTab.Search;
        panel.Nav.SelectRoot(root);
        Dispatcher.UIThread.RunJobs();

        return (panel, window);
    }

    [AvaloniaTheory]
    [InlineData(StationSearchPage.RootKey, false)]
    [InlineData(StationSearchPage.RootKey, true)]
    [InlineData(OutfittingSearchPage.RootKey, false)]
    [InlineData(OutfittingSearchPage.RootKey, true)]
    [InlineData(BodySearchPage.RootKey, false)]
    [InlineData(BodySearchPage.RootKey, true)]
    [InlineData(SignalSearchPage.RootKey, false)]
    [InlineData(SignalSearchPage.RootKey, true)]
    public void ADistanceOfOneThousandReadsWithItsSeparatorAndIsWrittenWithout(string root, bool headset)
    {
        using var look = AppLook.Put();

        var (panel, window) = Shown(root, headset);
        var page = panel.GetVisualDescendants().OfType<GalaxySearchPage>().Single();
        var distance = page.GetVisualDescendants().OfType<Amount>().First();

        Assert.Equal(Amount.CompactWidth, distance.Width);

        distance.Value = 1000;
        Dispatcher.UIThread.RunJobs();

        var shown = distance.GetVisualDescendants().OfType<TextBlock>().Single(block => block.Name == "AmountValue");
        Assert.Equal("1,000", shown.Text);
        Assert.Equal("1000", page.FormArguments()["max_distance"]);

        window.Close();
    }
}
