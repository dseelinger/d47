using D47.Core.Knowledge;
using D47.Core.Storage;
using D47.Core.Tests.Knowledge;
using D47.Core.Watches;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Watches;

public class ANextSystemWatchSpeaksOnArrivalTests
{
    private const string WatchesFile = @"C:\d47\data\watches.json";

    private static readonly DateTimeOffset Now = new(2026, 11, 12, 20, 31, 0, TimeSpan.Zero);

    private sealed class FakeGalaxy : StubGalaxyService
    {
        public List<StationQuery> Asked { get; } = [];

        public IReadOnlyList<StationSummary> Stations { get; set; } = [];

        public Exception? Fails { get; set; }

        public override Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken)
        {
            Asked.Add(query);

            return Fails is { } failure
                ? Task.FromException<StationSearchResult>(failure)
                : Task.FromResult(new StationSearchResult(query.ReferenceSystem, Stations.Count, Stations));
        }
    }

    /// <summary>A memory file system whose writes fail while <see cref="Locked"/> is true.</summary>
    private sealed class LockableFiles : IFileSystem
    {
        private readonly MemoryFileSystem _inner = new();

        public bool Locked { get; set; }

        public FileState? Stat(string path) => _inner.Stat(path);

        public DateTime? FolderWritten(string folder) => _inner.FolderWritten(folder);

        public string? ReadText(string path) => _inner.ReadText(path);

        public byte[]? ReadBytes(string path) => _inner.ReadBytes(path);

        public Stream? OpenRead(string path) => _inner.OpenRead(path);

        public void WriteText(string path, string contents)
        {
            if (Locked)
            {
                throw new IOException("locked");
            }

            _inner.WriteText(path, contents);
        }

        public void WriteBytes(string path, byte[] contents) => _inner.WriteBytes(path, contents);

        public void AppendText(string path, string contents) => _inner.AppendText(path, contents);

        public void CreateFolder(string folder) => _inner.CreateFolder(folder);

        public void DeleteFolder(string folder) => _inner.DeleteFolder(folder);

        public void Delete(string path) => _inner.Delete(path);

        public void Copy(string from, string to) => _inner.Copy(from, to);

        public IReadOnlyList<string> Enumerate(string folder, string pattern, bool recursive = false) =>
            _inner.Enumerate(folder, pattern, recursive);
    }

    private sealed class Rig
    {
        public required WatchStore Store { get; init; }

        public required FakeGalaxy Galaxy { get; init; }

        public required WatchRunner Runner { get; init; }

        public List<Func<Task>> Pending { get; } = [];

        public string? System { get; set; }

        public bool Audible { get; set; } = true;

        public bool HasGalaxy { get; set; } = true;

        /// <summary>Arrives in <paramref name="system"/>, finishes the query it started, and polls again a moment later.</summary>
        public IReadOnlyList<WatchNotice> Arrive(string system, DateTimeOffset at)
        {
            System = system;
            Runner.Poll(at);
            var first = Runner.Ready;
            Finish();
            Runner.Poll(at.AddMilliseconds(100));

            return [.. first, .. Runner.Ready];
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

    private static Rig Build(IFileSystem? files = null)
    {
        var store = new WatchStore(WatchesFile, files ?? new MemoryFileSystem(), NullLogger<WatchStore>.Instance);
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
            Galaxy = () => rig!.HasGalaxy ? galaxy : null,
        };

        rig = new Rig { Store = store, Galaxy = galaxy, Runner = runner };
        return rig;
    }

    private static Watch Manufactured(PadSize? pad = null) => new(
        Guid.NewGuid(), "Manufactured material trader", WatchKind.NextSystem,
        Service: "Material Trader", ServiceKind: "Manufactured", MinPad: pad);

    private static StationSummary Station(
        string name,
        string system = "Kremainn",
        string type = "Coriolis Starport",
        double distance = 240,
        string? trader = "Manufactured",
        int largePads = 4) => new()
    {
        Name = name,
        SystemName = system,
        Type = type,
        DistanceToArrival = distance,
        TraderType = trader,
        Services = ["Market", "Material Trader"],
        SmallPads = 4,
        MediumPads = 6,
        LargePads = largePads,
    };

    [Fact]
    public void AMatchingStationSaysTheLineOnceAndTurnsTheWatchOff()
    {
        var rig = Build();
        var watch = Manufactured();
        Assert.Null(rig.Store.Add(watch));
        rig.Galaxy.Stations = [Station("Wohler Terminal")];

        var ready = rig.Arrive("Kremainn", Now);

        var notice = Assert.Single(ready);
        Assert.Equal(watch.Id, notice.WatchId);
        Assert.Equal(
            "Kremainn has a Manufactured material trader, Commander. Wohler Terminal, 240 light seconds, large pad. That watch is off now.",
            notice.Text);

        var stored = Assert.Single(rig.Store.Watches);
        Assert.False(stored.Enabled);
        Assert.Equal("Kremainn", stored.FiredSystem);
        Assert.Equal(Now.AddMilliseconds(100), stored.FiredAt);

        Assert.Empty(rig.Arrive("Ngurii", Now.AddMinutes(5)));
        Assert.Empty(rig.Arrive("Kremainn", Now.AddMinutes(10)));
    }

    [Fact]
    public void TheQueryAsksForEveryFixedStationInTheArrivedSystem()
    {
        var rig = Build();
        Assert.Null(rig.Store.Add(Manufactured()));

        rig.Arrive("Kremainn", Now);

        var query = Assert.Single(rig.Galaxy.Asked);
        Assert.Equal("Kremainn", query.ReferenceSystem);
        Assert.Equal(1, query.MaxDistance);
        Assert.True(query.FixedOnly);
        Assert.Empty(query.Services);
        Assert.Null(query.TraderType);
        Assert.Null(query.TechnologyBroker);
        Assert.True(query.Size > 20);
    }

    [Fact]
    public void WhereSeveralStationsMatchTheNearestIsNamed()
    {
        var rig = Build();
        Assert.Null(rig.Store.Add(Manufactured()));
        rig.Galaxy.Stations = [Station("Far Port", distance: 1180), Station("Wohler Terminal", distance: 240)];

        var notice = Assert.Single(rig.Arrive("Kremainn", Now));

        Assert.Contains("Wohler Terminal, 240 light seconds", notice.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ACarrierAWrongTraderAndATooSmallPadEachFireNothing()
    {
        var rig = Build();
        var watch = Manufactured(PadSize.Large);
        Assert.Null(rig.Store.Add(watch));
        rig.Galaxy.Stations =
        [
            Station("X7Z-L2K", type: "Drake-Class Carrier", distance: 10),
            Station("Raw Port", trader: "Raw", distance: 20),
            Station("Small Outpost", type: "Outpost", largePads: 0, distance: 30),
        ];

        Assert.Empty(rig.Arrive("Kremainn", Now));
        Assert.True(Assert.Single(rig.Store.Watches).Enabled);
    }

    [Fact]
    public void TwoWatchesShareOneQueryPerArrival()
    {
        var rig = Build();
        var manufactured = Manufactured();
        var raw = new Watch(Guid.NewGuid(), "Raw material trader", WatchKind.NextSystem, Service: "Material Trader", ServiceKind: "Raw");
        Assert.Null(rig.Store.Add(manufactured));
        Assert.Null(rig.Store.Add(raw));
        rig.Galaxy.Stations = [Station("Wohler Terminal")];

        var notice = Assert.Single(rig.Arrive("Kremainn", Now));

        Assert.Equal(manufactured.Id, notice.WatchId);
        Assert.Single(rig.Galaxy.Asked);

        rig.Arrive("Ngurii", Now.AddMinutes(5));
        Assert.Equal(2, rig.Galaxy.Asked.Count);
    }

    [Fact]
    public void StayingInTheSameSystemSendsNoSecondQuery()
    {
        var rig = Build();
        Assert.Null(rig.Store.Add(Manufactured()));

        rig.Arrive("Kremainn", Now);
        rig.Arrive("Kremainn", Now.AddMinutes(1));

        Assert.Single(rig.Galaxy.Asked);
    }

    [Fact]
    public void NoQueryIsSentWithoutAnEnabledNextSystemWatch()
    {
        var rig = Build();
        Assert.Null(rig.Store.Add(Manufactured() with { Enabled = false }));
        Assert.Null(rig.Store.Add(new Watch(Guid.NewGuid(), "New Community Goals", WatchKind.NewCommunityGoal)));

        rig.Arrive("Kremainn", Now);

        Assert.Empty(rig.Galaxy.Asked);
    }

    [Fact]
    public void NoQueryIsSentWhileTheLineWouldNotBeSaid()
    {
        var rig = Build();
        Assert.Null(rig.Store.Add(Manufactured()));
        rig.Audible = false;

        rig.Arrive("Kremainn", Now);

        Assert.Empty(rig.Galaxy.Asked);
    }

    [Fact]
    public void NoSystemSendsNoQuery()
    {
        var rig = Build();
        Assert.Null(rig.Store.Add(Manufactured()));

        rig.Runner.Poll(Now);
        rig.Finish();

        Assert.Empty(rig.Galaxy.Asked);
    }

    [Fact]
    public void AFailedQueryOrNoStationSearchFiresNothing()
    {
        var rig = Build();
        Assert.Null(rig.Store.Add(Manufactured()));
        rig.Galaxy.Fails = new GalaxyUnavailableException("Spansh took too long to answer.");

        Assert.Empty(rig.Arrive("Kremainn", Now));

        rig.HasGalaxy = false;
        rig.Galaxy.Fails = null;
        rig.Galaxy.Stations = [Station("Wohler Terminal", system: "Ngurii")];

        Assert.Empty(rig.Arrive("Ngurii", Now.AddMinutes(5)));
        Assert.Single(rig.Galaxy.Asked);
        Assert.True(Assert.Single(rig.Store.Watches).Enabled);
    }

    [Fact]
    public void AnAnswerForASystemAlreadyLeftIsNotSaid()
    {
        var rig = Build();
        Assert.Null(rig.Store.Add(Manufactured()));
        rig.Galaxy.Stations = [Station("Wohler Terminal")];

        rig.System = "Kremainn";
        rig.Runner.Poll(Now);
        rig.System = "Ngurii";
        rig.Finish();
        rig.Runner.Poll(Now.AddSeconds(1));

        Assert.Empty(rig.Runner.Ready);
        Assert.True(rig.Store.Watches.Single().Enabled);
    }

    [Fact]
    public void ALineIsHeldUntilTheFileCanBeWritten()
    {
        var files = new LockableFiles();
        var rig = Build(files);
        Assert.Null(rig.Store.Add(Manufactured()));
        rig.Galaxy.Stations = [Station("Wohler Terminal")];
        files.Locked = true;

        Assert.Empty(rig.Arrive("Kremainn", Now));
        rig.Runner.Poll(Now.AddSeconds(1));
        Assert.Empty(rig.Runner.Ready);

        files.Locked = false;
        rig.Runner.Poll(Now.AddSeconds(2));
        Assert.Single(rig.Runner.Ready);

        rig.Runner.Poll(Now.AddSeconds(3));
        Assert.Empty(rig.Runner.Ready);
        Assert.Single(rig.Galaxy.Asked);
    }
}
