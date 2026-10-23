using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
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

/// <summary>The Stations, Outfitting and Signals distance fields take up to 99,999 light years (#1564).</summary>
public sealed class TheSearchDistanceFieldsGoUpToNinetyNineThousandTests
{
    private static readonly DateTimeOffset AskedAt = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

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
        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ColonisationScan> ScanForColonisationAsync(ColonisationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<MissionStationScan> FindMissionStationsAsync(MissionStationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<BodySummary>> SystemBodiesAsync(long systemAddress, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private static IReadOnlyDictionary<string, string> Filled(GalaxySearchKind kind, string root, object result, string distance)
    {
        var settings = TestSurface.Settings();
        settings.Apply(GalaxyCapability.EnabledKey, "true", SettingsCaller.Panel);

        var board = new GalaxySearchBoard();
        board.Post(new GalaxySearchPosting(
            kind,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["max_distance"] = distance },
            "LTT 7786",
            result,
            "Matched.",
            AskedAt));

        var history = new SearchHistory("search-history.json", new MemoryFileSystem(), NullLogger<SearchHistory>.Instance);
        var registry = CapabilityRegistry.Build([GalaxyCapability.Create(new Galaxy(), () => "LTT 7786", settings, searches: board, history: history)]);
        var store = new SavedSearchStore("saved-searches.json", new MemoryFileSystem(), NullLogger<SavedSearchStore>.Instance);

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
                History = history,
            },
        });

        var window = new Window { Content = panel, Width = 1280, Height = 960 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        panel.Tab = PanelTab.Search;
        panel.Nav.SelectRoot(root);
        Dispatcher.UIThread.RunJobs();

        var arguments = panel.GetVisualDescendants().OfType<GalaxySearchPage>().Single().FormArguments();
        window.Close();

        return arguments;
    }

    private static readonly StationSearchResult NoStations = new("LTT 7786", 0, []);

    private static readonly SignalSearchResult NoSignals = new("LTT 7786", 0, []);

    public static TheoryData<GalaxySearchKind, string, bool> Pages => new()
    {
        { GalaxySearchKind.Stations, StationSearchPage.RootKey, false },
        { GalaxySearchKind.Outfitting, OutfittingSearchPage.RootKey, false },
        { GalaxySearchKind.Signals, SignalSearchPage.RootKey, true },
    };

    [AvaloniaTheory]
    [MemberData(nameof(Pages))]
    public void ARadiusOverTheLimitShowsNinetyNineThousandNineHundredNinetyNine(GalaxySearchKind kind, string root, bool signals)
    {
        var arguments = Filled(kind, root, signals ? NoSignals : NoStations, "150000");

        Assert.Equal("99999", arguments["max_distance"]);
    }

    [AvaloniaTheory]
    [MemberData(nameof(Pages))]
    public void ARadiusOfTwentyThousandIsKept(GalaxySearchKind kind, string root, bool signals)
    {
        var arguments = Filled(kind, root, signals ? NoSignals : NoStations, "20000");

        Assert.Equal("20000", arguments["max_distance"]);
    }
}
