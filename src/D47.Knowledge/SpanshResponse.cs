using System.Text.Json;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Knowledge;

/// <summary>Reads the parts of a Spansh response d47 uses, and nothing else.</summary>
internal static class SpanshResponse
{
    public static GalaxySearchResult ReadSearch(JsonDocument document)
    {
        var root = document.RootElement;

        var systems = new List<SystemSummary>();

        if (root.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
        {
            foreach (var result in results.EnumerateArray())
            {
                systems.Add(ReadSystem(result));
            }
        }

        return new GalaxySearchResult(
            ReadReference(root),
            root.TryGetProperty("count", out var count) && count.TryGetInt32(out var total) ? total : systems.Count,
            systems);
    }

    /// <summary>What the service decided distances were measured from.</summary>
    private static string? ReadReference(JsonElement root) =>
        root.TryGetProperty("reference", out var reference)
        && reference.ValueKind == JsonValueKind.Object
        && reference.TryGetProperty("name", out var name)
        && name.ValueKind == JsonValueKind.String
            ? name.GetString()
            : null;

    public static StationSearchResult ReadStations(JsonDocument document)
    {
        var root = document.RootElement;

        var stations = new List<StationSummary>();

        if (root.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
        {
            foreach (var result in results.EnumerateArray())
            {
                stations.Add(ReadStation(result));
            }
        }

        return new StationSearchResult(
            ReadReference(root),
            root.TryGetProperty("count", out var count) && count.TryGetInt32(out var total) ? total : stations.Count,
            stations);
    }

    /// <summary>Each station of a station search answer with the modules and ships it lists.</summary>
    public static IReadOnlyList<(StationSummary Station, StationStock Stock)> ReadOutfitters(JsonDocument document) =>
    [
        .. document.RootElement.Items("results").Select(result => (ReadStation(result), ReadStock(result))),
    ];

    private static StationStock ReadStock(JsonElement station) =>
        new(
            [.. station.Items("modules")
                .Select(module => String(module, "name") is { } name
                    ? new StockedModule(name, ModuleClass(module), String(module, "rating"), Mount(String(module, "weapon_mode")))
                    : null)
                .OfType<StockedModule>()],
            [.. station.Items("ships")
                .Select(ship => ship.ValueKind == JsonValueKind.String ? ship.GetString() : String(ship, "name"))
                .OfType<string>()]);

    /// <summary>A weapon's <c>weapon_mode</c> in <see cref="ModuleSpecification.Mount"/>'s spelling; null for none.</summary>
    private static string? Mount(string? mode) => mode switch
    {
        "Fixed" => "fixed",
        "Gimbal" => "gimballed",
        "Turret" => "turreted",
        _ => null,
    };

    private static int? ModuleClass(JsonElement module) =>
        !module.TryGetProperty("class", out var size)
            ? null
            : size.ValueKind == JsonValueKind.Number && size.TryGetInt32(out var number)
                ? number
                : size.ValueKind == JsonValueKind.String
                  && int.TryParse(size.GetString(), System.Globalization.CultureInfo.InvariantCulture, out var parsed)
                    ? parsed
                    : null;

    /// <summary>The markets behind d47's own trade planner (Phase 36).</summary>
    public static IReadOnlyList<MarketSnapshot> ReadMarkets(JsonDocument document)
    {
        var markets = new List<MarketSnapshot>();

        foreach (var result in document.RootElement.Items("results"))
        {
            var x = Number(result, "system_x");
            var y = Number(result, "system_y");
            var z = Number(result, "system_z");

            if (x is null || y is null || z is null)
            {
                continue;
            }

            var quotes = new Dictionary<string, MarketQuote>(StringComparer.OrdinalIgnoreCase);

            foreach (var commodity in result.Items("market"))
            {
                if (String(commodity, "commodity") is not { } name)
                {
                    continue;
                }

                quotes[name] = new MarketQuote(name)
                {
                    BuyPrice = (int)(Number(commodity, "buy_price") ?? 0),
                    SellPrice = (int)(Number(commodity, "sell_price") ?? 0),
                    Demand = (int)(Number(commodity, "demand") ?? 0),
                    Supply = (int)(Number(commodity, "supply") ?? 0),
                    IsRare = Boolean(commodity, "is_rare"),
                    Category = String(commodity, "category"),
                };
            }

            if (quotes.Count == 0)
            {
                continue;
            }

            markets.Add(new MarketSnapshot
            {
                Station = String(result, "name") ?? "an unnamed station",
                System = String(result, "system_name") ?? "an unnamed system",
                X = x.Value,
                Y = y.Value,
                Z = z.Value,
                DistanceToArrival = Number(result, "distance_to_arrival"),
                HasLargePad = Boolean(result, "has_large_pad"),
                Type = String(result, "type"),
                ControllingPower = String(result, "system_controlling_power"),
                PowerState = String(result, "system_power_state"),
                UpdatedAt = Timestamp(result, "market_updated_at"),
                Source = PriceSource.Reported,
                Quotes = quotes,
                MarketId = Integer(result, "market_id"),
                SystemAddress = Integer(result, "system_id64"),
                Distance = Number(result, "distance"),
                SmallPads = Count(result, "small_pads"),
                MediumPads = Count(result, "medium_pads"),
                LargePads = Count(result, "large_pads"),
                Services = ReadServiceNames(result),
                ControllingFaction = String(result, "controlling_minor_faction"),
                ControllingFactionState = String(result, "controlling_minor_faction_state"),
                ControllingFactionInfluence = Number(result, "controlling_minor_faction_influence"),
                State = String(result, "state"),
                StationUpdatedAt = Timestamp(result, "updated_at"),
                Allegiance = String(result, "allegiance"),
                Government = String(result, "government"),
                PrimaryEconomy = String(result, "primary_economy"),
                SecondaryEconomy = String(result, "secondary_economy"),
                Economies = [.. result.Items("economies")
                    .Where(economy => economy.ValueKind == JsonValueKind.Object && String(economy, "name") is not null)
                    .Select(economy => new EconomyShare(String(economy, "name")!, Number(economy, "share") ?? 0))],
                HasMarket = OptionalFlag(result, "has_market"),
                HasOutfitting = OptionalFlag(result, "has_outfitting"),
                HasShipyard = OptionalFlag(result, "has_shipyard"),
                IsPlanetary = OptionalFlag(result, "is_planetary"),
                Body = String(result, "body_name"),
                BodyType = String(result, "body_type"),
                BodySubtype = String(result, "body_subtype"),
                BodyAddress = Integer(result, "body_id64"),
                BodyGravity = Number(result, "body_gravity"),
                BodyHasAtmosphere = OptionalFlag(result, "body_has_atmosphere"),
                BodyHasRings = OptionalFlag(result, "body_has_rings"),
                Latitude = Number(result, "latitude"),
                Longitude = Number(result, "longitude"),
                ExportCommodities = CommodityNames(result, "export_commodities"),
                ImportCommodities = CommodityNames(result, "import_commodities"),
                ProhibitedCommodities = CommodityNames(result, "prohibited_commodities"),
                SystemPopulation = Integer(result, "system_population"),
                SystemPowers = [.. result.Items("system_power")
                    .Where(power => power.ValueKind == JsonValueKind.String)
                    .Select(power => power.GetString()!)],
                SystemPrimaryEconomy = String(result, "system_primary_economy"),
                SystemSecondaryEconomy = String(result, "system_secondary_economy"),
                SystemIsColonised = OptionalFlag(result, "system_is_colonised"),
                SystemIsBeingColonised = OptionalFlag(result, "system_is_being_colonised"),
            });
        }

        return markets;
    }

    /// <summary>The system of every station in a station search's results.</summary>
    public static IReadOnlyList<string> ReadStationSystems(JsonDocument document) =>
        [.. document.RootElement.Items("results")
            .Select(result => String(result, "system_name"))
            .OfType<string>()];

    /// <summary>A plotted neutron or long-range route.</summary>
    public static PlottedRoute? ReadRoute(JsonElement result)
    {
        if (result.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var waypoints = new List<RouteWaypoint>();
        var totalJumps = 0;
        RouteEndpoint? start = null;
        var first = true;

        foreach (var jump in result.Items("system_jumps"))
        {
            var jumps = jump.Int("jumps") ?? 0;
            totalJumps += jumps;

            if (jumps == 0)
            {
                if (first && Endpoint(jump, "system") is { } origin)
                {
                    start = origin;
                }

                first = false;
                continue;
            }

            first = false;

            waypoints.Add(new RouteWaypoint(
                String(jump, "system") ?? "an unnamed system",
                jumps,
                Number(jump, "distance_left"),
                Boolean(jump, "neutron_star"))
            {
                DistanceJumped = Number(jump, "distance_jumped"),
                Position = Position(jump),
                SystemAddress = Address(jump),
            });
        }

        return new PlottedRoute(
            String(result, "source_system") ?? "where you are",
            String(result, "destination_system") ?? "the destination",
            Number(result, "distance") ?? 0,
            totalJumps,
            waypoints)
        {
            Start = start,
            ReportedJumps = Count(result, "total_jumps"),
        };
    }

    /// <summary>A Road to Riches route.</summary>
    public static RichesRoute? ReadRiches(JsonElement result)
    {
        if (result.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var stops = new List<RichesStop>();
        var entries = result.EnumerateArray().ToList();
        var kept = new List<JsonElement>();

        foreach (var stop in entries)
        {
            var bodies = new List<RichesBody>();

            foreach (var body in stop.Items("bodies"))
            {
                bodies.Add(new RichesBody(
                    String(body, "name") ?? "an unnamed body",
                    String(body, "subtype"))
                {
                    MappingValue = Integer(body, "estimated_mapping_value"),
                    DistanceToArrival = Number(body, "distance_to_arrival"),
                    Terraformable = Boolean(body, "is_terraformable"),
                    Type = String(body, "type"),
                    BodyAddress = Address(body),
                    ScanValue = Integer(body, "estimated_scan_value"),
                    LandmarkValue = Integer(body, "landmark_value"),
                });
            }

            if (bodies.Count > 0)
            {
                kept.Add(stop);
                stops.Add(new RichesStop(
                    String(stop, "name") ?? "an unnamed system",
                    stop.Int("jumps") ?? 0,
                    bodies)
                {
                    Position = Position(stop),
                    SystemAddress = Address(stop),
                });
            }
        }

        var (start, end) = Ends(entries, kept);

        return new RichesRoute(stops)
        {
            Start = start,
            End = end,
            UnlistedJumps = UnlistedJumps(entries, kept),
        };
    }

    /// <summary>An exobiology plot (Phase 18, "Find the exobiology").</summary>
    public static ExobiologyRoute? ReadExobiology(JsonElement result)
    {
        if (result.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var stops = new List<ExobiologyStop>();
        var entries = result.EnumerateArray().ToList();
        var kept = new List<JsonElement>();

        foreach (var stop in entries)
        {
            var bodies = new List<ExobiologyBody>();

            foreach (var body in stop.Items("bodies"))
            {
                var species = new List<ExobiologySpecies>();

                foreach (var landmark in body.Items("landmarks"))
                {
                    species.Add(new ExobiologySpecies(
                        // `type` is the genus and `subtype` is the species.
                        String(landmark, "type") ?? "an unnamed genus",
                        String(landmark, "subtype") ?? "an unnamed species",
                        landmark.Int("count") ?? 1,
                        Integer(landmark, "value") ?? 0));
                }

                bodies.Add(new ExobiologyBody(
                    String(body, "name") ?? "an unnamed body",
                    String(body, "subtype"))
                {
                    DistanceToArrival = Number(body, "distance_to_arrival"),
                    LandmarkValue = Integer(body, "landmark_value") ?? 0,
                    Species = species,
                    Type = String(body, "type"),
                    BodyAddress = Address(body),
                    ScanValue = Integer(body, "estimated_scan_value"),
                    MappingValue = Integer(body, "estimated_mapping_value"),
                });
            }

            if (bodies.Count > 0)
            {
                kept.Add(stop);
                stops.Add(new ExobiologyStop(
                    String(stop, "name") ?? "an unnamed system",
                    stop.Int("jumps") ?? 0,
                    bodies)
                {
                    Position = Position(stop),
                    SystemAddress = Address(stop),
                });
            }
        }

        var (start, end) = Ends(entries, kept);

        return new ExobiologyRoute(stops)
        {
            Start = start,
            End = end,
            UnlistedJumps = UnlistedJumps(entries, kept),
        };
    }

    /// <summary>A fleet carrier route.</summary>
    public static CarrierRoute? ReadCarrier(JsonElement result)
    {
        if (result.ValueKind != JsonValueKind.Object || !result.TryGetProperty("jumps", out _))
        {
            return null;
        }

        return new CarrierRoute([.. result.Items("jumps").Select(jump => new CarrierWaypoint(
            String(jump, "name") ?? "an unnamed system",
            Number(jump, "distance") ?? 0,
            Number(jump, "distance_to_destination") ?? 0,
            (int)(Integer(jump, "fuel_in_tank") ?? 0),
            (int)(Integer(jump, "fuel_used") ?? 0),
            Flag(jump, "must_restock"),
            (int)(Integer(jump, "restock_amount") ?? 0),
            (int)(Integer(jump, "tritium_in_market") ?? 0),
            Flag(jump, "has_icy_ring"),
            Flag(jump, "is_system_pristine"),
            Flag(jump, "is_desired_destination"))
        {
            Position = Position(jump),
            SystemAddress = Address(jump),
        })])
        {
            Mass = (int?)Integer(result, "mass"),
        };
    }

    private static StarPosition? Position(JsonElement element) =>
        Number(element, "x") is { } x && Number(element, "y") is { } y && Number(element, "z") is { } z
            ? new StarPosition(x, y, z)
            : null;

    private static RouteEndpoint? Endpoint(JsonElement element, string nameProperty) =>
        Position(element) is { } position
            ? new RouteEndpoint(String(element, nameProperty) ?? "an unnamed system", position)
            {
                SystemAddress = Address(element),
            }
            : null;

    /// <summary>An <c>id64</c> that spansh writes as a number or as a string.</summary>
    private static long? Address(JsonElement element)
    {
        if (!element.TryGetProperty("id64", out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out var number) => number,
            JsonValueKind.String when long.TryParse(
                value.GetString(),
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var parsed) => parsed,
            _ => null,
        };
    }

    /// <summary>The journal class of each system's main star in a systems search, by id64.</summary>
    public static Dictionary<long, string> ReadMainStarClasses(JsonDocument document)
    {
        var classes = new Dictionary<long, string>();

        foreach (var system in document.RootElement.Items("results"))
        {
            if (Address(system) is not { } address)
            {
                continue;
            }

            foreach (var body in system.Items("bodies"))
            {
                if (Boolean(body, "is_main_star"))
                {
                    if (StarClasses.FromSubtype(String(body, "subtype")) is { } starClass)
                    {
                        classes[address] = starClass;
                    }

                    break;
                }
            }
        }

        return classes;
    }

    /// <summary>The jumps on the dropped entries other than the first.</summary>
    private static int UnlistedJumps(List<JsonElement> entries, List<JsonElement> kept) =>
        entries.Skip(1).Where(entry => !kept.Contains(entry)).Sum(entry => entry.Int("jumps") ?? 0);

    /// <summary>The first and last entries of a plot, where the reader dropped them and they carry a position.</summary>
    private static (RouteEndpoint? Start, RouteEndpoint? End) Ends(List<JsonElement> entries, List<JsonElement> kept)
    {
        if (entries.Count == 0)
        {
            return (null, null);
        }

        var first = entries[0];
        var last = entries[^1];
        var start = kept.Contains(first) ? null : Endpoint(first, "name");
        var end = entries.Count > 1 && !kept.Contains(last) ? Endpoint(last, "name") : null;

        return (start, end);
    }

    /// <summary>A boolean spansh writes as true, false, 1 or 0.</summary>
    private static bool Flag(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value)
        && (value.ValueKind == JsonValueKind.True
            || (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number) && number != 0));

    /// <summary>The bodies of one <c>api/system</c> record that carry a landmark value above zero.</summary>
    public static SystemBiology ReadSystemBiology(JsonDocument document, long systemAddress)
    {
        var bodies = new List<SurveyedBody>();

        foreach (var body in (document.RootElement.Object("record") ?? default).Items("bodies"))
        {
            var value = Integer(body, "landmark_value") ?? 0;

            if (value <= 0 || Integer(body, "id64") is not { } id64)
            {
                continue;
            }

            var species = new List<ExobiologySpecies>();

            foreach (var landmark in body.Items("landmarks"))
            {
                species.Add(new ExobiologySpecies(
                    String(landmark, "type") ?? "an unnamed genus",
                    String(landmark, "subtype") ?? "an unnamed species",
                    landmark.Int("count") ?? 1,
                    Integer(landmark, "value") ?? 0));
            }

            if (species.Count == 0)
            {
                continue;
            }

            bodies.Add(new SurveyedBody(
                String(body, "name") ?? "an unnamed body",

                // A body's id64 carries its BodyID in the top nine bits.
                (int)((ulong)id64 >> 55),
                value,
                species));
        }

        return new SystemBiology(systemAddress, bodies);
    }

    /// <summary>
    /// Every body of an <c>api/dump</c> record, with the mapping values of the matching <c>api/system</c>
    /// record where there is one.
    /// </summary>
    public static IReadOnlyList<BodySummary> ReadSystemBodies(JsonDocument dump, JsonDocument? system)
    {
        var values = new Dictionary<long, long>();

        if (system is not null)
        {
            foreach (var body in (system.RootElement.Object("record") ?? default).Items("bodies"))
            {
                if (Integer(body, "id64") is { } id && Integer(body, "estimated_mapping_value") is { } value)
                {
                    values[id] = value;
                }
            }
        }

        var record = dump.RootElement.Object("system") ?? default;
        var systemName = String(record, "name") ?? "an unnamed system";
        var systemAddress = Integer(record, "id64");
        var bodies = new List<BodySummary>();

        foreach (var body in record.Items("bodies"))
        {
            var id64 = Integer(body, "id64");

            bodies.Add(new BodySummary
            {
                Name = String(body, "name") ?? "an unnamed body",
                SystemName = systemName,
                BodyId = Int(body, "bodyId"),
                SystemAddress = systemAddress,
                DistanceToArrival = Number(body, "distanceToArrival"),
                Subtype = String(body, "subType"),
                IsLandable = Boolean(body, "isLandable"),
                TerraformingState = String(body, "terraformingState"),
                ReserveLevel = String(body, "reserveLevel"),
                Signals = [.. DumpSignals(body).Select(signal => (SignalKind(signal.Key), signal.Value))],
                Rings = [.. body.Items("rings").Select(ring =>
                    new RingSummary(String(ring, "name") ?? "an unnamed ring", String(ring, "type"))
                    {
                        Hotspots = [.. DumpSignals(ring).Select(signal => (signal.Key, signal.Value))],
                        SignalsSeen = ring.Object("signals") is { } signals ? Timestamp(signals, "updateTime") : null,
                    })],
                MappingValue = id64 is { } key && values.TryGetValue(key, out var mapping) ? mapping : null,
                Volcanism = String(body, "volcanismType"),
                Atmosphere = String(body, "atmosphereType"),
                Gravity = Number(body, "gravity"),
                SurfaceTemperature = Number(body, "surfaceTemperature"),
                IsTidallyLocked = Boolean(body, "rotationalPeriodTidallyLocked"),
            });
        }

        return bodies;
    }

    /// <summary>The name and count pairs under <c>signals.signals</c>.</summary>
    private static IEnumerable<KeyValuePair<string, int>> DumpSignals(JsonElement holder)
    {
        if ((holder.Object("signals") ?? default).Object("signals") is not { } counts)
        {
            yield break;
        }

        foreach (var signal in counts.EnumerateObject())
        {
            if (signal.Value.ValueKind == JsonValueKind.Number && signal.Value.TryGetInt32(out var count))
            {
                yield return new(signal.Name, count);
            }
        }
    }

    /// <summary><c>$SAA_SignalType_Human;</c> as <c>Human</c>.</summary>
    private static string SignalKind(string name) =>
        name.StartsWith("$SAA_SignalType_", StringComparison.Ordinal) ? name["$SAA_SignalType_".Length..].TrimEnd(';') : name;

    /// <summary>Array members, or empty.</summary>
    private static IEnumerable<JsonElement> Items(this JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray()
            : [];

    private static JsonElement? Object(this JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Object
            ? value
            : null;

    private static int? Int(this JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt32(out var number)
            ? number
            : null;

    public static BodySearchResult ReadBodies(JsonDocument document)
    {
        var root = document.RootElement;

        var bodies = new List<BodySummary>();

        if (root.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
        {
            foreach (var result in results.EnumerateArray())
            {
                bodies.Add(ReadBody(result));
            }
        }

        return new BodySearchResult(
            ReadReference(root),
            root.TryGetProperty("count", out var count) && count.TryGetInt32(out var total) ? total : bodies.Count,
            bodies);
    }

    /// <summary>The systems of a mission station search, each with its factions and its stations.</summary>
    public static MissionStationScan ReadMissionStations(JsonDocument document)
    {
        var root = document.RootElement;

        var systems = new List<MissionSystem>();

        if (root.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
        {
            foreach (var result in results.EnumerateArray())
            {
                var system = ReadSystem(result);

                systems.Add(new MissionSystem(system, system.Stations));
            }
        }

        return new MissionStationScan(
            ReadReference(root),
            root.TryGetProperty("count", out var count) && count.TryGetInt32(out var total) ? total : systems.Count,
            systems);
    }

    /// <summary>
    /// One page of a faction search: each system with the faction's presence there and the stations it controls.
    /// A system's <c>bodies</c> are never read.
    /// </summary>
    public static FactionPage ReadFactionPage(JsonDocument document, string faction)
    {
        var root = document.RootElement;
        var systems = new List<FactionPageSystem>();
        var read = 0;

        foreach (var result in root.Items("results"))
        {
            read++;

            if (String(result, "name") is not { } name)
            {
                continue;
            }

            var presences = result.Items("minor_faction_presences")
                .Where(presence => presence.ValueKind == JsonValueKind.Object && String(presence, "name") is not null)
                .ToList();

            if (presences.FirstOrDefault(presence => String(presence, "name") == faction) is not
                { ValueKind: JsonValueKind.Object } own)
            {
                continue;
            }

            var system = new FactionSystem
            {
                Name = name,
                SystemAddress = Integer(result, "id64"),
                Population = Integer(result, "population"),
                Influence = Number(own, "influence"),
                ActiveStates = Strings(own, "active_states"),
                PendingStates = Strings(own, "pending_states"),
                RecoveringStates = Strings(own, "recovering_states"),
                Controls = String(result, "controlling_minor_faction") == faction,
                Others =
                [
                    .. presences
                        .Where(presence => String(presence, "name") != faction)
                        .Select(presence => new FactionNeighbour(String(presence, "name")!, Number(presence, "influence"))
                        {
                            Government = String(presence, "government"),
                            ActiveStates = Strings(presence, "active_states"),
                            PendingStates = Strings(presence, "pending_states"),
                        }),
                ],
                ReportedAt = Timestamp(result, "updated_at"),
                Position = Position(result),
            };

            systems.Add(new FactionPageSystem(
                system,
                String(own, "name")!,
                String(own, "government"),
                String(own, "allegiance"),
                [.. result.Items("stations")
                    .Where(station => station.ValueKind == JsonValueKind.Object
                        && String(station, "name") is not null
                        && String(station, "controlling_minor_faction") == faction)
                    .Select(station => new FactionStation(
                        String(station, "name")!,
                        String(station, "type"),
                        LargestPad(station),
                        Number(station, "distance_to_arrival"),
                        name))]));
        }

        return new FactionPage(
            root.TryGetProperty("count", out var count) && count.TryGetInt32(out var total) ? total : read,
            read,
            systems);
    }

    private static PadSize? LargestPad(JsonElement station) =>
        Count(station, "large_pads") > 0 || Boolean(station, "has_large_pad") ? PadSize.Large
        : Count(station, "medium_pads") > 0 ? PadSize.Medium
        : Count(station, "small_pads") > 0 ? PadSize.Small
        : null;

    private static IReadOnlyList<string> Strings(JsonElement element, string name) =>
        [.. element.Items(name).Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString()!)];

    /// <summary>A systems search writes a station's services as plain names, where a station search writes objects.</summary>
    private static IReadOnlyList<string> ReadServiceNames(JsonElement station) =>
        [.. station.Items("services")
            .Select(service => service.ValueKind switch
            {
                JsonValueKind.String => service.GetString(),
                JsonValueKind.Object => String(service, "name"),
                _ => null,
            })
            .OfType<string>()];

    /// <summary>The colonisation candidate scan (Phase 18, "Find somewhere worth colonising").</summary>
    public static ColonisationScan ReadColonisation(JsonDocument document)
    {
        var root = document.RootElement;

        var systems = new List<ColonisationSystem>();

        if (root.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
        {
            foreach (var result in results.EnumerateArray())
            {
                systems.Add(ReadColonisationSystem(result));
            }
        }

        return new ColonisationScan(
            ReadReference(root),
            root.TryGetProperty("count", out var count) && count.TryGetInt32(out var total) ? total : systems.Count,
            systems);
    }

    private static ColonisationSystem ReadColonisationSystem(JsonElement element)
    {
        var planets = new Dictionary<string, int>(StringComparer.Ordinal);
        var terraformable = 0;
        double? nearest = null;
        double? furthest = null;

        foreach (var body in element.Items("bodies"))
        {
            // Stars are counted in body_count and left out of the shape.
            if (String(body, "type") is not "Planet")
            {
                continue;
            }

            if (String(body, "subtype") is { } subtype)
            {
                planets[subtype] = planets.GetValueOrDefault(subtype) + 1;
            }

            if (String(body, "terraforming_state") is "Terraformable")
            {
                terraformable++;
            }

            if (Number(body, "distance_to_arrival") is { } arrival)
            {
                nearest = nearest is null ? arrival : Math.Min(nearest.Value, arrival);
                furthest = furthest is null ? arrival : Math.Max(furthest.Value, arrival);
            }
        }

        return new ColonisationSystem
        {
            Name = String(element, "name") ?? "an unnamed system",
            Distance = Number(element, "distance"),

            // Absent means zero, and here that is the same reading rather than a convenient one: the index
            // omits the field on systems nobody lives in.
            Population = Integer(element, "population") ?? 0,
            BeingColonised = Boolean(element, "is_being_colonised"),
            Colonised = Boolean(element, "is_colonised"),
            BodyCount = (int)(Integer(element, "body_count") ?? 0),
            Terraformable = terraformable,
            Planets = [.. planets.OrderByDescending(planet => planet.Value)
                .ThenBy(planet => planet.Key, StringComparer.Ordinal)
                .Select(planet => (planet.Key, planet.Value))],
            NearestBody = nearest,
            FurthestBody = furthest,
        };
    }

    private static StarPosition? SystemPosition(JsonElement element) =>
        Number(element, "system_x") is { } x && Number(element, "system_y") is { } y && Number(element, "system_z") is { } z
            ? new StarPosition(x, y, z)
            : null;

    private static BodySummary ReadBody(JsonElement element) => new()
    {
        Name = String(element, "name") ?? "an unnamed body",
        SystemName = String(element, "system_name") ?? "an unnamed system",
        BodyId = Int(element, "body_id"),
        SystemAddress = Integer(element, "system_id64"),
        Distance = Number(element, "distance"),
        Position = SystemPosition(element),
        DistanceToArrival = Number(element, "distance_to_arrival"),
        Subtype = String(element, "subtype"),
        IsLandable = Boolean(element, "is_landable"),
        TerraformingState = String(element, "terraforming_state"),
        ReserveLevel = String(element, "reserve_level"),
        MappingValue = Integer(element, "estimated_mapping_value"),
        Signals = ReadSignals(element),
        Rings = ReadRings(element),
        Materials = ReadMaterials(element),
        Volcanism = String(element, "volcanism_type"),
        Atmosphere = String(element, "atmosphere"),
        Gravity = Number(element, "gravity"),
        SurfaceTemperature = Number(element, "surface_temperature"),
        IsTidallyLocked = Boolean(element, "is_rotational_period_tidally_locked"),
        BodyAddress = Integer(element, "id64"),
        Type = String(element, "type"),
        IsMainStar = Boolean(element, "is_main_star"),
        ParentBodies = [.. element.Items("parents")
            .Where(parent => parent.ValueKind == JsonValueKind.Object && Integer(parent, "id64") is not null)
            .Select(parent => new BodyParent(String(parent, "type") ?? "Null", Integer(parent, "id64")!.Value, String(parent, "subtype")))],
        SpectralClass = String(element, "spectral_class"),
        Luminosity = String(element, "luminosity_class"),
        SolarMasses = Number(element, "solar_masses"),
        SolarRadius = Number(element, "solar_radius"),
        EarthMasses = Number(element, "earth_masses"),
        Radius = Number(element, "radius"),
        Age = Number(element, "age"),
        SurfacePressure = Number(element, "surface_pressure"),
        AtmosphereComposition = Shares(element, "atmosphere_composition"),
        SolidComposition = Shares(element, "solid_composition"),
        Genera = [.. element.Items("genuses")
            .Where(genus => genus.ValueKind == JsonValueKind.Object)
            .Select(genus => String(genus, "name"))
            .OfType<string>()
            .Select(CodexGenus.Name)],
        OrbitalPeriod = Number(element, "orbital_period"),
        SemiMajorAxis = Number(element, "semi_major_axis"),
        OrbitalEccentricity = Number(element, "orbital_eccentricity"),
        OrbitalInclination = Number(element, "orbital_inclination"),
        OrbitalSynchronicity = Number(element, "orbital_synchronicity"),
        ArgOfPeriapsis = Number(element, "arg_of_periapsis"),
        AscendingNode = Number(element, "ascending_node"),
        MeanAnomaly = Number(element, "mean_anomaly"),
        RotationalPeriod = Number(element, "rotational_period"),

        // The search writes the tilt in radians, as the dump does.
        AxialTilt = Number(element, "axis_tilt") * 180 / Math.PI,
        ScanValue = Integer(element, "estimated_scan_value"),
        SignalCount = Count(element, "signal_count"),
        SignalsSeen = Timestamp(element, "signals_updated_at"),
        LandmarkValue = Integer(element, "landmark_value"),
        Landmarks = [.. element.Items("landmarks")
            .Where(landmark => landmark.ValueKind == JsonValueKind.Object)
            .Select(landmark => new BodyLandmark(
                Integer(landmark, "id"),
                String(landmark, "type"),
                String(landmark, "subtype"),
                String(landmark, "variant"),
                Number(landmark, "latitude"),
                Number(landmark, "longitude"),
                Integer(landmark, "value"))
            {
                Count = Count(landmark, "count"),
            })],
        SynthesisRecipes = [.. element.Items("synthesis_recipes")
            .Where(recipe => recipe.ValueKind == JsonValueKind.Object && String(recipe, "name") is not null)
            .Select(recipe => new SynthesisRecipe(String(recipe, "name")!, String(recipe, "level")))],
        Belts = [.. element.Items("belts")
            .Where(belt => belt.ValueKind == JsonValueKind.Object)
            .Select(belt => new BeltProfile(
                String(belt, "name") ?? "an unnamed belt",
                String(belt, "type"),
                Number(belt, "mass"),
                Number(belt, "inner_radius"),
                Number(belt, "outer_radius")))],
        Stations = [.. element.Items("stations")
            .Where(station => station.ValueKind == JsonValueKind.Object)
            .Select(station => ReadBodyStation(station, element))],
        ControllingPower = String(element, "system_controlling_power"),
        PowerState = String(element, "system_power_state"),
        SystemRegion = String(element, "system_region"),
        ReportedAt = Timestamp(element, "updated_at"),
    };

    /// <summary>A station row nested in a bodies-search row; the body and system come from the row around it.</summary>
    private static StationSummary ReadBodyStation(JsonElement station, JsonElement body) => new()
    {
        Name = String(station, "name") ?? "an unnamed station",
        SystemName = String(body, "system_name") ?? "an unnamed system",
        SystemAddress = Integer(body, "system_id64"),
        Body = String(body, "name"),
        BodyAddress = Integer(body, "id64"),
        MarketId = Integer(station, "market_id"),
        DistanceToArrival = Number(station, "distance_to_arrival"),
        Type = String(station, "type"),
        HasLargePad = Boolean(station, "has_large_pad"),
        SmallPads = Count(station, "small_pads"),
        MediumPads = Count(station, "medium_pads"),
        LargePads = Count(station, "large_pads"),
        ControllingFaction = String(station, "controlling_minor_faction"),
        ControllingFactionState = String(station, "controlling_minor_faction_state"),
        HasMarket = OptionalFlag(station, "has_market"),
        HasOutfitting = OptionalFlag(station, "has_outfitting"),
        HasShipyard = OptionalFlag(station, "has_shipyard"),
        Services = [.. station.Items("services").Where(service => service.ValueKind == JsonValueKind.String).Select(service => service.GetString()!)],
    };

    /// <summary>The <c>name</c> and <c>share</c> pairs of a composition list.</summary>
    private static IReadOnlyDictionary<string, double> Shares(JsonElement element, string name)
    {
        var shares = new Dictionary<string, double>();

        foreach (var entry in element.Items(name).Where(entry => entry.ValueKind == JsonValueKind.Object))
        {
            if (String(entry, "name") is { } part)
            {
                shares[part] = Number(entry, "share") ?? 0;
            }
        }

        return shares;
    }

    /// <summary>Surface materials and their share.</summary>
    private static IReadOnlyList<(string Name, double Share)> ReadMaterials(JsonElement element)
    {
        if (!element.TryGetProperty("materials", out var materials) || materials.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var read = new List<(string, double)>();

        foreach (var material in materials.EnumerateArray())
        {
            if (String(material, "name") is { } name)
            {
                read.Add((name, Number(material, "share") ?? 0));
            }
        }

        return read;
    }

    /// <summary>The signal counts, dropped into pairs.</summary>
    private static IReadOnlyList<(string Kind, int Count)> ReadSignals(JsonElement element)
    {
        if (!element.TryGetProperty("signals", out var signals) || signals.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var read = new List<(string, int)>();

        foreach (var signal in signals.EnumerateArray())
        {
            if (String(signal, "name") is { } name)
            {
                read.Add((name, (int)(Integer(signal, "count") ?? 0)));
            }
        }

        return read;
    }

    private static IReadOnlyList<RingSummary> ReadRings(JsonElement element)
    {
        if (!element.TryGetProperty("rings", out var rings) || rings.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var read = new List<RingSummary>();

        foreach (var ring in rings.EnumerateArray())
        {
            read.Add(new RingSummary(String(ring, "name") ?? "an unnamed ring", String(ring, "type"))
            {
                Hotspots = ReadSignals(ring),
                SignalsSeen = Timestamp(ring, "signals_updated_at"),
            });
        }

        return read;
    }

    private static StationSummary ReadStation(JsonElement element) => new()
    {
        Name = String(element, "name") ?? "an unnamed station",
        SystemName = String(element, "system_name") ?? "an unnamed system",
        MarketId = Integer(element, "market_id"),
        SystemAddress = Integer(element, "system_id64"),
        Distance = Number(element, "distance"),
        Position = SystemPosition(element),
        DistanceToArrival = Number(element, "distance_to_arrival"),
        Type = String(element, "type"),
        HasLargePad = Boolean(element, "has_large_pad"),
        SmallPads = Count(element, "small_pads"),
        MediumPads = Count(element, "medium_pads"),
        LargePads = Count(element, "large_pads"),

        // Null where the station has a trader the index cannot classify — one in fifty, and a real state
        // rather than a parse failure.
        TraderType = String(element, "material_trader"),
        TechnologyBroker = StationQuery.BrokerTypeAt(
            ReadServiceNames(element).Contains("Technology Broker"),
            OptionalFlag(element, "system_is_colonised"),
            String(element, "technology_broker")),

        // Which timestamp depends on what was asked for, and both are worth having: a shipyard seen last year
        // and an outfitting bay seen last week are different kinds of answer.
        StockLastSeen = Timestamp(element, "outfitting_updated_at") ?? Timestamp(element, "shipyard_updated_at"),

        Services = ReadServiceNames(element),
        ControllingFaction = String(element, "controlling_minor_faction"),
        State = String(element, "state"),
        ControllingPower = String(element, "system_controlling_power"),
        PowerState = String(element, "system_power_state"),
        UpdatedAt = Timestamp(element, "updated_at"),
        Allegiance = String(element, "allegiance"),
        Government = String(element, "government"),
        PrimaryEconomy = String(element, "primary_economy"),
        SecondaryEconomy = String(element, "secondary_economy"),
        Economies = [.. element.Items("economies")
            .Where(economy => economy.ValueKind == JsonValueKind.Object && String(economy, "name") is not null)
            .Select(economy => new EconomyShare(String(economy, "name")!, Number(economy, "share") ?? 0))],
        ControllingFactionState = String(element, "controlling_minor_faction_state"),
        ControllingFactionInfluence = Number(element, "controlling_minor_faction_influence"),
        HasMarket = OptionalFlag(element, "has_market"),
        HasOutfitting = OptionalFlag(element, "has_outfitting"),
        HasShipyard = OptionalFlag(element, "has_shipyard"),
        Body = String(element, "body_name"),
        BodyType = String(element, "body_type"),
        BodySubtype = String(element, "body_subtype"),
        BodyAddress = Integer(element, "body_id64"),
        BodyGravity = Number(element, "body_gravity"),
        BodyHasAtmosphere = OptionalFlag(element, "body_has_atmosphere"),
        BodyHasRings = OptionalFlag(element, "body_has_rings"),
        IsPlanetary = OptionalFlag(element, "is_planetary"),
        Latitude = Number(element, "latitude"),
        Longitude = Number(element, "longitude"),
        ExportCommodities = CommodityNames(element, "export_commodities"),
        ImportCommodities = CommodityNames(element, "import_commodities"),
        ProhibitedCommodities = CommodityNames(element, "prohibited_commodities"),
        SystemPopulation = Integer(element, "system_population"),
        SystemPowers = [.. element.Items("system_power")
            .Where(power => power.ValueKind == JsonValueKind.String)
            .Select(power => power.GetString()!)],
        SystemPrimaryEconomy = String(element, "system_primary_economy"),
        SystemSecondaryEconomy = String(element, "system_secondary_economy"),
        SystemIsColonised = OptionalFlag(element, "system_is_colonised"),
        SystemIsBeingColonised = OptionalFlag(element, "system_is_being_colonised"),
        Market = element.TryGetProperty("market", out var market) && market.ValueKind == JsonValueKind.Array
            ? new StationMarket(
                [.. market.EnumerateArray()
                    .Where(row => row.ValueKind == JsonValueKind.Object && String(row, "commodity") is not null)
                    .Select(row => new MarketCommodity(
                        String(row, "commodity")!,
                        null,
                        String(row, "category"),
                        Integer(row, "buy_price") ?? 0,
                        Integer(row, "sell_price") ?? 0,
                        Integer(row, "supply") ?? 0,
                        Integer(row, "demand") ?? 0)
                    {
                        IsRare = OptionalFlag(row, "is_rare"),
                    })],
                Timestamp(element, "market_updated_at"))
            : null,
        Outfitting = element.TryGetProperty("modules", out var modules) && modules.ValueKind == JsonValueKind.Array
            ? new StationOutfitting(
                [.. modules.EnumerateArray()
                    .Where(row => row.ValueKind == JsonValueKind.Object && String(row, "name") is not null)
                    .Select(row => new StationModule(
                        String(row, "name")!,
                        String(row, "ed_symbol"),
                        String(row, "category"),
                        ModuleClass(row),
                        String(row, "rating"),
                        String(row, "ship"))
                    {
                        Price = Integer(row, "price"),
                        Mount = Mount(String(row, "weapon_mode")),
                    })],
                Timestamp(element, "outfitting_updated_at"))
            : null,
        Shipyard = element.TryGetProperty("ships", out var ships) && ships.ValueKind == JsonValueKind.Array
            ? new StationShipyard(
                [.. ships.EnumerateArray()
                    .Where(row => row.ValueKind == JsonValueKind.String || (row.ValueKind == JsonValueKind.Object && String(row, "name") is not null))
                    .Select(row => row.ValueKind == JsonValueKind.String
                        ? new StationShip(row.GetString()!, null)
                        : new StationShip(String(row, "name")!, String(row, "symbol"))
                        {
                            Price = Integer(row, "price"),
                        })],
                Timestamp(element, "shipyard_updated_at"))
            : null,
    };

    private static IReadOnlyList<string> CommodityNames(JsonElement element, string name) =>
        [.. element.Items(name).Where(commodity => commodity.ValueKind == JsonValueKind.Object).Select(commodity => String(commodity, "name")).OfType<string>()];

    /// <summary>True or false as sent; null where the member is absent or not a boolean.</summary>
    private static bool? OptionalFlag(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.ValueKind == JsonValueKind.True
            : null;

    private static DateTimeOffset? Timestamp(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
        && DateTimeOffset.TryParse(
            value.GetString(),
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : null;

    private static SystemSummary ReadSystem(JsonElement element)
    {
        var system = new SystemSummary
        {
            Name = String(element, "name") ?? "an unnamed system",
            SystemAddress = Integer(element, "id64"),
            Distance = Number(element, "distance"),
            Position = Number(element, "x") is { } x && Number(element, "y") is { } y && Number(element, "z") is { } z
                ? new StarPosition(x, y, z)
                : null,
            Allegiance = String(element, "allegiance"),
            Government = String(element, "government"),
            PrimaryEconomy = String(element, "primary_economy"),
            SecondaryEconomy = String(element, "secondary_economy"),
            Region = String(element, "region"),
            Security = String(element, "security"),
            Population = Integer(element, "population"),
            NeedsPermit = Boolean(element, "needs_permit"),
            KnownPermit = OptionalFlag(element, "known_permit"),
            IsColonised = OptionalFlag(element, "is_colonised"),
            IsBeingColonised = OptionalFlag(element, "is_being_colonised"),
            BodyCount = Count(element, "body_count"),
            ScanValue = Integer(element, "estimated_scan_value"),
            MappingValue = Integer(element, "estimated_mapping_value"),
            LandmarkValue = Integer(element, "landmark_value"),
            NearestPopulatedDistance = Number(element, "nearest_populated_distance"),
            NearestPopulatedAddress = Integer(element, "nearest_populated_id64"),
            SynthesisRecipes = [.. element.Items("synthesis_recipes")
                .Where(recipe => recipe.ValueKind == JsonValueKind.Object && String(recipe, "name") is not null)
                .Select(recipe => new SynthesisRecipe(String(recipe, "name")!, String(recipe, "level")))],
            ThargoidWarState = String(element, "thargoid_war_state"),
            ThargoidWarSuccessState = String(element, "thargoid_war_success_state"),
            ThargoidWarFailureState = String(element, "thargoid_war_failure_state"),
            StationCount = element.TryGetProperty("stations", out var stations)
                           && stations.ValueKind == JsonValueKind.Array
                ? stations.GetArrayLength()
                : null,
            ControllingFaction = String(element, "controlling_minor_faction"),
            ControllingFactionState = String(element, "controlling_minor_faction_state"),
            ControllingPower = String(element, "controlling_power"),
            PowerState = String(element, "power_state"),
            PowerControlProgress = Number(element, "power_state_control_progress"),
            Reinforcement = Integer(element, "power_state_reinforcement"),
            Undermining = Integer(element, "power_state_undermining"),
            Powers = Strings(element, "power"),
            Acquiring = element.TryGetProperty("power_conflict_progress", out var conflicts)
                        && conflicts.ValueKind == JsonValueKind.Array
                ? [.. conflicts.EnumerateArray()
                    .Where(conflict => conflict.ValueKind == JsonValueKind.Object && String(conflict, "power") is not null)
                    .Select(conflict => new PowerConflict(String(conflict, "power")!, Number(conflict, "progress") ?? 0))]
                : [],
            Factions = element.TryGetProperty("minor_faction_presences", out var presences)
                       && presences.ValueKind == JsonValueKind.Array
                ? [.. presences.EnumerateArray()
                    .Where(presence => presence.ValueKind == JsonValueKind.Object && String(presence, "name") is not null)
                    .Select(presence => new FactionPresence(String(presence, "name")!, Number(presence, "influence"))
                    {
                        State = String(presence, "state"),
                        Government = String(presence, "government"),
                        Allegiance = String(presence, "allegiance"),
                        ActiveStates = Strings(presence, "active_states"),
                        PendingStates = Strings(presence, "pending_states"),
                        RecoveringStates = Strings(presence, "recovering_states"),
                    })]
                : [],
            AtWar = element.TryGetProperty("minor_faction_presences", out var states)
                    && states.ValueKind == JsonValueKind.Array
                    && states.EnumerateArray().Any(presence => HasWar(presence, "active_states") || HasWar(presence, "pending_states")),
            ReportedAt = Timestamp(element, "updated_at"),
        };

        return system with
        {
            Stations = [.. element.Items("stations")
                .Where(station => station.ValueKind == JsonValueKind.Object && String(station, "name") is not null)
                .Select(station => ReadStation(station) with
                {
                    SystemName = system.Name,
                    SystemAddress = system.SystemAddress,
                    Distance = system.Distance,
                    Position = system.Position,
                    ControllingPower = system.ControllingPower,
                    PowerState = system.PowerState,
                })],
            Bodies = [.. element.Items("bodies")
                .Where(body => body.ValueKind == JsonValueKind.Object)
                .Select(body => ReadBody(body) with
                {
                    SystemName = system.Name,
                    SystemAddress = system.SystemAddress,
                    Distance = system.Distance,
                    Position = system.Position,
                    ControllingPower = system.ControllingPower,
                    PowerState = system.PowerState,
                    SystemRegion = system.Region,
                })],
        };
    }

    private static bool HasWar(JsonElement presence, string name) =>
        presence.ValueKind == JsonValueKind.Object
        && presence.TryGetProperty(name, out var states)
        && states.ValueKind == JsonValueKind.Array
        && states.EnumerateArray().Any(state =>
            state.ValueKind == JsonValueKind.String && state.GetString() is "War" or "Civil War");

    /// <summary>Where the search measured from, in galactic coordinates.</summary>
    public static (double X, double Y, double Z)? ReadReferenceCoordinates(JsonDocument document)
    {
        if (!document.RootElement.TryGetProperty("reference", out var reference)
            || reference.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var x = Number(reference, "x");
        var y = Number(reference, "y");
        var z = Number(reference, "z");

        return x is null || y is null || z is null ? null : (x.Value, y.Value, z.Value);
    }

    private static string? String(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static double? Number(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetDouble(out var number)
            ? number
            : null;

    private static long? Integer(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt64(out var number)
            ? number
            : null;

    private static int? Count(JsonElement element, string name) =>
        Integer(element, name) is { } number && number is >= 0 and <= int.MaxValue ? (int)number : null;

    private static bool Boolean(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
}

/// <summary>One page of a faction search.</summary>
/// <param name="Total">How many systems match across every page.</param>
/// <param name="Read">How many results this page held.</param>
internal sealed record FactionPage(int Total, int Read, IReadOnlyList<FactionPageSystem> Systems);

/// <summary>One system of a faction search, with the faction as that system's report describes it.</summary>
internal sealed record FactionPageSystem(
    FactionSystem System,
    string Faction,
    string? Government,
    string? Allegiance,
    IReadOnlyList<FactionStation> Stations);
