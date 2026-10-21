using System.Text.Json;
using System.Text.Json.Nodes;
using D47.Core.Journal;

namespace D47.Core.Contribution;

/// <summary>Builds EDDN messages from journal events. Holds the game state the headers need.</summary>
/// <param name="uploaderId">Returns the random number standing for the Commander, or null when there is none; read only when a message is built.</param>
/// <param name="readMarket">Returns the text of <c>Market.json</c>, or null when it cannot be read.</param>
public sealed class EddnComposer(
    Func<string?> uploaderId,
    string softwareVersion,
    Func<string?> readMarket,
    bool useTestSchemas = false)
{
    public const string SoftwareName = "Directive 47";

    private const string CommoditySchema = "https://eddn.edcd.io/schemas/commodity/3";

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

        return journalEvent.Kind == "Market" && Eligible ? ComposeCommodity(journalEvent) : null;
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
        if (uploaderId() is not { Length: > 0 } uploader)
        {
            return null;
        }

        var text = readMarket();

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

        using (document)
        {
            var file = document.RootElement;
            var timestamp = file.String("timestamp");
            var marketId = file.Long("MarketID");
            var system = file.String("StarSystem");
            var station = file.String("StationName");

            if (timestamp is null
                || marketId is null
                || string.IsNullOrEmpty(system)
                || string.IsNullOrEmpty(station)
                || timestamp != marketEvent.String("timestamp")
                || marketId != marketEvent.Long("MarketID"))
            {
                return null;
            }

            var commodities = new JsonArray();

            foreach (var item in file.Items("Items"))
            {
                if (Commodity(item) is { } commodity)
                {
                    commodities.Add(commodity);
                }
            }

            var message = new JsonObject
            {
                ["systemName"] = system,
                ["stationName"] = station,
            };

            if (file.String("StationType") is { Length: > 0 } stationType)
            {
                message["stationType"] = stationType;
            }

            if (file.String("CarrierDockingAccess") is { Length: > 0 } access)
            {
                message["carrierDockingAccess"] = access;
            }

            message["marketId"] = marketId;

            if (_horizons is { } horizons)
            {
                message["horizons"] = horizons;
            }

            if (_odyssey is { } odyssey)
            {
                message["odyssey"] = odyssey;
            }

            message["timestamp"] = timestamp;
            message["commodities"] = commodities;

            return new EddnEnvelope(SchemaRef(CommoditySchema), Header(uploader), message);
        }
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
