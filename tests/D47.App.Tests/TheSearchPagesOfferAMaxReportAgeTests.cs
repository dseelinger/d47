using Avalonia.Controls;
using Avalonia.Headless;
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

/// <summary>The search pages offer a max report age that is sent as hours and restored from history (#1772).</summary>
public sealed class TheSearchPagesOfferAMaxReportAgeTests
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

    private static IReadOnlyDictionary<string, string> Filled(GalaxySearchKind kind, string root, object result, string age)
    {
        var settings = TestSurface.Settings();
        settings.Apply(GalaxyCapability.EnabledKey, "true", SettingsCaller.Panel);

        var board = new GalaxySearchBoard();
        board.Post(new GalaxySearchPosting(
            kind,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["max_report_age_hours"] = age },
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

        if (TestSurface.CapturesWanted && age == "720")
        {
            using var frame = window.CaptureRenderedFrame()!;
            frame.SaveCapture($"max-report-age-{root}");
        }

        window.Close();

        return arguments;
    }

    public static TheoryData<GalaxySearchKind, string, object> Pages => new()
    {
        { GalaxySearchKind.Systems, SystemSearchPage.RootKey, new GalaxySearchResult("LTT 7786", 0, []) },
        { GalaxySearchKind.Stations, StationSearchPage.RootKey, new StationSearchResult("LTT 7786", 0, []) },
        { GalaxySearchKind.Outfitting, OutfittingSearchPage.RootKey, new StationSearchResult("LTT 7786", 0, []) },
        { GalaxySearchKind.Signals, SignalSearchPage.RootKey, new SignalSearchResult("LTT 7786", 0, []) },
    };

    [AvaloniaTheory]
    [MemberData(nameof(Pages))]
    public void AReplayedAgeIsSentAgain(GalaxySearchKind kind, string root, object result)
    {
        Assert.Equal("720", Filled(kind, root, result, "720")["max_report_age_hours"]);
    }

    [AvaloniaTheory]
    [MemberData(nameof(Pages))]
    public void AnAgeOutsideTheListIsKept(GalaxySearchKind kind, string root, object result)
    {
        Assert.Equal("100", Filled(kind, root, result, "100")["max_report_age_hours"]);
    }

    [AvaloniaTheory]
    [MemberData(nameof(Pages))]
    public void AnyIsLeftOutWhereAnyIsTheStartingAge(GalaxySearchKind kind, string root, object result)
    {
        var arguments = Filled(kind, root, result, "0");

        if (kind is GalaxySearchKind.Stations or GalaxySearchKind.Outfitting)
        {
            Assert.Equal("0", arguments["max_report_age_hours"]);
        }
        else
        {
            Assert.DoesNotContain("max_report_age_hours", arguments.Keys);
        }
    }

    [AvaloniaFact]
    public void TheStartingAgeIsLeftOutOfTheArguments()
    {
        Assert.Null(new MaxReportAge(72).Asked);
        Assert.Equal("0", new MaxReportAge(72) { Value = MaxReportAge.Any }.Asked);
        Assert.Null(new MaxReportAge().Asked);
        Assert.Equal("72", new MaxReportAge { Value = 72 }.Asked);
    }

    [Theory]
    [InlineData(0, "Any")]
    [InlineData(1, "1 hour")]
    [InlineData(24, "1 day")]
    [InlineData(720, "30 days")]
    [InlineData(4320, "180 days")]
    [InlineData(100, "100 hours")]
    public void AnAgeIsNamedInHoursOrDays(int hours, string label)
    {
        Assert.Equal(label, MaxReportAge.Label(hours));
    }
}
