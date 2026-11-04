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

    private sealed class Systems : StubStarSystemService
    {
        public override Task<StarSystemProfile?> ProfileAsync(long systemAddress, CancellationToken cancellationToken) =>
            Task.FromResult<StarSystemProfile?>(null);

        public override Task<IReadOnlyList<SystemNameMatch>> MatchNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public override Task<IReadOnlyList<SystemNameMatch>> PrefixNamesAsync(string typed, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SystemNameMatch>>([]);

        public override Task<PowerplayNeighbourhood> PowerplayNearAsync(string system, double lightYears, CancellationToken cancellationToken) =>
            Task.FromResult(new PowerplayNeighbourhood(0, []));
    }

    private sealed class Galaxy : StubGalaxyService
    {

    }

    private static IReadOnlyDictionary<string, string> Filled(GalaxySearchKind kind, string root, object result, string distance)
    {
        var settings = TestSurface.Settings();

        var board = new GalaxySearchBoard();
        board.Post(new GalaxySearchPosting(
            kind,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["max_distance"] = distance },
            "LTT 7786",
            result,
            "Matched.",
            AskedAt));

        var (panel, window) = SearchPanelHosting.ShowSearchForm(settings, new Galaxy(), board, new Systems(), root);

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
