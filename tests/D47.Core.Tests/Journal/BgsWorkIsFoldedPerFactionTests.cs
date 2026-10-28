using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class BgsWorkIsFoldedPerFactionTests
{
    private const string Docked =
        """{"timestamp":"2026-10-10T10:00:00Z","event":"Docked","StationName":"Hutton Orbital","MarketID":100,"StarSystem":"Alpha","SystemAddress":1,"StationFaction":{"Name":"Alpha Mining"}}""";

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static BgsWork Fold(params string[] lines) => lines.Aggregate(BgsWork.None, (work, line) => work.Apply(Event(line)));

    private static string At(string time, string body) => "{\"timestamp\":\"" + time + "\"," + body + "}";

    private static string Sale(string time = "2026-10-10T10:05:00Z", long market = 100) =>
        At(time, "\"event\":\"MarketSell\",\"MarketID\":" + market
            + ",\"Type\":\"gold\",\"Count\":10,\"SellPrice\":50000,\"TotalSale\":500000,\"AvgPricePaid\":40000");

    private static string Voucher(string type, string rest) =>
        At("2026-10-10T10:06:00Z", "\"event\":\"RedeemVoucher\",\"Type\":\"" + type + "\"," + rest);

    private static string Mission(string trend, string marks) =>
        At("2026-10-10T10:07:00Z",
            "\"event\":\"MissionCompleted\",\"FactionEffects\":[{\"Faction\":\"Beta Union\",\"Influence\":[{\"SystemAddress\":2,\"Trend\":\""
            + trend + "\",\"Influence\":\"" + marks + "\"}]}]");

    [Fact]
    public void ATradeSaleIsRecordedForTheDockedStationsFactionWithItsProfit()
    {
        var entry = Assert.Single(Fold(Docked, Sale()).Entries);

        Assert.Equal(new BgsEntry(DateTimeOffset.Parse("2026-10-10T10:05:00Z"), 1, "Alpha Mining", BgsWorkKind.Trade, 500000, 100000), entry);
    }

    [Fact]
    public void ABountyVoucherIsRecordedPerFactionAndSkipsAnEmptyFactionName()
    {
        var work = Fold(
            Docked,
            Voucher("bounty", "\"Factions\":[{\"Faction\":\"Gamma Pirates\",\"Amount\":3000},{\"Faction\":\"\",\"Amount\":900}]"));

        var entry = Assert.Single(work.Entries);

        Assert.Equal((1L, "Gamma Pirates", BgsWorkKind.Bounty, 3000L), (entry.SystemAddress, entry.Faction, entry.Kind, entry.Amount));
    }

    [Fact]
    public void ACombatBondTakesTheVouchersFaction()
    {
        var entry = Assert.Single(Fold(Docked, Voucher("CombatBond", "\"Faction\":\"Delta Party\",\"Amount\":7000")).Entries);

        Assert.Equal(("Delta Party", BgsWorkKind.CombatBond, 7000L), (entry.Faction, entry.Kind, entry.Amount));
    }

    [Fact]
    public void CartographyIsRecordedForTheDockedStationsFaction()
    {
        var entry = Assert.Single(Fold(Docked, At("2026-10-10T10:08:00Z", "\"event\":\"SellExplorationData\",\"TotalEarnings\":123456")).Entries);

        Assert.Equal(("Alpha Mining", BgsWorkKind.Cartography, 123456L), (entry.Faction, entry.Kind, entry.Amount));
    }

    [Fact]
    public void OrganicSalesSumValueAndBonus()
    {
        var work = Fold(
            Docked,
            At("2026-10-10T10:08:00Z", "\"event\":\"SellOrganicData\",\"MarketID\":100,\"BioData\":[{\"Value\":1000,\"Bonus\":500},{\"Value\":200,\"Bonus\":0}]"));

        var entry = Assert.Single(work.Entries);

        Assert.Equal((BgsWorkKind.Organic, 1700L), (entry.Kind, entry.Amount));
    }

    [Theory]
    [InlineData("+++", "UpGood", 3)]
    [InlineData("+++", "DownBad", -3)]
    [InlineData("", "UpGood", 1)]
    public void MissionMarksAreSignedByTheirTrend(string marks, string trend, long expected)
    {
        var entry = Assert.Single(Fold(Mission(trend, marks)).Entries);

        Assert.Equal((2L, "Beta Union", BgsWorkKind.Mission, expected), (entry.SystemAddress, entry.Faction, entry.Kind, entry.Amount));
    }

    [Theory]
    [InlineData("UpBad")]
    [InlineData("DownGood")]
    [InlineData("None")]
    public void AnyOtherMissionTrendRecordsNothing(string trend) => Assert.Empty(Fold(Mission(trend, "++")).Entries);

    [Fact]
    public void ASaleAtAFleetCarrierRecordsNothing()
    {
        var carrier = Docked.Replace("Alpha Mining", "FleetCarrier", StringComparison.Ordinal);

        Assert.Empty(Fold(carrier, Sale()).Entries);
    }

    [Fact]
    public void ASaleWhileUndockedRecordsNothing() =>
        Assert.Empty(Fold(Docked, At("2026-10-10T10:02:00Z", "\"event\":\"Undocked\",\"MarketID\":100"), Sale()).Entries);

    [Fact]
    public void ASaleAtAnotherMarketRecordsNothing() => Assert.Empty(Fold(Docked, Sale(market: 200)).Entries);

    [Theory]
    [InlineData("codex", "\"Amount\":5000")]
    [InlineData("trade", "\"Amount\":5000")]
    [InlineData("settlement", "\"Amount\":5000")]
    public void VouchersThatNameNoFactionRecordNothing(string type, string rest) => Assert.Empty(Fold(Docked, Voucher(type, rest)).Entries);

    [Fact]
    public void ALocationEventWhileDockedSetsTheStation()
    {
        var work = Fold(
            At("2026-10-10T10:00:00Z", "\"event\":\"Location\",\"Docked\":true,\"MarketID\":100,\"StarSystem\":\"Alpha\",\"SystemAddress\":1,\"StationFaction\":{\"Name\":\"Alpha Mining\"}"),
            Sale());

        Assert.Single(work.Entries);
        Assert.Equal("Alpha", work.NameOf(1));
    }

    [Fact]
    public void SystemNamesComeFromJumpsAndAnUnseenAddressHasNone()
    {
        var work = Fold(At("2026-10-10T10:00:00Z", "\"event\":\"FSDJump\",\"StarSystem\":\"Beta\",\"SystemAddress\":2"));

        Assert.Equal("Beta", work.NameOf(2));
        Assert.Null(work.NameOf(3));
    }

    [Fact]
    public void AnEntryMoreThanSevenDaysOlderThanTheNewestEventIsGone()
    {
        var work = Fold(Docked, Sale(), At("2026-10-17T10:05:01Z", "\"event\":\"Undocked\",\"MarketID\":100"));

        Assert.Empty(work.Entries);
    }

    [Fact]
    public void AnEventInTheWalkAndTheLiveJournalCountsOnceEvenWithTwoInTheSameSecond()
    {
        var sale = Sale("2026-10-10T10:05:00Z");
        var walked = Fold(Docked, sale, sale);
        var live = Fold(Docked, sale, sale, Sale("2026-10-10T10:06:00Z"));

        Assert.Equal(3, walked.With(live).Entries.Count);
    }

    [Fact]
    public void ANewCommanderClearsTheRecord()
    {
        var store = new GameStateStore();
        store.Apply(Event("""{"timestamp":"2026-10-10T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}"""));
        store.Apply(Event(Docked));
        store.Apply(Event(Sale()));
        Assert.Single(store.Active!.BgsWork.Entries);

        store.Apply(Event("""{"timestamp":"2026-10-10T11:00:00Z","event":"NewCommander","FID":"F1","Name":"Jameson"}"""));

        Assert.Empty(store.Active!.BgsWork.Entries);
    }

    [Fact]
    public void TheWalkDoesNotOpenAFileOlderThanTheWindow()
    {
        var files = new MemoryFileSystem();
        var old = "/j/Journal.2026-09-01T000000.01.log";
        var recent = "/j/Journal.2026-10-10T000000.01.log";
        var login = """{"timestamp":"2026-10-10T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""";
        files.WriteText(old, login + "\n");
        files.WriteText(recent, string.Join("\n", login, Docked, Sale()) + "\n");

        var spy = new Spy(files, new Dictionary<string, DateTime>
        {
            [old] = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc),
            [recent] = new(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc),
        });

        var found = BgsWorkBackfill.FromHistory(spy, [old, recent], NullLogger.Instance, TestContext.Current.CancellationToken);

        Assert.Equal([recent], spy.Opened);
        Assert.Single(found["F1"].Entries);
    }

    private sealed class Spy(IFileSystem inner, Dictionary<string, DateTime> written) : IFileSystem
    {
        public List<string> Opened { get; } = [];

        public FileState? Stat(string path) => new(written[path], 0);

        public DateTime? FolderWritten(string folder) => inner.FolderWritten(folder);

        public string? ReadText(string path) => inner.ReadText(path);

        public byte[]? ReadBytes(string path) => inner.ReadBytes(path);

        public Stream? OpenRead(string path)
        {
            Opened.Add(path);
            return inner.OpenRead(path);
        }

        public void WriteText(string path, string contents) => inner.WriteText(path, contents);

        public void WriteBytes(string path, byte[] contents) => inner.WriteBytes(path, contents);

        public void AppendText(string path, string contents) => inner.AppendText(path, contents);

        public void CreateFolder(string folder) => inner.CreateFolder(folder);

        public void Delete(string path) => inner.Delete(path);

        public void Copy(string from, string to) => inner.Copy(from, to);

        public IReadOnlyList<string> Enumerate(string folder, string pattern, bool recursive = false) =>
            inner.Enumerate(folder, pattern, recursive);
    }
}
