using System.Text.Json;
using System.Text.Json.Nodes;
using D47.Core.Contribution;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Contribution;

public sealed class AMarketEventBecomesACommodityMessageTests
{
    private const string Stamp = "2026-10-20T10:00:00Z";

    private const string Fileheader =
        """{"timestamp":"2026-10-20T09:00:00Z","event":"Fileheader","gameversion":"4.1.0.100","build":"r334473/r0 "}""";

    private const string LoadGame =
        """{"timestamp":"2026-10-20T09:00:01Z","event":"LoadGame","Horizons":true,"Odyssey":true,"gameversion":"4.1.0.100","build":"r334473/r0 "}""";

    private const string Location =
        """{"timestamp":"2026-10-20T09:00:02Z","event":"Location","StarSystem":"Sol","SystemAddress":10477373803,"StarPos":[0,0,0]}""";

    private const string MarketLine =
        """{"timestamp":"2026-10-20T10:00:00Z","event":"Market","MarketID":128000001,"StationName":"Test Port","StarSystem":"Sol"}""";

    private static readonly string[] Session = [Fileheader, LoadGame, Location, MarketLine];

    private static string MarketFile(string stamp = Stamp, long id = 128000001) =>
        $$"""
        {"timestamp":"{{stamp}}","event":"Market","MarketID":{{id}},"StationName":"Test Port","StationType":"Coriolis","StarSystem":"Sol",
         "Items":[
          {"id":1,"Name":"$gold_name;","Name_Localised":"Gold","Category":"$MARKET_category_metals;","Category_Localised":"Metals",
           "BuyPrice":9000,"SellPrice":8500,"MeanPrice":9400,"StockBracket":2,"DemandBracket":0,"Stock":50,"Demand":0,
           "Consumer":false,"Producer":true,"Rare":false,"StatusFlags":["Producer"]},
          {"id":2,"Name":"$drones_name;","Name_Localised":"Limpet","Category":"NonMarketable","Category_Localised":"Non-marketable",
           "BuyPrice":101,"SellPrice":101,"MeanPrice":101,"StockBracket":0,"DemandBracket":0,"Stock":0,"Demand":0},
          {"id":3,"Name":"$slaves_name;","Category":"$MARKET_category_slavery;","Legality":"Illegal",
           "BuyPrice":0,"SellPrice":4000,"MeanPrice":4000,"StockBracket":0,"DemandBracket":2,"Stock":0,"Demand":10}
         ]}
        """;

    private static JournalEvent Event(string line)
    {
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));

        return parsed!;
    }

    private static EddnEnvelope? Compose(string? file, string[] lines, bool test = false)
    {
        var composer = new EddnComposer("anon", "1.2.3", () => file, test);
        EddnEnvelope? last = null;

        foreach (var line in lines)
        {
            last = composer.Compose(Event(line));
        }

        return last;
    }

    [Fact]
    public void AMatchingMarketFileBuildsOneCommodityMessage()
    {
        var envelope = Compose(MarketFile(), Session);

        Assert.NotNull(envelope);
        Assert.Equal("https://eddn.edcd.io/schemas/commodity/3", envelope.SchemaRef);

        var header = envelope.Header;
        Assert.Equal("anon", (string?)header["uploaderID"]);
        Assert.Equal("Directive 47", (string?)header["softwareName"]);
        Assert.Equal("1.2.3", (string?)header["softwareVersion"]);
        Assert.Equal("4.1.0.100", (string?)header["gameversion"]);

        var message = envelope.Message;
        Assert.Equal("Sol", (string?)message["systemName"]);
        Assert.Equal("Test Port", (string?)message["stationName"]);
        Assert.Equal("Coriolis", (string?)message["stationType"]);
        Assert.Equal(128000001, (long?)message["marketId"]);
        Assert.Equal(Stamp, (string?)message["timestamp"]);
        Assert.True((bool?)message["horizons"]);
        Assert.True((bool?)message["odyssey"]);
        Assert.False(message.ContainsKey("economies"));
        Assert.False(message.ContainsKey("prohibited"));

        var gold = Assert.Single(message["commodities"]!.AsArray())!.AsObject();
        Assert.Equal("gold", (string?)gold["name"]);
        Assert.Equal(9400, (int?)gold["meanPrice"]);
        Assert.Equal(9000, (int?)gold["buyPrice"]);
        Assert.Equal(50, (int?)gold["stock"]);
        Assert.Equal(2, (int?)gold["stockBracket"]);
        Assert.Equal(8500, (int?)gold["sellPrice"]);
        Assert.Equal(0, (int?)gold["demand"]);
        Assert.Equal(0, (int?)gold["demandBracket"]);
        Assert.Equal("Producer", (string?)gold["statusFlags"]![0]);

        foreach (var removed in new[] { "id", "Id", "Category", "Producer", "Rare", "Consumer" })
        {
            Assert.False(gold.ContainsKey(removed));
        }
    }

    [Fact]
    public void ANonMarketableOrLegalityCommodityIsLeftOutAndNoLocalisedKeyRemains()
    {
        var json = Compose(MarketFile(), Session)!.ToJson();

        Assert.DoesNotContain("drones", json);
        Assert.DoesNotContain("slaves", json);
        Assert.DoesNotContain("_Localised", json);
    }

    [Theory]
    [InlineData("2026-10-20T10:00:01Z", 128000001)]
    [InlineData(Stamp, 128000002)]
    public void AMarketFileThatDiffersFromTheEventBuildsNothing(string stamp, long id)
    {
        Assert.Null(Compose(MarketFile(stamp, id), Session));
    }

    [Fact]
    public void AnUnreadableMarketFileBuildsNothing()
    {
        Assert.Null(Compose(null, Session));
        Assert.Null(Compose("{ not json", Session));
    }

    [Fact]
    public void TheGameBuildKeepsItsTrailingSpace()
    {
        Assert.Equal("r334473/r0 ", (string?)Compose(MarketFile(), Session)!.Header["gamebuild"]);
    }

    [Theory]
    [InlineData("3.8.0.407")]
    [InlineData("4.0.0.1 Beta")]
    [InlineData("4.0.0.1-BETA")]
    public void ALegacyOrBetaClientBuildsNothing(string version)
    {
        var fileheader = Fileheader.Replace("4.1.0.100", version);
        var loadGame = LoadGame.Replace("4.1.0.100", version);

        Assert.Null(Compose(MarketFile(), [fileheader, loadGame, Location, MarketLine]));
    }

    [Fact]
    public void WithoutAnyVersionNothingIsBuilt()
    {
        Assert.Null(Compose(MarketFile(), [Location, MarketLine]));
    }

    [Fact]
    public void WithoutALoadGameTheExpansionFlagsAreLeftOut()
    {
        var message = Compose(MarketFile(), [Fileheader, Location, MarketLine])!.Message;

        Assert.False(message.ContainsKey("horizons"));
        Assert.False(message.ContainsKey("odyssey"));
    }

    [Fact]
    public void TheTestFlagAppendsTestToTheSchemaRef()
    {
        var envelope = Compose(MarketFile(), Session, test: true)!;

        Assert.EndsWith("/commodity/3/test", envelope.SchemaRef);
        Assert.EndsWith("/test", JsonNode.Parse(envelope.ToJson())!["$schemaRef"]!.GetValue<string>());
    }

    [Fact]
    public void TheComposerFoldsTheSystemFromLocationsAndJumps()
    {
        var composer = new EddnComposer("anon", "1.2.3", () => null);

        composer.Compose(Event(Location));
        composer.Compose(Event(
            """{"timestamp":"2026-10-20T09:05:00Z","event":"FSDJump","StarSystem":"Alpha Centauri","SystemAddress":5,"StarPos":[3.03125,-0.09375,3.15625]}"""));

        Assert.Equal("Alpha Centauri", composer.StarSystem);
        Assert.Equal(5, composer.SystemAddress);
        Assert.Equal(new StarPosition(3.03125, -0.09375, 3.15625), composer.StarPos);
    }

    [Fact]
    public void TheJsonBodyHoldsTheSchemaRefHeaderAndMessage()
    {
        using var body = JsonDocument.Parse(Compose(MarketFile(), Session)!.ToJson());

        Assert.Equal(
            ["$schemaRef", "header", "message"],
            body.RootElement.EnumerateObject().Select(property => property.Name));
    }
}
