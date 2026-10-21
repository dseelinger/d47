using D47.Core.Contribution;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Contribution;

public sealed class OutfittingAndShipyardBecomeEddnMessagesTests
{
    private const string Stamp = "2026-10-20T10:00:00Z";

    private static readonly string[] Preamble =
    [
        """{"timestamp":"2026-10-20T09:00:00Z","event":"Fileheader","gameversion":"4.1.0.100","build":"r334473/r0 "}""",
        """{"timestamp":"2026-10-20T09:00:01Z","event":"LoadGame","Horizons":true,"Odyssey":true,"gameversion":"4.1.0.100","build":"r334473/r0 "}""",
        """{"timestamp":"2026-10-20T09:00:02Z","event":"Location","StarSystem":"Sol","SystemAddress":10477373803,"StarPos":[0,0,0]}""",
    ];

    private const string OutfittingLine =
        """{"timestamp":"2026-10-20T10:00:00Z","event":"Outfitting","MarketID":128000001,"StationName":"Test Port","StarSystem":"Sol"}""";

    private const string ShipyardLine =
        """{"timestamp":"2026-10-20T10:00:00Z","event":"Shipyard","MarketID":128000001,"StationName":"Test Port","StarSystem":"Sol"}""";

    private static string OutfittingFile(string stamp = Stamp, long id = 128000001) =>
        $$"""
        {"timestamp":"{{stamp}}","event":"Outfitting","MarketID":{{id}},"StationName":"Test Port","StarSystem":"Sol","Horizons":true,
         "Items":[
          {"id":1,"Name":"int_fighterbaymk2_size5_class1_free","BuyPrice":1000},
          {"id":2,"Name":"hpt_pulselaser_fixed_small","BuyPrice":2000},
          {"id":3,"Name":"empire_eagle_armour_grade1","BuyPrice":0},
          {"id":4,"Name":"int_planetapproachsuite","BuyPrice":0},
          {"id":5,"Name":"paintjob_eagle_blue","BuyPrice":500},
          {"id":6,"Name":"decal_combat_elite","BuyPrice":500},
          {"id":7,"Name":"ShipKit_Eagle_Spoiler","BuyPrice":500},
          {"id":8,"Name":"bobble_pilot","BuyPrice":500},
          {"id":9,"Name":"hpt_cannon_fixed_small","sku":"ELITE_SPECIFIC_V_POWER","BuyPrice":500},
          {"id":10,"Name":"int_hyperdrive_size2_class1","sku":"ELITE_HORIZONS_V_PLANETARY_LANDINGS","BuyPrice":500},
          {"id":11,"Name":"hpt_pulselaser_fixed_small","BuyPrice":2000}
         ]}
        """;

    private static string ShipyardFile(string stamp = Stamp, long id = 128000001) =>
        $$"""
        {"timestamp":"{{stamp}}","event":"Shipyard","MarketID":{{id}},"StationName":"Test Port","StarSystem":"Sol","Horizons":true,"AllowCobraMkIV":true,
         "PriceList":[
          {"id":1,"ShipType":"anacondamkii","ShipType_Localised":"Anaconda","ShipPrice":146000000},
          {"id":2,"ShipType":"sidewinder","ShipType_Localised":"Sidewinder","ShipPrice":32000}
         ]}
        """;

    private static JournalEvent Event(string line)
    {
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));

        return parsed!;
    }

    private static EddnEnvelope? Compose(string? outfitting, string? shipyard, string eventLine)
    {
        var composer = new EddnComposer(() => "anon", "1.2.3", () => null, false, () => outfitting, () => shipyard);

        foreach (var line in Preamble)
        {
            composer.Compose(Event(line));
        }

        return composer.Compose(Event(eventLine));
    }

    [Fact]
    public void AMatchingOutfittingFileBuildsOneOutfittingMessage()
    {
        var envelope = Compose(OutfittingFile(), null, OutfittingLine);

        Assert.NotNull(envelope);
        Assert.Equal("https://eddn.edcd.io/schemas/outfitting/2", envelope.SchemaRef);
        Assert.Equal("Sol", (string?)envelope.Message["systemName"]);
        Assert.Equal(128000001, (long?)envelope.Message["marketId"]);
        Assert.Equal(Stamp, (string?)envelope.Message["timestamp"]);
        Assert.True((bool?)envelope.Message["horizons"]);

        var modules = envelope.Message["modules"]!.AsArray().Select(node => (string?)node).ToList();
        Assert.Equal(
            ["int_fighterbaymk2_size5_class1_free", "hpt_pulselaser_fixed_small", "empire_eagle_armour_grade1", "int_hyperdrive_size2_class1"],
            modules);
    }

    [Fact]
    public void OutfittingLeavesOutWhatOnlySomeCommandersCanBuy()
    {
        var text = Compose(OutfittingFile(), null, OutfittingLine)!.Message.ToJsonString();

        Assert.DoesNotContain("paintjob", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("decal", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("shipkit", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bobble", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PlanetApproachSuite", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hpt_cannon_fixed_small", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ANoPriceOrLocalisedKeyIsSentWithOutfittingOrShipyard()
    {
        var outfitting = Compose(OutfittingFile(), null, OutfittingLine)!.Message.ToJsonString();
        var shipyard = Compose(null, ShipyardFile(), ShipyardLine)!.Message.ToJsonString();

        foreach (var text in new[] { outfitting, shipyard })
        {
            Assert.DoesNotContain("_Localised", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Price", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("BuyPrice", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AMatchingShipyardFileBuildsOneShipyardMessage()
    {
        var envelope = Compose(null, ShipyardFile(), ShipyardLine);

        Assert.NotNull(envelope);
        Assert.Equal("https://eddn.edcd.io/schemas/shipyard/2", envelope.SchemaRef);
        Assert.Equal("Test Port", (string?)envelope.Message["stationName"]);
        Assert.Equal(128000001, (long?)envelope.Message["marketId"]);
        Assert.Equal(["anacondamkii", "sidewinder"], envelope.Message["ships"]!.AsArray().Select(node => (string?)node).ToList());
    }

    [Fact]
    public void AFileForAnotherTimeOrStationBuildsNothing()
    {
        Assert.Null(Compose(OutfittingFile("2026-10-20T09:59:00Z"), null, OutfittingLine));
        Assert.Null(Compose(OutfittingFile(id: 5), null, OutfittingLine));
        Assert.Null(Compose(null, ShipyardFile("2026-10-20T09:59:00Z"), ShipyardLine));
        Assert.Null(Compose(null, ShipyardFile(id: 5), ShipyardLine));
    }

    [Fact]
    public void AMissingFileBuildsNothing()
    {
        Assert.Null(Compose(null, null, OutfittingLine));
        Assert.Null(Compose(null, null, ShipyardLine));
    }
}
