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

    public SpanshStarSystemService(ILogger<SpanshStarSystemService> logger, HttpClient? http = null)
    {
        _logger = logger;
        _ownsClient = http is null;

        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(45) };

        _http.BaseAddress ??= new Uri($"https://{Host}/");

        if (!_http.DefaultRequestHeaders.UserAgent.Any())
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("d47/0.1 (+https://github.com/dseelinger/d47)");
        }
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

        foreach (var body in system.Items("bodies"))
        {
            var bodyName = body.String("name");

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
        };
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
        };
    }

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
