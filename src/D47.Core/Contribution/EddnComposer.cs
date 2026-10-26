using System.Text.Json;
using System.Text.Json.Nodes;
using D47.Core.Journal;

namespace D47.Core.Contribution;

/// <summary>Builds EDDN messages from journal events. Holds the game state the headers need.</summary>
/// <param name="uploaderId">Returns the random number standing for the Commander, or null when there is none; read only when a message is built.</param>
/// <param name="readMarket">Returns the text of <c>Market.json</c>, or null when it cannot be read.</param>
/// <param name="readOutfitting">Returns the text of <c>Outfitting.json</c>, or null when it cannot be read.</param>
/// <param name="readShipyard">Returns the text of <c>Shipyard.json</c>, or null when it cannot be read.</param>
/// <param name="readNavRoute">Returns the text of <c>NavRoute.json</c>, or null when it cannot be read.</param>
/// <param name="statusBodyName">Returns the <c>BodyName</c> in <c>Status.json</c>, or null when it has none.</param>
public sealed class EddnComposer(
    Func<string?> uploaderId,
    string softwareVersion,
    Func<string?> readMarket,
    bool useTestSchemas = false,
    Func<string?>? readOutfitting = null,
    Func<string?>? readShipyard = null,
    Func<string?>? readNavRoute = null,
    Func<string?>? statusBodyName = null)
{
    public const string SoftwareName = "Directive 47";

    private const string CommoditySchema = "https://eddn.edcd.io/schemas/commodity/3";
    private const string OutfittingSchema = "https://eddn.edcd.io/schemas/outfitting/2";
    private const string ShipyardSchema = "https://eddn.edcd.io/schemas/shipyard/2";
    private const string JournalSchema = "https://eddn.edcd.io/schemas/journal/1";
    private const string FssDiscoveryScanSchema = "https://eddn.edcd.io/schemas/fssdiscoveryscan/1";
    private const string FssSignalDiscoveredSchema = "https://eddn.edcd.io/schemas/fsssignaldiscovered/1";
    private const string FssBodySignalsSchema = "https://eddn.edcd.io/schemas/fssbodysignals/1";
    private const string FssAllBodiesFoundSchema = "https://eddn.edcd.io/schemas/fssallbodiesfound/1";
    private const string ScanBaryCentreSchema = "https://eddn.edcd.io/schemas/scanbarycentre/1";
    private const string CodexEntrySchema = "https://eddn.edcd.io/schemas/codexentry/1";
    private const string ApproachSettlementSchema = "https://eddn.edcd.io/schemas/approachsettlement/1";
    private const string NavBeaconScanSchema = "https://eddn.edcd.io/schemas/navbeaconscan/1";
    private const string NavRouteSchema = "https://eddn.edcd.io/schemas/navroute/1";

    /// <summary>How long after its last signal a run of FSSSignalDiscovered events is closed.</summary>
    public static readonly TimeSpan RunIdle = TimeSpan.FromSeconds(10);

    private static readonly string[] SignalKeys =
    [
        "timestamp", "SignalName", "SignalType", "IsStation", "USSType", "SpawningState", "SpawningFaction",
        "SpawningPower", "OpposingPower", "ThreatLevel",
    ];

    private static readonly string[] DiscoveryScanKeys = ["timestamp", "event", "SystemName", "SystemAddress", "BodyCount", "NonBodyCount"];

    private static readonly string[] BodySignalsKeys = ["timestamp", "event", "SystemAddress", "BodyID", "BodyName", "Signals"];

    private static readonly string[] AllBodiesFoundKeys = ["timestamp", "event", "SystemName", "SystemAddress", "Count"];

    private static readonly string[] BaryCentreKeys =
    [
        "timestamp", "event", "SystemAddress", "BodyID", "SemiMajorAxis", "Eccentricity",
        "OrbitalInclination", "Periapsis", "OrbitalPeriod", "AscendingNode", "MeanAnomaly",
    ];

    private static readonly string[] CodexKeys =
    [
        "timestamp", "event", "System", "SystemAddress", "Name", "Region", "EntryID", "Category", "SubCategory",
        "Latitude", "Longitude", "NearestDestination", "VoucherAmount", "Traits",
    ];

    private static readonly string[] SettlementKeys =
    [
        "timestamp", "event", "SystemAddress", "Name", "MarketID", "BodyID", "BodyName", "Latitude", "Longitude",
        "StationGovernment", "StationAllegiance", "StationEconomies", "StationFaction", "StationServices", "StationEconomy",
    ];

    private static readonly string[] SettlementRequired = ["Name", "BodyID", "BodyName", "Latitude", "Longitude"];

    private static readonly string[] BeaconKeys = ["timestamp", "event", "SystemAddress", "NumBodies"];

    private static readonly string[] RouteStopKeys = ["StarSystem", "SystemAddress", "StarPos", "StarClass"];

    private static readonly string[] FactionRemovals = ["MyReputation", "HappiestSystem", "HomeSystem", "SquadronFaction"];

    private static readonly string[] JumpRemovals = ["Wanted", "BoostUsed", "FuelLevel", "FuelUsed", "JumpDist"];

    private static readonly string[] LocationRemovals = ["Wanted", "Latitude", "Longitude"];

    private string _gameVersion = "";
    private string _gameBuild = "";
    private bool? _horizons;
    private bool? _odyssey;
    private bool _inOperation;
    private string? _journalBodyName;
    private long? _journalBodyId;
    private readonly List<JournalEvent> _run = [];
    private DateTimeOffset _runLastSignal;

    public string? StarSystem { get; private set; }

    public long? SystemAddress { get; private set; }

    public StarPosition? StarPos { get; private set; }

    /// <summary>Folds the event into the state and builds nothing.</summary>
    public void Observe(JournalEvent journalEvent)
    {
        ArgumentNullException.ThrowIfNull(journalEvent);

        Fold(journalEvent);
        _run.Clear();
    }

    /// <summary>Folds the event into the state, then returns the message it calls for, or null. Runs of FSSSignalDiscovered are not handled here.</summary>
    public EddnEnvelope? Compose(JournalEvent journalEvent)
    {
        ArgumentNullException.ThrowIfNull(journalEvent);

        Fold(journalEvent);

        return ComposeOwn(journalEvent);
    }

    /// <summary>
    /// Folds the event, then returns the messages it calls for. A FSSSignalDiscovered event joins the open run;
    /// any other event first closes the run, whose message comes before the event's own.
    /// </summary>
    public IReadOnlyList<EddnEnvelope> ComposeAll(JournalEvent journalEvent, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(journalEvent);

        var before = CurrentLocation;
        Fold(journalEvent);

        var messages = new List<EddnEnvelope>();

        if (journalEvent.Kind == "FSSSignalDiscovered")
        {
            if (Eligible)
            {
                _run.Add(journalEvent);
                _runLastSignal = now;
            }

            return messages;
        }

        if (CloseRun(before) is { } run)
        {
            messages.Add(run);
        }

        if (ComposeOwn(journalEvent) is { } own)
        {
            messages.Add(own);
        }

        return messages;
    }

    /// <summary>Closes the open run when its last signal is at least <see cref="RunIdle"/> old, and returns its message.</summary>
    public EddnEnvelope? Flush(DateTimeOffset now) =>
        _run.Count > 0 && now - _runLastSignal >= RunIdle ? CloseRun(CurrentLocation) : null;

    private EddnEnvelope? ComposeOwn(JournalEvent journalEvent)
    {
        if (!Eligible)
        {
            return null;
        }

        return journalEvent.Kind switch
        {
            "Market" => ComposeCommodity(journalEvent),
            "Outfitting" => ComposeOutfitting(journalEvent),
            "Shipyard" => ComposeShipyard(journalEvent),
            "Docked" => ComposeJournal(journalEvent, ["Wanted", "ActiveFine", "CockpitBreach"], false, true),
            "Scan" or "SAASignalsFound" => ComposeJournal(journalEvent, [], false, true),
            "FSDJump" => ComposeJournal(journalEvent, JumpRemovals, true, false),
            "Location" => ComposeJournal(journalEvent, LocationRemovals, true, false),
            "CarrierJump" => ComposeJournal(journalEvent, [.. JumpRemovals, .. LocationRemovals], true, false),
            "FSSDiscoveryScan" => ComposeFss(journalEvent, FssDiscoveryScanSchema, DiscoveryScanKeys, false),
            "FSSBodySignals" => ComposeFss(journalEvent, FssBodySignalsSchema, BodySignalsKeys, true),
            "FSSAllBodiesFound" => ComposeFss(journalEvent, FssAllBodiesFoundSchema, AllBodiesFoundKeys, false),
            "ScanBaryCentre" => ComposeFss(journalEvent, ScanBaryCentreSchema, BaryCentreKeys, true),
            "CodexEntry" => ComposeFss(journalEvent, CodexEntrySchema, CodexKeys, false, ShapeCodex),
            "ApproachSettlement" => ComposeFss(journalEvent, ApproachSettlementSchema, SettlementKeys, true, ShapeSettlement),
            "NavBeaconScan" => ComposeFss(journalEvent, NavBeaconScanSchema, BeaconKeys, true),
            "NavRoute" => ComposeNavRoute(journalEvent),
            _ => null,
        };
    }

    private bool Eligible =>
        !_inOperation
        && _gameVersion.StartsWith("4.", StringComparison.Ordinal)
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
                _inOperation = false;
                break;

            case "GameModeChange":
                _inOperation = journalEvent.String("GameMode") == "Operation";
                break;

            case "ApproachBody":
                _journalBodyName = journalEvent.String("BodyName");
                _journalBodyId = journalEvent.Long("BodyID");
                break;

            case "LeaveBody":
                _journalBodyName = null;
                _journalBodyId = null;
                break;

            case "Location" or "FSDJump" or "CarrierJump":
                if (journalEvent.Kind == "Location")
                {
                    _journalBodyName = journalEvent.String("Body");
                    _journalBodyId = journalEvent.Long("BodyID");
                }
                else
                {
                    _journalBodyName = null;
                    _journalBodyId = null;
                }

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

    private EddnEnvelope? ComposeJournal(JournalEvent journalEvent, string[] removals, bool factions, bool addLocation)
    {
        if (uploaderId() is not { Length: > 0 } uploader
            || JsonNode.Parse(journalEvent.Raw.GetRawText()) is not JsonObject message)
        {
            return null;
        }

        if (addLocation)
        {
            if (!AddFoldedLocation(message, journalEvent))
            {
                return null;
            }
        }
        else if (message["StarSystem"] is null || message["SystemAddress"] is null || message["StarPos"] is null)
        {
            return null;
        }

        foreach (var key in removals)
        {
            message.Remove(key);
        }

        if (factions && message["Factions"] is JsonArray entries)
        {
            foreach (var entry in entries.OfType<JsonObject>())
            {
                foreach (var key in FactionRemovals)
                {
                    entry.Remove(key);
                }
            }
        }

        RemoveLocalised(message);

        if (_horizons is { } horizons)
        {
            message["horizons"] = horizons;
        }

        if (_odyssey is { } odyssey)
        {
            message["odyssey"] = odyssey;
        }

        return new EddnEnvelope(SchemaRef(JournalSchema), Header(uploader), message);
    }

    private (long Address, string Name, StarPosition Position)? CurrentLocation =>
        SystemAddress is { } address && StarSystem is { } name && StarPos is { } position ? (address, name, position) : null;

    /// <param name="shape">Checks and adjusts the picked message before the location is added; false builds nothing.</param>
    private EddnEnvelope? ComposeFss(
        JournalEvent journalEvent, string schema, string[] keys, bool addsStarSystem, Func<JsonObject, bool>? shape = null)
    {
        if (uploaderId() is not { Length: > 0 } uploader
            || CurrentLocation is not { } location
            || journalEvent.Long("SystemAddress") != location.Address
            || Pick(journalEvent.Raw, keys) is not { } message)
        {
            return null;
        }

        if (shape is not null && !shape(message))
        {
            return null;
        }

        if (addsStarSystem)
        {
            message["StarSystem"] = location.Name;
        }

        message["StarPos"] = new JsonArray(location.Position.X, location.Position.Y, location.Position.Z);

        return Finish(schema, uploader, message);
    }

    /// <summary>Adds the body the Commander is on, per the codexentry README: <c>BodyID</c> only when the journal and Status.json name the same body.</summary>
    private bool ShapeCodex(JsonObject message)
    {
        if (message["System"] is null || message["EntryID"] is null)
        {
            return false;
        }

        if (statusBodyName?.Invoke() is { Length: > 0 } body)
        {
            message["BodyName"] = body;

            if (_journalBodyId is { } id && string.Equals(_journalBodyName, body, StringComparison.Ordinal))
            {
                message["BodyID"] = id;
            }
        }

        return true;
    }

    private static bool ShapeSettlement(JsonObject message)
    {
        if (SettlementRequired.Any(key => message[key] is null))
        {
            return false;
        }

        if (message["StationFaction"] is JsonObject faction)
        {
            Keep(faction, "Name", "FactionState");
        }

        if (message["StationEconomies"] is JsonArray economies)
        {
            foreach (var economy in economies.OfType<JsonObject>())
            {
                Keep(economy, "Name", "Proportion");
            }
        }

        return true;
    }

    private static void Keep(JsonObject node, params string[] keys)
    {
        foreach (var key in node.Select(pair => pair.Key).Where(key => !keys.Contains(key)).ToList())
        {
            node.Remove(key);
        }
    }

    /// <summary>Builds navroute/1 from NavRoute.json when it is the file the event names and it holds a route.</summary>
    private EddnEnvelope? ComposeNavRoute(JournalEvent routeEvent)
    {
        if (uploaderId() is not { Length: > 0 } uploader
            || readNavRoute?.Invoke() is not { } text
            || string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        JsonNode? parsed;

        try
        {
            parsed = JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            return null;
        }

        if (parsed is not JsonObject file
            || file["timestamp"] is not JsonValue { } stamp
            || !stamp.TryGetValue<string>(out var timestamp)
            || timestamp != routeEvent.String("timestamp")
            || file["Route"] is not JsonArray { Count: > 0 } stops)
        {
            return null;
        }

        var route = new JsonArray();

        foreach (var stop in stops)
        {
            if (stop is not JsonObject source)
            {
                return null;
            }

            var picked = new JsonObject();

            foreach (var key in RouteStopKeys)
            {
                if (source[key] is not { } value)
                {
                    return null;
                }

                picked[key] = value.DeepClone();
            }

            route.Add(picked);
        }

        var message = new JsonObject
        {
            ["timestamp"] = timestamp,
            ["event"] = "NavRoute",
            ["Route"] = route,
        };

        return Finish(NavRouteSchema, uploader, message);
    }

    /// <summary>Copies the named properties of the event, with every _Localised key removed.</summary>
    private static JsonObject? Pick(JsonElement raw, string[] keys)
    {
        if (JsonNode.Parse(raw.GetRawText()) is not JsonObject source)
        {
            return null;
        }

        var picked = new JsonObject();

        foreach (var key in keys)
        {
            if (source[key] is { } value)
            {
                source.Remove(key);
                picked[key] = value;
            }
        }

        RemoveLocalised(picked);

        return picked;
    }

    private EddnEnvelope Finish(string schema, string uploader, JsonObject message)
    {
        if (_horizons is { } horizons)
        {
            message["horizons"] = horizons;
        }

        if (_odyssey is { } odyssey)
        {
            message["odyssey"] = odyssey;
        }

        return new EddnEnvelope(SchemaRef(schema), Header(uploader), message);
    }

    /// <summary>
    /// Empties the run and builds its message at the location, before or after the closing event, whose address the
    /// first signal carries. Signals from another system, and mission targets, are left out.
    /// </summary>
    private EddnEnvelope? CloseRun((long Address, string Name, StarPosition Position)? before)
    {
        var run = _run.ToList();
        _run.Clear();

        if (run.Count == 0 || uploaderId() is not { Length: > 0 } uploader)
        {
            return null;
        }

        var first = run[0].Long("SystemAddress");

        var location = before is { } earlier && earlier.Address == first ? earlier
            : CurrentLocation is { } later && later.Address == first ? later
            : ((long Address, string Name, StarPosition Position)?)null;

        if (location is not { } here)
        {
            return null;
        }

        var signals = new JsonArray();

        foreach (var signal in run)
        {
            if (signal.Long("SystemAddress") != here.Address
                || signal.String("USSType") == "$USS_Type_MissionTarget;"
                || Pick(signal.Raw, SignalKeys) is not { } entry)
            {
                continue;
            }

            signals.Add(entry);
        }

        if (signals.Count == 0)
        {
            return null;
        }

        var message = new JsonObject
        {
            ["timestamp"] = signals[0]!["timestamp"]?.DeepClone(),
            ["event"] = "FSSSignalDiscovered",
            ["SystemAddress"] = here.Address,
            ["StarSystem"] = here.Name,
            ["StarPos"] = new JsonArray(here.Position.X, here.Position.Y, here.Position.Z),
            ["signals"] = signals,
        };

        return Finish(FssSignalDiscoveredSchema, uploader, message);
    }

    /// <summary>Adds the folded system's name and position when the event is in that system; false when it is not or none is known.</summary>
    private bool AddFoldedLocation(JsonObject message, JournalEvent journalEvent)
    {
        if (SystemAddress is not { } address
            || StarSystem is null
            || StarPos is not { } position
            || journalEvent.Long("SystemAddress") != address)
        {
            return false;
        }

        message["StarSystem"] ??= StarSystem;
        message["StarPos"] ??= new JsonArray(position.X, position.Y, position.Z);

        return true;
    }

    private static void RemoveLocalised(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var key in obj.Select(pair => pair.Key).Where(key => key.EndsWith("_Localised", StringComparison.Ordinal)).ToList())
                {
                    obj.Remove(key);
                }

                foreach (var pair in obj)
                {
                    RemoveLocalised(pair.Value);
                }

                break;

            case JsonArray array:
                foreach (var item in array)
                {
                    RemoveLocalised(item);
                }

                break;
        }
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
