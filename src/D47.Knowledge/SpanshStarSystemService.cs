using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging;

namespace D47.Knowledge;

/// <summary><see cref="IStarSystemService"/> against spansh.co.uk's system dump and name search.</summary>
public sealed class SpanshStarSystemService : IStarSystemService, IDisposable
{
    /// <summary>The same host the searches reach, named here too so grepping finds it.</summary>
    public const string Host = SpanshGalaxyService.Host;

    /// <summary>How many name matches are returned.</summary>
    public const int MatchLimit = 5;

    private static readonly TimeSpan DumpBudget = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan SearchBudget = TimeSpan.FromSeconds(15);

    private readonly HttpClient _http;

    private readonly ILogger<SpanshStarSystemService> _logger;

    private readonly bool _ownsClient;

    private readonly ConcurrentDictionary<string, IReadOnlyList<SystemNameMatch>> _prefixes = new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, IReadOnlyList<string>> _factionWords = new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, IReadOnlyList<StationNameMatch>> _stationNames = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, IReadOnlyList<BodyNameMatch>> _bodyNames = new(StringComparer.Ordinal);

    public SpanshStarSystemService(ILogger<SpanshStarSystemService> logger, HttpClient? http = null)
    {
        _logger = logger;
        _ownsClient = http is null;

        _http = KnowledgeHttp.Client(http, Host, TimeSpan.FromSeconds(45));
    }

    public async Task<StarSystemProfile?> ProfileAsync(long systemAddress, CancellationToken cancellationToken)
    {
        using var document = await SpanshGalaxyService.SendAsync(
            _logger,
            token => _http.GetAsync($"api/dump/{systemAddress}", token),
            "the system lookup",
            cancellationToken,
            missingWhen: HttpStatusCode.NotFound,
            budget: DumpBudget).ConfigureAwait(false);

        return document?.RootElement.Object("system") is { } system ? ReadProfile(system, systemAddress) : null;
    }

    public async Task<IReadOnlyList<SystemNameMatch>> MatchNamesAsync(
        string typed,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(typed))
        {
            return [];
        }

        using var document = await SpanshGalaxyService.SendAsync(
            _logger,
            token => _http.GetAsync($"api/search/systems?q={Uri.EscapeDataString(typed.Trim())}", token),
            "the system name search",
            cancellationToken,
            budget: SearchBudget).ConfigureAwait(false);

        var matches = new List<SystemNameMatch>();

        foreach (var result in document!.RootElement.Items("results"))
        {
            if (result.String("name") is { } name
                && result.Long("id64") is { } id64
                && result.Double("x") is { } x
                && result.Double("y") is { } y
                && result.Double("z") is { } z)
            {
                matches.Add(new SystemNameMatch(name, id64, new StarPosition(x, y, z)));

                if (matches.Count == MatchLimit)
                {
                    break;
                }
            }
        }

        return matches;
    }

    /// <summary>Answers come from <c>field_values/system_names</c> and are kept for the session, keyed by the lowercased text.</summary>
    public async Task<IReadOnlyList<SystemNameMatch>> PrefixNamesAsync(
        string typed,
        CancellationToken cancellationToken)
    {
        var prefix = typed.Trim();

        if (prefix.Length == 0)
        {
            return [];
        }

        var key = prefix.ToLowerInvariant();

        if (_prefixes.TryGetValue(key, out var known))
        {
            return known;
        }

        using var document = await SpanshGalaxyService.SendAsync(
            _logger,
            token => _http.GetAsync($"api/systems/field_values/system_names?q={Uri.EscapeDataString(prefix)}", token),
            "the system name suggestions",
            cancellationToken,
            budget: SearchBudget).ConfigureAwait(false);

        var matches = new List<SystemNameMatch>();

        foreach (var entry in document!.RootElement.Items("min_max"))
        {
            if (entry.String("name") is { } name
                && entry.Long("id64") is { } id64
                && entry.Double("x") is { } x
                && entry.Double("y") is { } y
                && entry.Double("z") is { } z)
            {
                matches.Add(new SystemNameMatch(name, id64, new StarPosition(x, y, z)));
            }
        }

        return _prefixes.GetOrAdd(key, matches);
    }

    /// <summary>Asks <c>field_values/minor_faction_presences</c> for the longest typed word and filters the answer here; answers are kept for the session by that word.</summary>
    public async Task<IReadOnlyList<string>> FactionNamesAsync(
        string typed,
        CancellationToken cancellationToken)
    {
        var word = FactionNameSuggestions.WordToSend(typed);

        if (word.Length == 0)
        {
            return [];
        }

        var key = word.ToLowerInvariant();

        if (!_factionWords.TryGetValue(key, out var names))
        {
            using var document = await SpanshGalaxyService.SendAsync(
                _logger,
                token => _http.GetAsync($"api/systems/field_values/minor_faction_presences?q={Uri.EscapeDataString(word)}", token),
                "the faction name suggestions",
                cancellationToken,
                budget: SearchBudget).ConfigureAwait(false);

            names = _factionWords.GetOrAdd(key, ReadFactionNames(document!.RootElement));
        }

        return FactionNameSuggestions.Filter(typed, names);
    }

    /// <summary>Answers come from <c>stations/field_values/name</c> and are kept for the session, keyed by the lowercased text.</summary>
    public async Task<IReadOnlyList<StationNameMatch>> StationNamesAsync(
        string typed,
        CancellationToken cancellationToken)
    {
        var text = typed.Trim();

        if (text.Length == 0)
        {
            return [];
        }

        var key = text.ToLowerInvariant();

        if (_stationNames.TryGetValue(key, out var known))
        {
            return known;
        }

        using var document = await SpanshGalaxyService.SendAsync(
            _logger,
            token => _http.GetAsync($"api/stations/field_values/name?q={Uri.EscapeDataString(text)}", token),
            "the station name suggestions",
            cancellationToken,
            budget: SearchBudget).ConfigureAwait(false);

        var matches = new List<StationNameMatch>();

        foreach (var entry in document!.RootElement.Items("min_max"))
        {
            if (entry.String("name") is { } name
                && entry.Long("market_id") is { } marketId
                && entry.Long("system_id64") is { } systemAddress
                && entry.Double("system_x") is { } x
                && entry.Double("system_y") is { } y
                && entry.Double("system_z") is { } z)
            {
                matches.Add(new StationNameMatch(name, marketId, systemAddress, new StarPosition(x, y, z)));
            }
        }

        return _stationNames.GetOrAdd(key, matches);
    }

    /// <summary>Answers come from <c>bodies/field_values/name</c> and are kept for the session, keyed by the lowercased text.</summary>
    public async Task<IReadOnlyList<BodyNameMatch>> BodyNamesAsync(
        string typed,
        CancellationToken cancellationToken)
    {
        var text = typed.Trim();

        if (text.Length == 0)
        {
            return [];
        }

        var key = text.ToLowerInvariant();

        if (_bodyNames.TryGetValue(key, out var known))
        {
            return known;
        }

        using var document = await SpanshGalaxyService.SendAsync(
            _logger,
            token => _http.GetAsync($"api/bodies/field_values/name?q={Uri.EscapeDataString(text)}", token),
            "the body name suggestions",
            cancellationToken,
            budget: SearchBudget).ConfigureAwait(false);

        var matches = new List<BodyNameMatch>();

        foreach (var entry in document!.RootElement.Items("min_max"))
        {
            if (entry.String("name") is { } name
                && entry.Long("id64") is { } bodyAddress
                && entry.Long("system_id64") is { } systemAddress
                && entry.Double("system_x") is { } x
                && entry.Double("system_y") is { } y
                && entry.Double("system_z") is { } z)
            {
                matches.Add(new BodyNameMatch(name, bodyAddress, systemAddress, new StarPosition(x, y, z)));
            }
        }

        return _bodyNames.GetOrAdd(key, matches);
    }

    private static IReadOnlyList<string> ReadFactionNames(JsonElement root) =>
        root.Object("values") is { } values
            ? [.. values.Items("name")
                .Where(name => name.ValueKind == JsonValueKind.String)
                .Select(name => name.GetString()!)]
            : [];

    public async Task<PowerplayNeighbourhood> PowerplayNearAsync(
        string system,
        double lightYears,
        CancellationToken cancellationToken)
    {
        using var content = new StringContent(SpanshRequest.PowerplayNear(system, lightYears), Encoding.UTF8, "application/json");

        using var document = await SpanshGalaxyService.SendAsync(
            _logger,
            token => _http.PostAsync("api/systems/search", content, token),
            "the Powerplay search",
            cancellationToken,
            budget: SearchBudget).ConfigureAwait(false);

        var root = document!.RootElement;
        var neighbours = new List<PowerplayNeighbour>();
        var sawItself = false;

        foreach (var result in root.Items("results"))
        {
            if (result.String("name") is not { } name || result.Double("distance") is not { } distance)
            {
                continue;
            }

            if (distance == 0 && string.Equals(name, system, StringComparison.OrdinalIgnoreCase))
            {
                sawItself = true;
                continue;
            }

            neighbours.Add(new PowerplayNeighbour(
                name,
                distance,
                result.String("controlling_power"),
                result.String("power_state"),
                result.Double("power_state_control_progress"),
                [.. result.Items("power")
                    .Where(power => power.ValueKind == JsonValueKind.String)
                    .Select(power => power.GetString()!)]));
        }

        var total = root.Int("count") ?? neighbours.Count;

        return new PowerplayNeighbourhood(Math.Max(0, sawItself ? total - 1 : total), neighbours);
    }

    private static StarSystemProfile ReadProfile(JsonElement system, long systemAddress)
    {
        var name = system.String("name") ?? "an unnamed system";

        var stations = new List<StationProfile>();

        stations.AddRange(system.Items("stations").Select(station => ReadStation(station, body: null)));

        var bodies = new List<BodyProfile>();
        var barycentreElements = new List<JsonElement>();
        var chains = new List<List<(string Kind, int BodyId)>>();

        foreach (var body in system.Items("bodies"))
        {
            var bodyName = body.String("name");

            if (body.String("type") == "Barycentre")
            {
                barycentreElements.Add(body);
            }
            else
            {
                chains.Add(Parents(body));
            }

            stations.AddRange(body.Items("stations").Select(station => ReadStation(station, bodyName)));

            if (body.String("type") is "Star" or "Planet" && ReadBody(body) is { } read)
            {
                bodies.Add(read);
            }
        }

        return new StarSystemProfile
        {
            Name = name,
            SystemAddress = system.Long("id64") ?? systemAddress,
            Position = system.Object("coords") is { } coords
                       && coords.Double("x") is { } x
                       && coords.Double("y") is { } y
                       && coords.Double("z") is { } z
                ? new StarPosition(x, y, z)
                : null,
            Allegiance = system.String("allegiance"),
            Government = system.String("government"),
            PrimaryEconomy = system.String("primaryEconomy"),
            SecondaryEconomy = system.String("secondaryEconomy"),
            Security = system.String("security"),
            Population = system.Long("population"),
            NeedsPermit = PermitSystemTable.Locked(name),
            ControllingFaction = system.Object("controllingFaction")?.String("name"),
            Factions = [.. system.Items("factions")
                .Where(faction => faction.String("name") is not null)
                .Select(faction => new FactionStanding(
                    faction.String("name")!,
                    faction.String("government"),
                    faction.String("allegiance"),
                    faction.Double("influence"),
                    States(faction, "activeStates"),
                    States(faction, "pendingStates")))
                .OrderByDescending(faction => faction.Influence ?? 0)],
            Powerplay = ReadPowerplay(system),
            Stations = stations,
            Bodies = bodies,
            Barycentres = [.. barycentreElements.Select(element => ReadBarycentre(element, chains)).OfType<BarycentreProfile>()],
            ReportedAt = Timestamp(system, "date"),
        };
    }

    private static string[] States(JsonElement faction, string property) =>
        [.. faction.Items(property).Select(state => state.String("state")).OfType<string>()];

    private static PowerplayStanding? ReadPowerplay(JsonElement system)
    {
        var standing = new PowerplayStanding
        {
            ControllingPower = system.String("controllingPower"),
            State = system.String("powerState"),
            ControlProgress = system.Double("powerStateControlProgress"),
            Reinforcement = system.Long("powerStateReinforcement"),
            Undermining = system.Long("powerStateUndermining"),
            Powers = [.. system.Items("powers")
                .Where(power => power.ValueKind == JsonValueKind.String)
                .Select(power => power.GetString()!)],
        };

        return standing is { ControllingPower: null, State: null, ControlProgress: null, Reinforcement: null, Undermining: null, Powers.Count: 0 }
            ? null
            : standing;
    }

    private static StationProfile ReadStation(JsonElement station, string? body)
    {
        var type = station.String("type");

        return new StationProfile
        {
            Name = station.String("name") ?? "an unnamed station",
            Kind = KindOf(type),
            Type = type,
            Body = body,
            DistanceToArrival = station.Double("distanceToArrival"),
            ControllingFaction = station.String("controllingFaction"),
            Government = station.String("government"),
            PrimaryEconomy = station.String("primaryEconomy"),
            LargestPad = LargestPad(station.Object("landingPads")),
            Services = [.. station.Items("services")
                .Where(service => service.ValueKind == JsonValueKind.String)
                .Select(service => service.GetString()!)],
            UpdatedAt = Timestamp(station, "updateTime"),
            MarketId = station.Long("id"),
            Allegiance = station.String("allegiance"),
            ControllingFactionState = station.String("controllingFactionState"),
            Economies = [.. Shares(station.Object("economies"))
                .OrderByDescending(economy => economy.Value)
                .Select(economy => new EconomyShare(economy.Key, economy.Value))],
            Pads = ReadPads(station.Object("landingPads")),
            CarrierDockingAccess = station.String("carrierDockingAccess"),
            Market = station.Object("market") is { } market
                ? new StationMarket(
                    [.. market.Items("commodities")
                        .Where(row => row.String("name") is not null)
                        .Select(row => new MarketCommodity(
                            row.String("name")!,
                            row.String("symbol"),
                            row.String("category"),
                            row.Long("buyPrice") ?? 0,
                            row.Long("sellPrice") ?? 0,
                            row.Long("supply") ?? 0,
                            row.Long("demand") ?? 0))],
                    Timestamp(market, "updateTime"))
                : null,
            Outfitting = station.Object("outfitting") is { } outfitting
                ? new StationOutfitting(
                    [.. outfitting.Items("modules")
                        .Where(row => row.String("name") is not null)
                        .Select(row => new StationModule(
                            row.String("name")!,
                            row.String("symbol"),
                            row.String("category"),
                            row.Int("class"),
                            row.String("rating"),
                            row.String("ship")))],
                    Timestamp(outfitting, "updateTime"))
                : null,
            Shipyard = station.Object("shipyard") is { } shipyard
                ? new StationShipyard(
                    [.. shipyard.Items("ships")
                        .Where(row => row.String("name") is not null)
                        .Select(row => new StationShip(row.String("name")!, row.String("symbol")))],
                    Timestamp(shipyard, "updateTime"))
                : null,
        };
    }

    /// <summary>True or false as reported; null when the property is absent.</summary>
    private static bool? Flag(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.ValueKind == JsonValueKind.True
            : null;

    private static PadCounts? ReadPads(JsonElement? pads) =>
        pads is { } counts && (counts.Int("small") is not null || counts.Int("medium") is not null || counts.Int("large") is not null)
            ? new PadCounts(counts.Int("small") ?? 0, counts.Int("medium") ?? 0, counts.Int("large") ?? 0)
            : null;

    /// <summary>The numeric members of an object by name; empty when it is absent.</summary>
    private static Dictionary<string, double> Shares(JsonElement? holder)
    {
        var shares = new Dictionary<string, double>(StringComparer.Ordinal);

        if (holder is { } members)
        {
            foreach (var member in members.EnumerateObject())
            {
                if (member.Value.ValueKind == JsonValueKind.Number && member.Value.TryGetDouble(out var share))
                {
                    shares[member.Name] = share;
                }
            }
        }

        return shares;
    }

    internal static StationKind KindOf(string? type) => type switch
    {
        "Outpost" => StationKind.Outpost,
        "Asteroid base" => StationKind.Starport,
        "Planetary Port" or "Planetary Outpost" => StationKind.SurfacePort,
        "Settlement" => StationKind.Settlement,
        "Mega ship" => StationKind.Megaship,
        "Drake-Class Carrier" => StationKind.FleetCarrier,
        not null when type.EndsWith(" Starport", StringComparison.Ordinal) => StationKind.Starport,
        _ => StationKind.Other,
    };

    private static PadSize? LargestPad(JsonElement? pads) =>
        pads is not { } counts ? null
        : counts.Int("large") > 0 ? PadSize.Large
        : counts.Int("medium") > 0 ? PadSize.Medium
        : counts.Int("small") > 0 ? PadSize.Small
        : null;

    private static BodyProfile? ReadBody(JsonElement body)
    {
        if (body.Int("bodyId") is not { } bodyId || body.String("name") is not { } name)
        {
            return null;
        }

        return new BodyProfile
        {
            BodyId = bodyId,
            Name = name,
            Type = body.String("type")!,
            SubType = body.String("subType"),
            DistanceToArrival = body.Double("distanceToArrival"),
            ParentId = Parent(body),
            Parents = Parents(body),
            SpectralClass = body.String("spectralClass"),
            Luminosity = body.String("luminosity"),
            IsMainStar = body.Bool("mainStar"),
            SolarMasses = body.Double("solarMasses"),
            SolarRadius = body.Double("solarRadius"),
            EarthMasses = body.Double("earthMasses"),
            Radius = body.Double("radius"),
            Gravity = body.Double("gravity"),
            SurfaceTemperature = body.Double("surfaceTemperature"),
            SurfacePressure = body.Double("surfacePressure"),
            Volcanism = body.String("volcanismType"),
            Atmosphere = body.String("atmosphereType"),
            TerraformingState = body.String("terraformingState"),
            IsLandable = body.Bool("isLandable"),
            ReserveLevel = body.String("reserveLevel"),
            Rings = [.. body.Items("rings")
                .Where(ring => ring.String("name") is not null)
                .Select(ring => new RingProfile(ring.String("name")!, ring.String("type"), Signals(ring)))],
            Signals = Signals(body),
            Materials = Shares(body.Object("materials")),
            AtmosphereComposition = Shares(body.Object("atmosphereComposition")),
            SolidComposition = Shares(body.Object("solidComposition")),
            Genera = [.. (body.Object("signals") is { } reported ? reported.Items("genuses") : [])
                .Where(genus => genus.ValueKind == JsonValueKind.String)
                .Select(genus => CodexGenus.Name(genus.GetString()!))],
            OrbitalPeriod = body.Double("orbitalPeriod"),
            SemiMajorAxis = body.Double("semiMajorAxis"),
            OrbitalEccentricity = body.Double("orbitalEccentricity"),
            OrbitalInclination = body.Double("orbitalInclination"),
            ArgOfPeriapsis = body.Double("argOfPeriapsis"),
            AscendingNode = body.Double("ascendingNode"),
            MeanAnomaly = body.Double("meanAnomaly"),
            MeanAnomalyAt = body.Object("timestamps") is { } timestamps ? Timestamp(timestamps, "meanAnomaly") : null,
            RotationalPeriod = body.Double("rotationalPeriod"),
            TidallyLocked = Flag(body, "rotationalPeriodTidallyLocked"),
            AxialTilt = body.Double("axialTilt") * 180 / Math.PI,
            Age = body.Double("age"),
            AbsoluteMagnitude = body.Double("absoluteMagnitude"),
            ReportedAt = Timestamp(body, "updateTime"),
        };
    }

    private static BarycentreProfile? ReadBarycentre(JsonElement barycentre, List<List<(string Kind, int BodyId)>> chains)
    {
        if (barycentre.Int("bodyId") is not { } bodyId)
        {
            return null;
        }

        var parents = chains
            .Select(chain => chain.FindIndex(entry => entry == ("Null", bodyId)))
            .Select((index, at) => (index, at))
            .Where(found => found.index >= 0)
            .Select(found => chains[found.at].Skip(found.index + 1).ToList())
            .FirstOrDefault() ?? [];

        return new BarycentreProfile
        {
            BodyId = bodyId,
            Parents = parents,
            SemiMajorAxis = barycentre.Double("semiMajorAxis"),
            OrbitalEccentricity = barycentre.Double("orbitalEccentricity"),
            OrbitalInclination = barycentre.Double("orbitalInclination"),
            ArgOfPeriapsis = barycentre.Double("argOfPeriapsis"),
            AscendingNode = barycentre.Double("ascendingNode"),
            OrbitalPeriod = barycentre.Double("orbitalPeriod"),
            MeanAnomaly = barycentre.Double("meanAnomaly"),
            MeanAnomalyAt = barycentre.Object("timestamps") is { } timestamps ? Timestamp(timestamps, "meanAnomaly") : null,
        };
    }

    private static List<(string Kind, int BodyId)> Parents(JsonElement body) =>
    [
        .. body.Items("parents")
            .Where(parent => parent.ValueKind == JsonValueKind.Object)
            .SelectMany(parent => parent.EnumerateObject())
            .Where(entry => entry.Value.ValueKind == JsonValueKind.Number && entry.Value.TryGetInt32(out _))
            .Select(entry => (entry.Name, entry.Value.GetInt32())),
    ];

    /// <summary>The first entry of <c>parents</c> that is a star or planet, skipping barycentres.</summary>
    private static int? Parent(JsonElement body)
    {
        foreach (var parent in body.Items("parents"))
        {
            if (parent.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            foreach (var entry in parent.EnumerateObject())
            {
                if (entry.Name != "Null" && entry.Value.ValueKind == JsonValueKind.Number && entry.Value.TryGetInt32(out var id))
                {
                    return id;
                }
            }
        }

        return null;
    }

    private static Dictionary<string, int> Signals(JsonElement holder)
    {
        var signals = new Dictionary<string, int>(StringComparer.Ordinal);

        if (holder.Object("signals")?.Object("signals") is { } counts)
        {
            foreach (var signal in counts.EnumerateObject())
            {
                if (signal.Value.ValueKind == JsonValueKind.Number && signal.Value.TryGetInt32(out var count))
                {
                    signals[signal.Name] = count;
                }
            }
        }

        return signals;
    }

    private static DateTimeOffset? Timestamp(JsonElement element, string name) =>
        element.String(name) is { } text
        && DateTimeOffset.TryParse(
            text,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : null;

    public void Dispose()
    {
        if (_ownsClient)
        {
            _http.Dispose();
        }
    }
}
