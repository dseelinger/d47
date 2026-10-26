using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>find_signal: the index and the Commander's own scans together, placed without Spansh where they can be.</summary>
public class FindSignalAnswersFromTheIndexAndOwnScansTests
{
    private const string HazardousRes = "$MULTIPLAYER_SCENARIO79_TITLE;";

    private static readonly DateTimeOffset Now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    private static readonly string Tsv = string.Join(
        "\n",
        SignalIndex.Header,
        "1001\tAlpha\t3\t0\t0\tResourceExtraction\t" + HazardousRes + "\t2026-10-10",
        "1001\tAlpha\t3\t0\t0\tInstallation\tUnauthorised Installation\t2026-10-12",
        "1002\tBeta\t0\t4\t0\tResourceExtraction\t" + HazardousRes + "\t2026-10-15",
        "1003\tGamma\t0\t0\t60\tResourceExtraction\t" + HazardousRes + "\t2026-10-15",
        "1004\tFar\t500\t0\t0\tResourceExtraction\t" + HazardousRes + "\t2026-10-15") + "\n";

    private sealed class FakeIndex(SignalIndex? index) : ISignalIndexSource
    {
        public Task<SignalIndex?> GetAsync(CancellationToken cancellationToken) => Task.FromResult(index);
    }

    private sealed class FakeSystems(Func<string, IReadOnlyList<SystemNameMatch>>? match = null) : IStarSystemService
    {
        public List<string> Asked { get; } = [];

        public Task<StarSystemProfile?> ProfileAsync(long systemAddress, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<SystemNameMatch>> MatchNamesAsync(string typed, CancellationToken cancellationToken)
        {
            Asked.Add(typed);

            return match is null ? throw new InvalidOperationException("Spansh was called.") : Task.FromResult(match(typed));
        }

        public Task<IReadOnlyList<SystemNameMatch>> PrefixNamesAsync(string typed, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PowerplayNeighbourhood> PowerplayNearAsync(string system, double lightYears, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private static string Jump(long address, string system, double x, double y, double z) =>
        "{\"timestamp\":\"2026-10-01T10:00:00Z\",\"event\":\"FSDJump\",\"StarSystem\":\"" + system
        + "\",\"SystemAddress\":" + address + ",\"StarPos\":[" + x + "," + y + "," + z + "]}";

    private static string Signal(string time, long address, string type, string name) =>
        "{\"timestamp\":\"" + time + "\",\"event\":\"FSSSignalDiscovered\",\"SystemAddress\":" + address
        + ",\"SignalName\":\"" + name + "\",\"SignalType\":\"" + type + "\"}";

    private static SignalSightings Own(bool folded = true)
    {
        var sightings = new SignalSightings(new MemoryFileSystem(), NullLogger.Instance);

        sightings.Fold(
        [
            Jump(2001, "Delta", 1.234, 2, 0),
            Signal("2026-10-18T09:00:00Z", 2001, "ResourceExtraction", HazardousRes),
            Jump(1001, "Alpha", 3, 0, 0),
            Signal("2026-10-17T09:00:00Z", 1001, "ResourceExtraction", HazardousRes),
        ]);

        if (folded)
        {
            sightings.FoldHistory([]);
        }

        return sightings;
    }

    private sealed record World(
        CapabilityRegistry Registry,
        GalaxySearchBoard Board,
        SearchHistory History,
        FakeSystems Systems);

    private static World Build(
        bool indexOn = true,
        bool galaxyOn = false,
        bool ownFolded = true,
        FakeSystems? systems = null,
        string? text = null)
    {
        var board = new GalaxySearchBoard();
        var history = new SearchHistory(
            Path.Combine(@"C:\d47-test", "search-history.json"),
            new MemoryFileSystem(),
            NullLogger<SearchHistory>.Instance);
        var settings = TestSurface.For(new MemoryInstall()).Settings;
        var fakeSystems = systems ?? new FakeSystems();

        settings.Apply(GalaxyCapability.EnabledKey, galaxyOn ? "true" : "false", SettingsCaller.Panel);
        settings.Apply(GalaxyCapability.SignalIndexKey, indexOn ? "true" : "false", SettingsCaller.Panel);

        var registry = CapabilityRegistry.Build(
        [
            GalaxyCapability.Create(
                null,
                () => "Sol",
                settings,
                now: () => Now,
                searches: board,
                systems: fakeSystems,
                history: history,
                signalIndex: new FakeIndex(SignalIndex.Read(new StringReader(text ?? Tsv))),
                signals: Own(ownFolded),
                currentPosition: () => StarPosition.Origin),
        ]);

        return new World(registry, board, history, fakeSystems);
    }

    private static Task<ToolResult> Ask(World world, params (string Name, string Value)[] values) =>
        world.Registry.InvokeAsync(
            "find_signal",
            new ToolArguments(values.ToDictionary(v => v.Name, v => v.Value, StringComparer.Ordinal)),
            TestContext.Current.CancellationToken);

    private static SignalSearchResult Posted(World world) =>
        Assert.IsType<SignalSearchResult>(world.Board.Last(GalaxySearchKind.Signals)!.Result);

    [Fact]
    public async Task AKindIsAnsweredNearestFirstWithTwoDecimals()
    {
        var world = Build();

        var result = await Ask(world, ("signal", "Hazardous RES"));

        Assert.False(result.IsError, result.Content);

        var found = Posted(world);

        Assert.Equal(["Delta", "Alpha", "Beta", "Gamma"], found.Signals.Select(f => f.System));
        Assert.Equal([2.35, 3.00, 4.00, 60.00], found.Signals.Select(f => f.Distance));
        Assert.All(found.Signals, f => Assert.Equal("Hazardous RES", f.Signal));
        Assert.Contains("The nearest is Hazardous RES in Delta, 2.35 light years from Sol", result.Content, StringComparison.Ordinal);
        Assert.Contains("last seen yesterday by you", result.Content, StringComparison.Ordinal);
        Assert.Contains("3 more within 100 light years", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASignalInBothSourcesShowsOnceWithTheLaterDate()
    {
        var world = Build();

        await Ask(world, ("signal", "Hazardous RES"));

        var alpha = Assert.Single(Posted(world).Signals, f => f.System == "Alpha");

        Assert.Equal(new DateTimeOffset(2026, 10, 17, 9, 0, 0, TimeSpan.Zero), alpha.LastSeen);
        Assert.True(alpha.OwnScan);
        Assert.False(Assert.Single(Posted(world).Signals, f => f.System == "Beta").OwnScan);
    }

    [Fact]
    public async Task APartOfANameMatchesTheSignal()
    {
        var world = Build();

        var result = await Ask(world, ("signal", "unauthorised"));

        Assert.False(result.IsError, result.Content);

        var found = Assert.Single(Posted(world).Signals);

        Assert.Equal("Unauthorised Installation", found.Signal);
        Assert.Equal("Alpha", found.System);
    }

    [Fact]
    public async Task ANameThatFindsNothingSaysHowNamesAreMatched()
    {
        var world = Build();

        var result = await Ask(world, ("signal", "memorial"));

        Assert.Contains("No memorial found within 100 light years of Sol.", result.Content, StringComparison.Ordinal);
        Assert.Contains("Sol-Pilotengedenkstätte", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ANearSystemOnlyTheIndexHasIsPlacedWithoutSpansh()
    {
        var world = Build(galaxyOn: true);

        var result = await Ask(world, ("signal", "Hazardous RES"), ("near", "Gamma"), ("max_distance", "10"));

        Assert.False(result.IsError, result.Content);
        Assert.Empty(world.Systems.Asked);

        var found = Assert.Single(Posted(world).Signals);

        Assert.Equal("Gamma", found.System);
        Assert.Equal(0, found.Distance);
        Assert.Equal("Gamma", world.Board.Last(GalaxySearchKind.Signals)!.Reference);
    }

    [Fact]
    public async Task ANearSystemNeitherSourceHasIsLookedUpWhileGalaxySearchIsOn()
    {
        var systems = new FakeSystems(_ => [new SystemNameMatch("Elsewhere", 9, new StarPosition(0, 0, 58))]);
        var world = Build(galaxyOn: true, systems: systems);

        var result = await Ask(world, ("signal", "Hazardous RES"), ("near", "Elsewhere"), ("max_distance", "5"));

        Assert.False(result.IsError, result.Content);
        Assert.Equal(["Elsewhere"], systems.Asked);
        Assert.Equal("Gamma", Assert.Single(Posted(world).Signals).System);
    }

    [Fact]
    public async Task ANearSystemNeitherSourceHasIsRefusedWhileGalaxySearchIsOff()
    {
        var world = Build(galaxyOn: false);

        var result = await Ask(world, ("signal", "Hazardous RES"), ("near", "Elsewhere"));

        Assert.True(result.IsError);
        Assert.Contains("I don't know where Elsewhere is", result.Content, StringComparison.Ordinal);
        Assert.Contains("turn Galaxy search on in settings", result.Content, StringComparison.Ordinal);
        Assert.Empty(world.Systems.Asked);
        Assert.Null(world.Board.Last(GalaxySearchKind.Signals));
    }

    [Fact]
    public async Task WithTheIndexOffOnlyTheOwnScansAreSearched()
    {
        var world = Build(indexOn: false);

        var result = await Ask(world, ("signal", "Hazardous RES"));

        Assert.Contains(
            "Searched your own scans only; the signal index is off in Settings.", result.Content, StringComparison.Ordinal);
        Assert.Equal(["Delta", "Alpha"], Posted(world).Signals.Select(f => f.System));
        Assert.All(Posted(world).Signals, f => Assert.True(f.OwnScan));
    }

    [Fact]
    public async Task BeforeTheBackfillIsDoneTheAnswerSaysTheScansAreLoading()
    {
        var loading = await Ask(Build(ownFolded: false), ("signal", "Hazardous RES"));
        var loaded = await Ask(Build(), ("signal", "Hazardous RES"));

        Assert.Contains("Your own scans are still loading.", loading.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("still loading", loaded.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NothingFoundSaysHowFarTheSearchLooked()
    {
        var world = Build();

        var result = await Ask(world, ("signal", "Titan"), ("max_distance", "25"));

        Assert.False(result.IsError, result.Content);
        Assert.StartsWith("No Titan found within 25 light years of Sol.", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("uploader", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnAnswerPostsUnderSignalsIsKeptInTheHistoryAndCanBeSaved()
    {
        var world = Build();

        await Ask(world, ("signal", "Any RES"));

        Assert.Equal(4, Posted(world).Total);
        Assert.Null(world.Board.Last(GalaxySearchKind.Bodies));

        var entry = Assert.Single(world.History.All());

        Assert.Equal("find_signal", entry.Tool);
        Assert.IsType<SignalSearchResult>(entry.Result);
        Assert.StartsWith("Signals", SearchWords.Describe(entry.Tool, entry.Arguments, entry.Reference), StringComparison.Ordinal);

        var store = new SavedSearchStore(
            Path.Combine(@"C:\d47-test", "saved-searches.json"),
            new MemoryFileSystem(),
            NullLogger<SavedSearchStore>.Instance);

        var outcome = store.Add("my RES", "find_signal", entry.Arguments, Now, []);

        Assert.True(outcome.Saved, outcome.Message);
        Assert.Equal("find_signal", Assert.Single(store.All()).Tool);
    }

    [Fact]
    public void TheSignalParameterNamesEveryKind()
    {
        var tool = GalaxyCapability.Create(null, () => null, TestSurface.For(new MemoryInstall()).Settings)
            .Tools.Single(t => t.Name == "find_signal");
        var description = tool.Parameters.Single(p => p.Name == "signal").Description;

        Assert.All(SignalKind.All, kind => Assert.Contains(kind.Name, description, StringComparison.Ordinal));
        Assert.Contains(SignalSearch.AnyRes, description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NearSetToTheCommandersOwnSystemNeedsNoLookup()
    {
        var world = Build(galaxyOn: false);

        var result = await Ask(world, ("signal", "Hazardous RES"), ("near", "sol"));

        Assert.False(result.IsError, result.Content);
        Assert.Equal("Delta", Posted(world).Signals[0].System);
        Assert.Equal("Sol", world.Board.Last(GalaxySearchKind.Signals)!.Reference);
    }

    [Fact]
    public async Task AMegashipIsListedOnceWhereItWasLastSeen()
    {
        var text = string.Join(
            "\n",
            SignalIndex.Header,
            "1002\tBeta\t0\t4\t0\tMegaship\tKolo Gryphon\t2026-10-01",
            "1003\tGamma\t0\t0\t60\tMegaship\tKolo Gryphon\t2026-10-15") + "\n";
        var world = Build(text: text);

        await Ask(world, ("signal", "Megaship"));

        var found = Assert.Single(Posted(world).Signals);

        Assert.Equal("Gamma", found.System);
    }

    [Fact]
    public async Task AReportAgeLeavesOutOlderSightingsAndSaysSo()
    {
        var world = Build();

        var result = await Ask(world, ("signal", "Hazardous RES"), ("max_report_age_hours", "72"));

        Assert.False(result.IsError, result.Content);
        Assert.Equal(["Delta", "Alpha"], Posted(world).Signals.Select(f => f.System));
        Assert.Contains("Only reports from the last 3 days were considered.", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AReportAgeOfZeroChangesNothing()
    {
        var world = Build();

        await Ask(world, ("signal", "Hazardous RES"), ("max_report_age_hours", "0"));

        Assert.Equal(["Delta", "Alpha", "Beta", "Gamma"], Posted(world).Signals.Select(f => f.System));
    }

    [Fact]
    public async Task AReportAgeOutOfRangeIsRefused()
    {
        var result = await Ask(Build(), ("signal", "Hazardous RES"), ("max_report_age_hours", "9000"));

        Assert.True(result.IsError);
    }
}
