using System.Text.Json;
using D47.Core.Knowledge;

namespace D47.Knowledge;

/// <summary>
/// Reads the parts of a Spansh response d47 uses, and nothing else.
/// <para>
/// Written against <see cref="JsonDocument"/> rather than deserialised into types, deliberately.
/// A search result carries every body in every system it returns — three systems came back as
/// 44 KB, one system record on its own is 268 KB — and modelling that shape would mean owning a
/// large surface of somebody else's undocumented schema, all of it untrusted (architecture.md
/// §7). Reading six fields by name is smaller, and a field that disappears becomes a null rather
/// than a deserialisation failure that takes the turn down.
/// </para>
/// </summary>
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

    /// <summary>
    /// What the service decided distances were measured from. Echoed back because a Commander who
    /// asked for "near me" and a service that quietly defaulted to Sol have not had the same
    /// question answered.
    /// <para>
    /// Read from <c>reference.name</c> and deliberately <em>not</em> from the sibling
    /// <c>search_reference</c>, which sounds like the same thing and is not: it is a GUID
    /// identifying the search, so a summary built from it would tell the Commander their
    /// distances were measured from <c>4FF6E786-9829-11F1-A270-E7F8D53241C7</c>.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// The markets behind d47's own trade planner (Phase 36).
    /// <para>
    /// Every result of the station search carries its whole <c>market</c> array, which is the fact
    /// the phase rests on: d47 does not need a galaxy dump and does not need anybody else's
    /// planner, because the lookups it already makes carry the prices, the demand and the supply.
    /// </para>
    /// <para>
    /// <b>A station with no coordinates is dropped.</b> Position is what makes a leg measurable,
    /// and a market that cannot be routed to is not a candidate — it is a station that would be
    /// silently treated as being at Sol.
    /// </para>
    /// </summary>
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
                UpdatedAt = Timestamp(result, "market_updated_at"),
                Source = PriceSource.Reported,
                Quotes = quotes,
            });
        }

        return markets;
    }

    /// <summary>
    /// A plotted neutron or long-range route.
    /// <para>
    /// The first waypoint of a plot is the system it started from, carrying zero jumps. It is
    /// dropped: "here are your waypoints — Sol, then…" is a line about where the Commander
    /// already is, and every count downstream would be one too high.
    /// </para>
    /// </summary>
    public static PlottedRoute? ReadRoute(JsonElement result)
    {
        if (result.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var waypoints = new List<RouteWaypoint>();
        var totalJumps = 0;

        foreach (var jump in result.Items("system_jumps"))
        {
            var jumps = jump.Int("jumps") ?? 0;
            totalJumps += jumps;

            if (jumps == 0)
            {
                continue;
            }

            waypoints.Add(new RouteWaypoint(
                String(jump, "system") ?? "an unnamed system",
                jumps,
                Number(jump, "distance_left"),
                Boolean(jump, "neutron_star")));
        }

        return new PlottedRoute(
            String(result, "source_system") ?? "where you are",
            String(result, "destination_system") ?? "the destination",
            Number(result, "distance") ?? 0,
            totalJumps,
            waypoints);
    }

    /// <summary>
    /// A Road to Riches route. Stops with nothing to scan are dropped — the plotter includes the
    /// origin, and a loop includes the return leg, both of which come back with an empty body
    /// list and would read as "stop here and scan nothing".
    /// </summary>
    public static RichesRoute? ReadRiches(JsonElement result)
    {
        if (result.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var stops = new List<RichesStop>();

        foreach (var stop in result.EnumerateArray())
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
                });
            }

            if (bodies.Count > 0)
            {
                stops.Add(new RichesStop(
                    String(stop, "name") ?? "an unnamed system",
                    stop.Int("jumps") ?? 0,
                    bodies));
            }
        }

        return new RichesRoute(stops);
    }

    /// <summary>
    /// An exobiology plot (Phase 18, "Find the exobiology").
    /// <para>
    /// <b>The origin comes back as a stop with no bodies</b> — a live plot from Sol returned four
    /// stops of which the first was Sol itself carrying an empty list. Dropping bodyless stops is
    /// what keeps "three systems worth landing on" from reading as four.
    /// </para>
    /// </summary>
    public static ExobiologyRoute? ReadExobiology(JsonElement result)
    {
        if (result.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var stops = new List<ExobiologyStop>();

        foreach (var stop in result.EnumerateArray())
        {
            var bodies = new List<ExobiologyBody>();

            foreach (var body in stop.Items("bodies"))
            {
                var species = new List<ExobiologySpecies>();

                foreach (var landmark in body.Items("landmarks"))
                {
                    species.Add(new ExobiologySpecies(
                        // `type` is the genus and `subtype` is the species. Named that way round
                        // here because "subtype" says nothing at a call site.
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
                });
            }

            if (bodies.Count > 0)
            {
                stops.Add(new ExobiologyStop(
                    String(stop, "name") ?? "an unnamed system",
                    stop.Int("jumps") ?? 0,
                    bodies));
            }
        }

        return new ExobiologyRoute(stops);
    }

    /// <summary>
    /// Array members, or empty. The journal namespace has its own copy of this idea for the same
    /// reason: an absent list and an empty one mean the same thing at every call site.
    /// </summary>
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

    /// <summary>
    /// The colonisation candidate scan (Phase 18, "Find somewhere worth colonising").
    /// <para>
    /// The counting happens here rather than downstream because the raw material is enormous and
    /// almost entirely unwanted: 51 systems within 15 light years of Sol came back as 1.49 MB,
    /// nearly all of it the embedded bodies. Reducing each system to its shape as it is read means
    /// nothing beyond a dozen numbers per system is ever held, let alone projected into records
    /// carrying fields the source does not have.
    /// </para>
    /// </summary>
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
            // Stars are counted in body_count and left out of the shape. A colony is built on
            // planets and in orbit of them, and listing "1 M (Red dwarf) Star" beside the four
            // worlds that matter is a line that costs a Commander attention and buys nothing.
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

            // Absent means zero, and here that is the same reading rather than a convenient one:
            // the index omits the field on systems nobody lives in.
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

    private static BodySummary ReadBody(JsonElement element) => new()
    {
        Name = String(element, "name") ?? "an unnamed body",
        SystemName = String(element, "system_name") ?? "an unnamed system",
        BodyId = Int(element, "body_id"),
        SystemAddress = Integer(element, "system_id64"),
        Distance = Number(element, "distance"),
        DistanceToArrival = Number(element, "distance_to_arrival"),
        Subtype = String(element, "subtype"),
        IsLandable = Boolean(element, "is_landable"),
        TerraformingState = String(element, "terraforming_state"),
        ReserveLevel = String(element, "reserve_level"),
        MappingValue = Integer(element, "estimated_mapping_value"),
        Signals = ReadSignals(element),
        Rings = ReadRings(element),
        Materials = ReadMaterials(element),
    };

    /// <summary>
    /// Surface materials and their share. Every result carries them whether or not one was asked
    /// for, which is what makes ranking possible at all — the index will not sort on share.
    /// </summary>
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

    /// <summary>
    /// The signal counts, dropped into pairs. Read defensively for the same reason everything
    /// else here is: a signal whose entry has no name is skipped rather than becoming a blank
    /// line in something a Commander is about to hear read aloud.
    /// </summary>
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
        DistanceToArrival = Number(element, "distance_to_arrival"),
        Type = String(element, "type"),
        HasLargePad = Boolean(element, "has_large_pad"),

        // Null where the station has a trader the index cannot classify — one in fifty, and a
        // real state rather than a parse failure.
        TraderType = String(element, "material_trader"),

        // Which timestamp depends on what was asked for, and both are worth having: a shipyard
        // seen last year and an outfitting bay seen last week are different kinds of answer.
        StockLastSeen = Timestamp(element, "outfitting_updated_at") ?? Timestamp(element, "shipyard_updated_at"),
    };

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

    private static SystemSummary ReadSystem(JsonElement element) => new()
    {
        Name = String(element, "name") ?? "an unnamed system",
        SystemAddress = Integer(element, "id64"),
        Distance = Number(element, "distance"),
        Allegiance = String(element, "allegiance"),
        Government = String(element, "government"),
        PrimaryEconomy = String(element, "primary_economy"),
        Security = String(element, "security"),
        Population = Integer(element, "population"),
        NeedsPermit = Boolean(element, "needs_permit"),

        // The station list is read for its length and then dropped. How many there are is worth
        // saying out loud; which ones they are is the next question, and answering it unasked is
        // what makes a result too big to speak.
        StationCount = element.TryGetProperty("stations", out var stations)
                       && stations.ValueKind == JsonValueKind.Array
            ? stations.GetArrayLength()
            : null,
    };

    /// <summary>
    /// Where the search measured from, in galactic coordinates.
    /// <para>
    /// This comes off a <em>search</em> response rather than a system endpoint, because there is
    /// no lookup-by-name endpoint — <c>api/system/name/Colonia</c> is a 404, and the by-id
    /// endpoint needs an id64 the Commander does not have. Naming a system as the search's
    /// reference and reading back the coordinates it resolved to is the one call that turns a
    /// spoken name into a position.
    /// </para>
    /// </summary>
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

    private static bool Boolean(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
}
