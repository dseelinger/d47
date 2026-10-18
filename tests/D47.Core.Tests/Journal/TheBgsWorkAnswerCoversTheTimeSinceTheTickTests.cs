using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class TheBgsWorkAnswerCoversTheTimeSinceTheTickTests
{
    private const string Commander =
        """{"timestamp":"2026-10-10T09:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""";

    private const string Docked =
        """{"timestamp":"2026-10-10T10:00:00Z","event":"Docked","StationName":"Hutton Orbital","MarketID":100,"StarSystem":"Alpha","SystemAddress":1,"StationFaction":{"Name":"Alpha Mining"}}""";

    private const string EarlySale =
        """{"timestamp":"2026-10-10T10:05:00Z","event":"MarketSell","MarketID":100,"Type":"gold","Count":10,"SellPrice":50000,"TotalSale":500000,"AvgPricePaid":40000}""";

    private static readonly DateTimeOffset Tick = DateTimeOffset.Parse("2026-10-10T10:30:00Z");

    private static string Mission(string time, string faction, string trend, string marks) =>
        "{\"timestamp\":\"" + time + "\",\"event\":\"MissionCompleted\",\"FactionEffects\":[{\"Faction\":\"" + faction
        + "\",\"Influence\":[{\"SystemAddress\":2,\"Trend\":\"" + trend + "\",\"Influence\":\"" + marks + "\"}]}]}";

    private static GameStateStore Store(params string[] events)
    {
        var store = new GameStateStore();

        foreach (var line in events)
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store;
    }

    private static async Task<string> Ask(
        GameStateStore store, DateTimeOffset? tick, DateTimeOffset now, string? json = null)
    {
        var capability = JournalCapability.Create(store, now: () => now, bgsTick: () => tick);
        var result = await CapabilityRegistry.Build([capability]).InvokeAsync(
            "get_bgs_work",
            ToolArguments.FromJson(json),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);

        return result.Spoken;
    }

    [Fact]
    public async Task OnlyWorkAfterTheTickIsReported()
    {
        var store = Store(Commander, Docked, EarlySale, Mission("2026-10-10T11:00:00Z", "Beta Union", "UpGood", "++"));

        var said = await Ask(store, Tick, Tick.AddHours(3));

        Assert.StartsWith("Since the BGS tick at 10:30 UTC, 3 hours ago:", said, StringComparison.Ordinal);
        Assert.Contains("Beta Union in a system not in your recent journals: missions +2.", said, StringComparison.Ordinal);
        Assert.DoesNotContain("Alpha Mining", said, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithoutATickTheLastDayIsReportedAndTheSettingIsNamed()
    {
        var now = DateTimeOffset.Parse("2026-10-11T12:00:00Z");
        var store = Store(Commander, Docked, EarlySale, Mission("2026-10-11T09:00:00Z", "Beta Union", "DownBad", "+"));

        var said = await Ask(store, null, now);

        Assert.StartsWith("In the last 24 hours. d47 does not know when the BGS tick was;", said, StringComparison.Ordinal);
        Assert.Contains("the BGS tick time setting in Galaxy lets it ask.", said, StringComparison.Ordinal);
        Assert.Contains("missions -1", said, StringComparison.Ordinal);
        Assert.DoesNotContain("Alpha Mining", said, StringComparison.Ordinal);
    }

    private static string Visit(string time, double influence) =>
        "{\"timestamp\":\"" + time + "\",\"event\":\"FSDJump\",\"StarSystem\":\"Alpha\",\"SystemAddress\":1,\"Factions\":"
        + "[{\"Name\":\"Alpha Mining\",\"Influence\":" + influence.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}]}";

    private const string LateSale =
        """{"timestamp":"2026-10-10T11:00:00Z","event":"MarketSell","MarketID":100,"Type":"gold","Count":10,"SellPrice":50000,"TotalSale":500000,"AvgPricePaid":40000}""";

    [Fact]
    public async Task InfluenceBeforeTheTickAndAtTheLatestVisitIsAddedToTheLine()
    {
        var store = Store(Commander, Docked, Visit("2026-10-10T10:10:00Z", 0.231), LateSale, Visit("2026-10-10T12:02:00Z", 0.24));

        var said = await Ask(store, Tick, Tick.AddHours(3));

        Assert.Contains("influence 23.1% before the tick, 24.0% when you were there at 12:02 UTC.", said, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EqualInfluenceIsSaidAsUnchanged()
    {
        var store = Store(Commander, Docked, Visit("2026-10-10T10:10:00Z", 0.231), LateSale, Visit("2026-10-10T12:02:00Z", 0.231));

        var said = await Ask(store, Tick, Tick.AddHours(3));

        Assert.Contains("influence 23.1%, unchanged when you were there at 12:02 UTC.", said, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithoutATickTimeNoInfluenceIsAdded()
    {
        var store = Store(Commander, Docked, Visit("2026-10-10T10:10:00Z", 0.231), LateSale, Visit("2026-10-10T12:02:00Z", 0.24));

        var said = await Ask(store, null, Tick.AddHours(3));

        Assert.Contains("Alpha Mining in Alpha", said, StringComparison.Ordinal);
        Assert.DoesNotContain("influence", said, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NothingInThePeriodSaysSo()
    {
        var store = Store(Commander, Docked, EarlySale);

        Assert.Equal("No BGS work since the tick at 10:30 UTC.", await Ask(store, Tick, Tick.AddHours(1)));
        Assert.Equal("No BGS work in the last 24 hours.", await Ask(store, null, Tick.AddDays(2)));
    }

    [Fact]
    public async Task ATradeLineGivesCreditsSoldAndProfit()
    {
        var store = Store(Commander, Docked, EarlySale);

        var said = await Ask(store, Tick.AddHours(-1), Tick);

        Assert.Contains(
            "Alpha Mining in Alpha: trade 500,000 credits sold for 100,000 profit.",
            said,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{"faction":"ALPHA mining"}""")]
    [InlineData("""{"system":"alpha"}""")]
    public async Task FactionAndSystemEachNarrowTheAnswerIgnoringCase(string json)
    {
        var store = Store(Commander, Docked, EarlySale, Mission("2026-10-10T11:00:00Z", "Beta Union", "UpGood", "+"));

        var said = await Ask(store, Tick.AddHours(-1), Tick.AddHours(3), json);

        Assert.Contains("Alpha Mining in Alpha", said, StringComparison.Ordinal);
        Assert.DoesNotContain("Beta Union", said, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FourPairsNameThreeAndCountTheFourth()
    {
        var store = Store(
            Commander,
            Docked,
            EarlySale,
            Mission("2026-10-10T11:00:00Z", "Beta Union", "UpGood", "+"),
            Mission("2026-10-10T11:01:00Z", "Gamma Labour", "UpGood", "+"),
            Mission("2026-10-10T11:02:00Z", "Delta Pirates", "UpGood", "+"));
        var tick = Tick.AddHours(-1);

        var said = await Ask(store, tick, Tick.AddHours(3));

        Assert.Equal(1, new[] { "Alpha Mining", "Beta Union", "Gamma Labour", "Delta Pirates" }.Count(
            name => !said.Contains(name, StringComparison.Ordinal)));
        Assert.Contains("and 1 more faction.", said, StringComparison.Ordinal);

        var filtered = await Ask(store, tick, Tick.AddHours(3), """{"system":"Alpha"}""");

        Assert.DoesNotContain("more faction", filtered, StringComparison.Ordinal);
    }

    [Fact]
    public async Task APairWithMoreEntriesComesFirst()
    {
        var store = Store(
            Commander,
            Docked,
            Mission("2026-10-10T11:00:00Z", "Beta Union", "UpGood", "+"),
            Mission("2026-10-10T11:01:00Z", "Gamma Labour", "UpGood", "+"),
            Mission("2026-10-10T11:02:00Z", "Gamma Labour", "UpGood", "+"));

        var said = await Ask(store, Tick, Tick.AddHours(3));

        Assert.True(
            said.IndexOf("Gamma Labour", StringComparison.Ordinal) < said.IndexOf("Beta Union", StringComparison.Ordinal),
            said);
    }
}
