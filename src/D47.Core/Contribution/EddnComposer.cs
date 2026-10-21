using System.Text.Json;
using System.Text.Json.Nodes;
using D47.Core.Journal;

namespace D47.Core.Contribution;

/// <summary>Builds EDDN messages from journal events. Holds the game state the headers need.</summary>
/// <param name="uploaderId">Returns the random number standing for the Commander, or null when there is none; read only when a message is built.</param>
/// <param name="readMarket">Returns the text of <c>Market.json</c>, or null when it cannot be read.</param>
/// <param name="readOutfitting">Returns the text of <c>Outfitting.json</c>, or null when it cannot be read.</param>
/// <param name="readShipyard">Returns the text of <c>Shipyard.json</c>, or null when it cannot be read.</param>
public sealed class EddnComposer(
    Func<string?> uploaderId,
    string softwareVersion,
    Func<string?> readMarket,
    bool useTestSchemas = false,
    Func<string?>? readOutfitting = null,
    Func<string?>? readShipyard = null)
{
    public const string SoftwareName = "Directive 47";

    private const string CommoditySchema = "https://eddn.edcd.io/schemas/commodity/3";
    private const string OutfittingSchema = "https://eddn.edcd.io/schemas/outfitting/2";
    private const string ShipyardSchema = "https://eddn.edcd.io/schemas/shipyard/2";

    private string _gameVersion = "";
    private string _gameBuild = "";
    private bool? _horizons;
    private bool? _odyssey;

    public string? StarSystem { get; private set; }

    public long? SystemAddress { get; private set; }

    public StarPosition? StarPos { get; private set; }

    /// <summary>Folds the event into the state and builds nothing.</summary>
    public void Observe(JournalEvent journalEvent)
    {
        ArgumentNullException.ThrowIfNull(journalEvent);

        Fold(journalEvent);
    }

    /// <summary>Folds the event into the state, then returns the message it calls for, or null.</summary>
    public EddnEnvelope? Compose(JournalEvent journalEvent)
    {
        ArgumentNullException.ThrowIfNull(journalEvent);

        Fold(journalEvent);

        if (!Eligible)
        {
            return null;
        }

        return journalEvent.Kind switch
        {
            "Market" => ComposeCommodity(journalEvent),
            "Outfitting" => ComposeOutfitting(journalEvent),
            "Shipyard" => ComposeShipyard(journalEvent),
            _ => null,
        };
    }

    private bool Eligible =>
        _gameVersion.StartsWith("4.", StringComparison.Ordinal)
        && !_gameVersion.Contains("beta", StringComparison.OrdinalIgnoreCase);

    private void Fold(JournalEvent journalEvent)
    {
        switch (journalEvent.Kind)
        {
            case "Fileheader":
                FoldVersion(journalEvent);
                break;

            case "LoadGame":
                FoldVersion(journalEvent);
                _horizons = Flag(journalEvent.Raw, "Horizons");
                _odyssey = Flag(journalEvent.Raw, "Odyssey");
                break;

            case "Location" or "FSDJump" or "CarrierJump":
                StarSystem = journalEvent.String("StarSystem") ?? StarSystem;
                SystemAddress = journalEvent.Long("SystemAddress") ?? SystemAddress;
                StarPos = StarPosition.Read(journalEvent.Raw) ?? StarPos;
                break;
        }
    }

    private void FoldVersion(JournalEvent journalEvent)
    {
        _gameVersion = journalEvent.String("gameversion") ?? _gameVersion;
        _gameBuild = journalEvent.String("build") ?? _gameBuild;
    }

    private static bool? Flag(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;

    private EddnEnvelope? ComposeCommodity(JournalEvent marketEvent)
    {
        using var station = OpenStationFile(readMarket, marketEvent);

        if (station is null)
        {
            return null;
        }

        var commodities = new JsonArray();

        foreach (var item in station.File.Items("Items"))
        {
            if (Commodity(item) is { } commodity)
            {
                commodities.Add(commodity);
            }
        }

        var message = station.Message();

        if (station.File.String("StationType") is { Length: > 0 } stationType)
        {
            message["stationType"] = stationType;
        }

        if (station.File.String("CarrierDockingAccess") is { Length: > 0 } access)
        {
            message["carrierDockingAccess"] = access;
        }

        AddIdentity(message, station);
        message["commodities"] = commodities;

        return new EddnEnvelope(SchemaRef(CommoditySchema), Header(station.Uploader), message);
    }

    private EddnEnvelope? ComposeOutfitting(JournalEvent outfittingEvent)
    {
        using var station = OpenStationFile(readOutfitting, outfittingEvent);

        if (station is null)
        {
            return null;
        }

        var modules = new JsonArray();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in station.File.Items("Items"))
        {
            if (Module(item) is { } name && seen.Add(name))
            {
                modules.Add(name);
            }
        }

        if (modules.Count == 0)
        {
            return null;
        }

        var message = station.Message();
        AddIdentity(message, station);
        message["modules"] = modules;

        return new EddnEnvelope(SchemaRef(OutfittingSchema), Header(station.Uploader), message);
    }

    private EddnEnvelope? ComposeShipyard(JournalEvent shipyardEvent)
    {
        using var station = OpenStationFile(readShipyard, shipyardEvent);

        if (station is null)
        {
            return null;
        }

        var ships = new JsonArray();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in station.File.Items("PriceList"))
        {
            if (item.String("ShipType") is { Length: > 0 } ship && seen.Add(ship))
            {
                ships.Add(ship);
            }
        }

        if (ships.Count == 0)
        {
            return null;
        }

        var message = station.Message();
        AddIdentity(message, station);
        message["ships"] = ships;

        return new EddnEnvelope(SchemaRef(ShipyardSchema), Header(station.Uploader), message);
    }

    private void AddIdentity(JsonObject message, StationFile station)
    {
        message["marketId"] = station.MarketId;

        if (_horizons is { } horizons)
        {
            message["horizons"] = horizons;
        }

        if (_odyssey is { } odyssey)
        {
            message["odyssey"] = odyssey;
        }

        message["timestamp"] = station.Timestamp;
    }

    /// <summary>The module's name when a station sells it to every Commander, else null.</summary>
    private static string? Module(JsonElement item)
    {
        var name = item.String("Name");

        if (string.IsNullOrEmpty(name)
            || name.Equals("Int_PlanetApproachSuite", StringComparison.OrdinalIgnoreCase)
            || !(name.StartsWith("Hpt_", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Int_", StringComparison.OrdinalIgnoreCase)
                || name.Contains("_Armour_", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        var sku = item.String("sku");

        return string.IsNullOrEmpty(sku) || sku == "ELITE_HORIZONS_V_PLANETARY_LANDINGS" ? name : null;
    }

    /// <summary>Opens a station file and returns it only when it is the one the event names.</summary>
    private StationFile? OpenStationFile(Func<string?>? read, JournalEvent journalEvent)
    {
        if (uploaderId() is not { Length: > 0 } uploader)
        {
            return null;
        }

        var text = read?.Invoke();

        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(text);
        }
        catch (JsonException)
        {
            return null;
        }

        var file = document.RootElement;
        var timestamp = file.String("timestamp");
        var marketId = file.Long("MarketID");
        var system = file.String("StarSystem");
        var station = file.String("StationName");

        if (timestamp is null
            || marketId is null
            || string.IsNullOrEmpty(system)
            || string.IsNullOrEmpty(station)
            || timestamp != journalEvent.String("timestamp")
            || marketId != journalEvent.Long("MarketID"))
        {
            document.Dispose();

            return null;
        }

        return new StationFile(document, uploader, timestamp, marketId.Value, system, station);
    }

    private sealed class StationFile(
        JsonDocument document,
        string uploader,
        string timestamp,
        long marketId,
        string system,
        string station) : IDisposable
    {
        public JsonElement File => document.RootElement;

        public string Uploader => uploader;

        public string Timestamp => timestamp;

        public long MarketId => marketId;

        public JsonObject Message() => new()
        {
            ["systemName"] = system,
            ["stationName"] = station,
        };

        public void Dispose() => document.Dispose();
    }

    private static JsonObject? Commodity(JsonElement item)
    {
        if (item.String("Category") == "NonMarketable"
            || !string.IsNullOrEmpty(item.String("Legality")))
        {
            return null;
        }

        var name = item.String("Name")?.TrimStart('$');

        if (name is not null && name.EndsWith("_name;", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^"_name;".Length];
        }

        var mean = item.Int("MeanPrice");
        var buy = item.Int("BuyPrice");
        var stock = item.Int("Stock");
        var sell = item.Int("SellPrice");
        var demand = item.Int("Demand");
        var stockBracket = item.Int("StockBracket");
        var demandBracket = item.Int("DemandBracket");

        if (string.IsNullOrEmpty(name)
            || mean is null || buy is null || stock is null || sell is null || demand is null
            || stockBracket is null || demandBracket is null)
        {
            return null;
        }

        var commodity = new JsonObject
        {
            ["name"] = name,
            ["meanPrice"] = mean,
            ["buyPrice"] = buy,
            ["stock"] = stock,
            ["stockBracket"] = stockBracket,
            ["sellPrice"] = sell,
            ["demand"] = demand,
            ["demandBracket"] = demandBracket,
        };

        if (item.TryGetProperty("StatusFlags", out var flags) && flags.ValueKind == JsonValueKind.Array)
        {
            var distinct = flags.EnumerateArray()
                .Where(flag => flag.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(flag.GetString()))
                .Select(flag => flag.GetString()!)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (distinct.Count > 0)
            {
                var array = new JsonArray();

                foreach (var flag in distinct)
                {
                    array.Add(flag);
                }

                commodity["statusFlags"] = array;
            }
        }

        return commodity;
    }

    private JsonObject Header(string uploader) => new()
    {
        ["uploaderID"] = uploader,
        ["softwareName"] = SoftwareName,
        ["softwareVersion"] = softwareVersion,
        ["gameversion"] = _gameVersion,
        ["gamebuild"] = _gameBuild,
    };

    private string SchemaRef(string schema) => useTestSchemas ? schema + "/test" : schema;
}
