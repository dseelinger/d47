using D47.Core.Knowledge;
using D47.Core.Storage;
using D47.Core.Tests.Knowledge;
using D47.Core.Watches;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Watches;

public class StockWatchesAreCheckedInTheBackgroundTests
{
    private const string WatchesFile = @"C:\d47\data\watches.json";

    private static readonly DateTimeOffset Tick = new(2026, 11, 3, 6, 40, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Start = Tick.AddHours(2);

    private sealed class FakeTrade : StubTradePlanService
    {
        public List<(long MarketId, string Commodity)> Asked { get; } = [];

        public Func<long, StationQuote?> Answer { get; set; } = _ => null;

        public Exception? Fails { get; set; }

        public override Task<StationQuote?> QuoteAsync(long marketId, string commodity, CancellationToken cancellationToken)
        {
            Asked.Add((marketId, commodity));

            return Fails is { } failure ? Task.FromException<StationQuote?>(failure) : Task.FromResult(Answer(marketId));
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

        public void Delete(string path) => _inner.Delete(path);

        public void Copy(string from, string to) => _inner.Copy(from, to);

        public IReadOnlyList<string> Enumerate(string folder, string pattern, bool recursive = false) =>
            _inner.Enumerate(folder, pattern, recursive);
    }

    private sealed class Rig
    {
        public required WatchStore Store { get; init; }

        public required FakeTrade Trade { get; init; }

        public required WatchRunner Runner { get; init; }

        public List<Func<Task>> Pending { get; } = [];

        public DateTimeOffset? LastTick { get; set; } = Tick;

        public bool GalaxySearch { get; set; } = true;

        public bool BgsTick { get; set; } = true;

        /// <summary>Finishes every quote started so far.</summary>
        public void Finish()
        {
            var started = Pending.ToList();
            Pending.Clear();

            foreach (var work in started)
            {
                work().GetAwaiter().GetResult();
            }
        }

        /// <summary>Polls, finishes the quote it started, and polls again a moment later.</summary>
        public IReadOnlyList<WatchNotice> Check(DateTimeOffset now)
        {
            Runner.Poll(now);
            Finish();
            Runner.Poll(now.AddMilliseconds(100));
            return Runner.Ready;
        }
    }

    private static Rig Build(IFileSystem? files = null)
    {
        var store = new WatchStore(WatchesFile, files ?? new MemoryFileSystem(), NullLogger<WatchStore>.Instance);
        var trade = new FakeTrade();
        Rig? rig = null;

        var runner = new WatchRunner(
            store,
            trade,
            () => rig!.LastTick,
            () => rig!.GalaxySearch,
            () => rig!.BgsTick,
            () => [],
            () => false,
            NullLogger<WatchRunner>.Instance)
        {
            Dispatch = work => rig!.Pending.Add(work),
        };

        rig = new Rig { Store = store, Trade = trade, Runner = runner };
        return rig;
    }

    private static Watch Relics(long marketId = 128000001) => new(
        Guid.NewGuid(), "Relics", WatchKind.Stock,
        MarketId: marketId, Station: "Cheranovsky City", System: "Ngurii", Commodity: "Soontill Relics",
        Comparison: StockComparison.Above, Threshold: 100);

    private static StationQuote Stock(int stock, DateTimeOffset at) => new(stock, 2, 25_000, 0, 0, at);

    [Fact]
    public void AHoldingQuoteFiresOnceAndASecondInTheSameTickDoesNot()
    {
        var rig = Build();
        var watch = Relics();
        Assert.Null(rig.Store.Add(watch));
        rig.Trade.Answer = _ => Stock(240, Start);

        var ready = rig.Check(Start);

        var notice = Assert.Single(ready);
        Assert.Equal(watch.Id, notice.WatchId);
        Assert.Equal("Soontill Relics at Cheranovsky City, Ngurii: 240 in stock.", notice.Text);
        Assert.Equal(BgsTickWatch.Format(Tick), rig.Store.Watches.Single().FiredForTick);

        Assert.Empty(rig.Check(Start.AddMinutes(31)));
        Assert.Empty(rig.Check(Start.AddHours(5)));
        Assert.Single(rig.Trade.Asked);
    }

    [Fact]
    public void AQuoteThatComesBackAfterTheWatchFiredThisTickDoesNotFire()
    {
        var rig = Build();
        var watch = Relics();
        Assert.Null(rig.Store.Add(watch));
        rig.Trade.Answer = _ => Stock(240, Start);

        rig.Runner.Poll(Start);
        Assert.True(rig.Store.MarkFired(watch.Id, Tick));
        rig.Finish();
        rig.Runner.Poll(Start.AddMilliseconds(100));

        Assert.Empty(rig.Runner.Ready);
    }

    [Fact]
    public void TheNextTickFiresAgainWhileTheStockStillHolds()
    {
        var rig = Build();
        Assert.Null(rig.Store.Add(Relics()));
        rig.Trade.Answer = _ => Stock(240, Start);
        Assert.Single(rig.Check(Start));

        var later = Start.AddDays(1);
        rig.LastTick = Tick.AddDays(1);
        rig.Trade.Answer = _ => Stock(180, later);

        var notice = Assert.Single(rig.Check(later));
        Assert.Equal("Soontill Relics at Cheranovsky City, Ngurii: 180 in stock.", notice.Text);
        Assert.Equal(BgsTickWatch.Format(Tick.AddDays(1)), rig.Store.Watches.Single().FiredForTick);
    }

    [Fact]
    public void NothingIsRequestedWhileGalaxySearchIsOff()
    {
        var rig = Build();
        Assert.Null(rig.Store.Add(Relics()));
        rig.GalaxySearch = false;

        rig.Check(Start);

        Assert.Empty(rig.Trade.Asked);
    }

    [Fact]
    public void NothingIsRequestedWhileBgsTickTimeIsOff()
    {
        var rig = Build();
        Assert.Null(rig.Store.Add(Relics()));
        rig.BgsTick = false;

        rig.Check(Start);

        Assert.Empty(rig.Trade.Asked);
    }

    [Fact]
    public void NothingIsRequestedWhileNoTickIsKnown()
    {
        var rig = Build();
        Assert.Null(rig.Store.Add(Relics()));
        rig.LastTick = null;

        rig.Check(Start);

        Assert.Empty(rig.Trade.Asked);
    }

    [Fact]
    public void NothingIsRequestedForADisabledWatch()
    {
        var rig = Build();
        Assert.Null(rig.Store.Add(Relics() with { Enabled = false }));

        rig.Check(Start);

        Assert.Empty(rig.Trade.Asked);
    }

    [Fact]
    public void NothingIsRequestedOrMarkedWhileTheLineWouldNotBeSaid()
    {
        var rig = Build();
        Assert.Null(rig.Store.Add(Relics()));
        rig.Trade.Answer = _ => Stock(240, Start);
        var audible = false;
        rig.Runner.Audible = () => audible;

        Assert.Empty(rig.Check(Start));
        Assert.Empty(rig.Trade.Asked);

        audible = true;
        Assert.Single(rig.Check(Start.AddMinutes(1)));
    }

    [Fact]
    public void AQuoteThatComesBackAfterASwitchWentOffIsNotSaid()
    {
        var rig = Build();
        Assert.Null(rig.Store.Add(Relics()));
        rig.Trade.Answer = _ => Stock(240, Start);

        rig.Runner.Poll(Start);
        rig.GalaxySearch = false;
        rig.Finish();
        rig.Runner.Poll(Start.AddMilliseconds(100));

        Assert.Empty(rig.Runner.Ready);
        Assert.Null(rig.Store.Watches.Single().FiredForTick);
    }

    [Fact]
    public void OnlyOneQuoteIsInFlightAtATime()
    {
        var rig = Build();
        Assert.Null(rig.Store.Add(Relics(1)));
        Assert.Null(rig.Store.Add(Relics(2)));

        rig.Runner.Poll(Start);
        rig.Runner.Poll(Start.AddMilliseconds(100));
        rig.Runner.Poll(Start.AddMinutes(1));

        Assert.Single(rig.Pending);

        rig.Finish();
        rig.Runner.Poll(Start.AddMinutes(1).AddMilliseconds(100));

        Assert.Single(rig.Pending);
        rig.Finish();
        Assert.Equal([1L, 2L], rig.Trade.Asked.Select(asked => asked.MarketId));
    }

    [Fact]
    public void AWatchChecked29MinutesAgoIsNotCheckedAgain()
    {
        var rig = Build();
        Assert.Null(rig.Store.Add(Relics()));
        rig.Trade.Answer = _ => Stock(40, Start);

        Assert.Empty(rig.Check(Start));
        Assert.Empty(rig.Check(Start.AddMinutes(29)));
        Assert.Single(rig.Trade.Asked);

        Assert.Empty(rig.Check(Start.AddMinutes(30)));
        Assert.Equal(2, rig.Trade.Asked.Count);
    }

    [Fact]
    public void AMarketWithNoReportDoesNotHold()
    {
        var rig = Build();
        Assert.Null(rig.Store.Add(Relics()));
        rig.Trade.Answer = _ => null;

        Assert.Empty(rig.Check(Start));
        Assert.Null(rig.Store.Watches.Single().FiredForTick);
    }

    [Fact]
    public void AnUnknownCommodityCountsAsACheckAndDoesNotFire()
    {
        var rig = Build();
        Assert.Null(rig.Store.Add(Relics()));
        rig.Trade.Fails = new GalaxyUnavailableException("The market index has no commodity called \"Soontill Relics\".");

        Assert.Empty(rig.Check(Start));
        Assert.Empty(rig.Check(Start.AddMinutes(29)));

        Assert.Single(rig.Trade.Asked);
        Assert.Null(rig.Store.Watches.Single().FiredForTick);
    }

    [Fact]
    public void AnyOtherFailureCountsAsACheckAndDoesNotFire()
    {
        var rig = Build();
        Assert.Null(rig.Store.Add(Relics()));
        rig.Trade.Fails = new HttpRequestException("refused");

        Assert.Empty(rig.Check(Start));
        Assert.Empty(rig.Check(Start.AddMinutes(29)));

        Assert.Single(rig.Trade.Asked);
    }

    [Fact]
    public void AWatchMovedToAnotherMarketWhileItsQuoteWasOutDoesNotFireOnTheOldOne()
    {
        var rig = Build();
        var watch = Relics(1);
        Assert.Null(rig.Store.Add(watch));
        rig.Trade.Answer = _ => Stock(240, Start);

        rig.Runner.Poll(Start);
        Assert.Null(rig.Store.Update(watch with { MarketId = 2 }));
        rig.Finish();
        rig.Runner.Poll(Start.AddMilliseconds(100));

        Assert.Empty(rig.Runner.Ready);
    }

    [Fact]
    public void AFiringThatCannotBeStoredIsNotSaid()
    {
        var files = new LockableFiles();
        var rig = Build(files);
        Assert.Null(rig.Store.Add(Relics()));
        rig.Trade.Answer = _ => Stock(240, Start);
        files.Locked = true;

        Assert.Empty(rig.Check(Start));
        Assert.Empty(rig.Check(Start.AddMinutes(30)));
        Assert.Equal(2, rig.Trade.Asked.Count);

        files.Locked = false;
        Assert.Single(rig.Check(Start.AddMinutes(60)));
    }

    [Fact]
    public void ALineLeavesOutAMissingStationOrSystem()
    {
        var quote = Stock(12_000, Start);

        Assert.Equal("Soontill Relics at Ngurii: 12,000 in stock.", WatchRunner.Line(Relics() with { Station = null }, quote));
        Assert.Equal("Soontill Relics at Cheranovsky City: 12,000 in stock.", WatchRunner.Line(Relics() with { System = " " }, quote));
        Assert.Equal("Soontill Relics: 12,000 in stock.", WatchRunner.Line(Relics() with { Station = null, System = null }, quote));
    }
}
