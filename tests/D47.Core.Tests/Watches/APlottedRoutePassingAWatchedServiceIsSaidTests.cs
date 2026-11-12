using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using D47.Core.Tests.Knowledge;
using D47.Core.Watches;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Watches;

public class APlottedRoutePassingAWatchedServiceIsSaidTests
{
    private const string WatchesFile = @"C:\d47\data\watches.json";

    private const string Start = "Ngurii";

    private static readonly DateTimeOffset Now = new(2026, 11, 12, 20, 7, 0, TimeSpan.Zero);

    private sealed class FakeGalaxy : StubGalaxyService
    {
        public List<string> Asked { get; } = [];

        public Dictionary<string, IReadOnlyList<StationSummary>> Stations { get; } = new(StringComparer.OrdinalIgnoreCase);

        public override Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken)
        {
            var system = query.ReferenceSystem ?? string.Empty;
            Asked.Add(system);

            var stations = Stations.TryGetValue(system, out var found) ? found : [];
            return Task.FromResult(new StationSearchResult(query.ReferenceSystem, stations.Count, stations));
        }
    }

    private sealed class Rig
    {
        public required WatchStore Store { get; init; }

        public required FakeGalaxy Galaxy { get; init; }

        public required WatchRunner Runner { get; init; }

        public List<Func<Task>> Pending { get; } = [];

        public string? System { get; set; } = Start;

        public NavRoute Route { get; set; } = NavRoute.None;

        public bool Audible { get; set; } = true;

        public List<WatchNotice> Said { get; } = [];

        private DateTimeOffset _at = Now;

        /// <summary>The hops asked about, leaving out the system the Commander is in.</summary>
        public IEnumerable<string> HopsAsked => Galaxy.Asked.Where(system => system != Start);

        /// <summary>Polls, then finishes every query that poll started, <paramref name="times"/> times.</summary>
        public void Run(int times)
        {
            for (var i = 0; i < times; i++)
            {
                _at = _at.AddMilliseconds(100);
                Runner.Poll(_at);
                Said.AddRange(Runner.Ready);
                Finish();
            }
        }

        /// <summary>Polls once and leaves the queries it started unfinished.</summary>
        public void PollOnly()
        {
            _at = _at.AddMilliseconds(100);
            Runner.Poll(_at);
            Said.AddRange(Runner.Ready);
        }

        public void Finish()
        {
            var started = Pending.ToList();
            Pending.Clear();

            foreach (var work in started)
            {
                work().GetAwaiter().GetResult();
            }
        }
    }

    private static Rig Build()
    {
        var store = new WatchStore(WatchesFile, new MemoryFileSystem(), NullLogger<WatchStore>.Instance);
        var galaxy = new FakeGalaxy();
        Rig? rig = null;

        var runner = new WatchRunner(
            store,
            new StubTradePlanService(),
            () => null,
            () => [],
            NullLogger<WatchRunner>.Instance)
        {
            Dispatch = work => rig!.Pending.Add(work),
            Audible = () => rig!.Audible,
            CurrentSystem = () => rig!.System,
            Galaxy = () => galaxy,
            Route = () => rig!.Route,
        };

        rig = new Rig { Store = store, Galaxy = galaxy, Runner = runner };
        return rig;
    }

    private static Watch Manufactured(bool checkRoute = true) => new(
        Guid.NewGuid(), "Manufactured material trader", WatchKind.NextSystem,
        Service: "Material Trader", ServiceKind: "Manufactured", CheckRoute: checkRoute);

    /// <summary>The Commander's system, then <paramref name="jumps"/> hops named with <paramref name="prefix"/> and the jump number.</summary>
    private static NavRoute Plotted(int jumps, string prefix = "Hop", int minute = 0) => new()
    {
        Hops = [new RouteHop(Start, "K"), .. Enumerable.Range(1, jumps).Select(jump => new RouteHop($"{prefix} {jump}", "K"))],
        ReadAt = Now.AddMinutes(minute),
    };

    private static StationSummary Station(string system, string name = "Wohler Terminal") => new()
    {
        Name = name,
        SystemName = system,
        Type = "Coriolis Starport",
        DistanceToArrival = 240,
        TraderType = "Manufactured",
        Services = ["Market", "Material Trader"],
        SmallPads = 4,
        MediumPads = 6,
        LargePads = 4,
    };

    [Fact]
    public void TheFirstMatchingHopIsSaidOnceWithItsJumpAndNoLaterHopIsAsked()
    {
        var rig = Build();
        var watch = Manufactured();
        Assert.Null(rig.Store.Add(watch));
        rig.Galaxy.Stations["Hop 6"] = [Station("Hop 6")];
        rig.Galaxy.Stations["Hop 9"] = [Station("Hop 9", "Later Port")];
        rig.Route = Plotted(14);

        rig.Run(30);

        var notice = Assert.Single(rig.Said);
        Assert.Equal(watch.Id, notice.WatchId);
        Assert.Equal(
            "Your route passes a Manufactured material trader, Commander: Hop 6, jump 6 of 14. Wohler Terminal, 240 light seconds, large pad.",
            notice.Text);
        Assert.Equal(["Hop 1", "Hop 2", "Hop 3", "Hop 4", "Hop 5", "Hop 6"], rig.HopsAsked);
        Assert.True(Assert.Single(rig.Store.Watches).Enabled);
    }

    [Fact]
    public void ASecondRouteRestartsFromItsOwnFirstHop()
    {
        var rig = Build();
        Assert.Null(rig.Store.Add(Manufactured()));
        rig.Route = Plotted(14);

        rig.Run(3);
        Assert.Equal(["Hop 1", "Hop 2", "Hop 3"], rig.HopsAsked);

        rig.PollOnly();
        Assert.Single(rig.Pending);

        // Plotted while Hop 4 is still being asked about: nothing more is asked until that answer is in.
        rig.Route = Plotted(5, "Other", minute: 1);
        rig.PollOnly();
        Assert.Single(rig.Pending);

        rig.Finish();
        Assert.Equal("Hop 4", rig.HopsAsked.Last());
        rig.Run(10);

        Assert.Equal(["Hop 1", "Hop 2", "Hop 3", "Hop 4", "Other 1", "Other 2", "Other 3", "Other 4", "Other 5"], rig.HopsAsked);
        Assert.Empty(rig.Said);
    }

    [Fact]
    public void ANewRouteSaysAWatchAgain()
    {
        var rig = Build();
        Assert.Null(rig.Store.Add(Manufactured()));
        rig.Galaxy.Stations["Hop 2"] = [Station("Hop 2")];
        rig.Galaxy.Stations["Other 3"] = [Station("Other 3")];
        rig.Route = Plotted(4);

        rig.Run(10);
        rig.Route = Plotted(4, "Other", minute: 1);
        rig.Run(10);

        Assert.Equal(
            ["Hop 2, jump 2 of 4", "Other 3, jump 3 of 4"],
            rig.Said.Select(notice => notice.Text.Split(": ")[1].Split('.')[0]));
    }

    [Fact]
    public void ARouteOfSixtyHopsAsksAtMostForty()
    {
        var rig = Build();
        Assert.Null(rig.Store.Add(Manufactured()));
        rig.Route = Plotted(60);

        rig.Run(80);

        Assert.Equal(WatchRunner.MostRouteHops, rig.HopsAsked.Count());
        Assert.Equal("Hop 40", rig.HopsAsked.Last());
    }

    [Fact]
    public void NoHopIsAskedForAWatchWithItsRouteBoxClear()
    {
        var rig = Build();
        Assert.Null(rig.Store.Add(Manufactured(checkRoute: false)));
        rig.Route = Plotted(14);

        rig.Run(10);

        Assert.Empty(rig.HopsAsked);
    }

    [Fact]
    public void AClearedRouteStopsTheChecks()
    {
        var rig = Build();
        Assert.Null(rig.Store.Add(Manufactured()));
        rig.Route = Plotted(14);

        rig.Run(2);
        rig.Route = NavRoute.None;
        rig.Run(10);

        Assert.Equal(["Hop 1", "Hop 2"], rig.HopsAsked);
    }

    [Fact]
    public void AHopAlreadyPassedIsSkippedAndTheJumpIsCountedFromWhereTheCommanderIs()
    {
        var rig = Build();
        Assert.Null(rig.Store.Add(Manufactured()));
        rig.Galaxy.Stations["Hop 6"] = [Station("Hop 6")];
        rig.Route = Plotted(14);

        rig.Run(1);
        rig.System = "Hop 3";
        rig.Run(10);

        Assert.Equal(["Hop 1", "Hop 4", "Hop 5", "Hop 6"], rig.HopsAsked.Where(system => system != "Hop 3"));
        Assert.Contains("Hop 6, jump 3 of 11.", Assert.Single(rig.Said).Text, StringComparison.Ordinal);
    }

    [Fact]
    public void NothingIsAskedWhileTheLineWouldNotBeSaid()
    {
        var rig = Build();
        Assert.Null(rig.Store.Add(Manufactured()));
        rig.Audible = false;
        rig.Route = Plotted(14);

        rig.Run(10);

        Assert.Empty(rig.HopsAsked);
    }
}
