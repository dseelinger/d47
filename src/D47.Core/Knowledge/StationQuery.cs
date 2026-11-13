using System.Globalization;
using D47.Core.Journal;

namespace D47.Core.Knowledge;

/// <summary>A landing pad size, smallest first.</summary>
public enum PadSize
{
    Small,
    Medium,
    Large,
}

/// <summary>One station, cut down to what answers "where do I buy this".</summary>
public sealed record StationSummary
{
    public required string Name { get; init; }

    public required string SystemName { get; init; }

    /// <summary>What the journal writes as <c>MarketID</c> on docking.</summary>
    public long? MarketId { get; init; }

    /// <summary>The system's id64, for the same reason.</summary>
    public long? SystemAddress { get; init; }

    /// <summary>Light years from the reference system.</summary>
    public double? Distance { get; init; }

    /// <summary>The system's position; null where the search did not give all three coordinates.</summary>
    public StarPosition? Position { get; init; }

    /// <summary>Light seconds from the system's entry point.</summary>
    public double? DistanceToArrival { get; init; }

    public string? Type { get; init; }

    public bool HasLargePad { get; init; }

    /// <summary>Pads of each size; null where the search does not give the count.</summary>
    public int? SmallPads { get; init; }

    public int? MediumPads { get; init; }

    public int? LargePads { get; init; }

    /// <summary>The station's state in words, marked as reported; null for a state that is not shown.</summary>
    public string? ReportedState => State switch
    {
        "Construction" => "reported under construction",
        "Damaged" or "DamagedHuman" => "reported damaged",
        "UnderAttack" => "reported under attack",
        "UnderRepairs" => "reported under repairs",
        _ => null,
    };

    /// <summary>Whether a ship needing this pad or larger can dock. True where no pad count is known.</summary>
    public bool Admits(PadSize size)
    {
        if (SmallPads is null && MediumPads is null && LargePads is null)
        {
            return true;
        }

        return (size <= PadSize.Small && SmallPads > 0) || (size <= PadSize.Medium && MediumPads > 0) || LargePads > 0;
    }

    /// <summary>The pad sizes the station is known to have, smallest first.</summary>
    public IReadOnlyList<PadSize> KnownPads =>
    [
        .. new[] { (PadSize.Small, SmallPads), (PadSize.Medium, MediumPads), (PadSize.Large, LargePads) }
            .Where(pad => pad.Item2 > 0)
            .Select(pad => pad.Item1),
    ];

    /// <summary>Raw, Manufactured or Encoded where the station has a material trader.</summary>
    public string? TraderType { get; init; }

    /// <summary>Guardian or Human where the station has a technology broker.</summary>
    public string? TechnologyBroker { get; init; }

    /// <summary>When the service last saw this station's stock.</summary>
    public DateTimeOffset? StockLastSeen { get; init; }

    /// <summary>The services the station offers, in the service's spelling.</summary>
    public IReadOnlyList<string> Services { get; init; } = [];

    /// <summary>The station's controlling minor faction.</summary>
    public string? ControllingFaction { get; init; }

    /// <summary>The station's own state, in the service's spelling.</summary>
    public string? State { get; init; }

    /// <summary>When the service last had a report of this station.</summary>
    public DateTimeOffset? UpdatedAt { get; init; }

    /// <summary>The Power controlling the station's system, in the service's spelling.</summary>
    public string? ControllingPower { get; init; }

    /// <summary>The system's Powerplay state, in the service's spelling.</summary>
    public string? PowerState { get; init; }

    /// <summary>A Stronghold Carrier in a Stronghold system.</summary>
    public bool IsStrongholdCarrier => StrongholdCarrier.IsNamedOne(Name, PowerState);

    /// <summary>A Stronghold Carrier in a Stronghold system controlled by any Power but <paramref name="pledgedPower"/>.</summary>
    public bool IsOtherPowersStrongholdCarrier(string? pledgedPower) =>
        StrongholdCarrier.IsOtherPowers(IsStrongholdCarrier, ControllingPower, pledgedPower);

    /// <summary>A finished station that does not move: not a carrier, a megaship or a construction site.</summary>
    public bool IsFixed =>
        StationQuery.IsFixedType(Type)
        && !Name.StartsWith("Planetary Construction Site:", StringComparison.OrdinalIgnoreCase)
        && !Name.StartsWith("Orbital Construction Site:", StringComparison.OrdinalIgnoreCase);
}

/// <summary>A station search as asked, before <see cref="StationQuery"/> validates it.</summary>
public sealed record StationSearch
{
    public string? ReferenceSystem { get; init; }

    public double? MinDistance { get; init; }

    public double? MaxDistance { get; init; }

    public int Size { get; init; }

    /// <summary>Comma-separated names from <see cref="StationQuery.StationTypes"/>.</summary>
    public string? StationTypes { get; init; }

    public string? MinPad { get; init; }

    /// <summary>Light seconds from the system's entry point.</summary>
    public double? MaxStationDistance { get; init; }

    /// <summary>Comma-separated names from <see cref="StationQuery.ServiceNames"/>.</summary>
    public string? Services { get; init; }

    public string? MaterialTrader { get; init; }

    /// <summary>A name from <see cref="SearchSorts.For"/>; null is distance.</summary>
    public string? OrderBy { get; init; }

    /// <summary><c>asc</c> or <c>desc</c>; null is the field's first direction.</summary>
    public string? Order { get; init; }

    public string? TechnologyBroker { get; init; }

    public string? Module { get; init; }

    public string? ModuleClass { get; init; }

    public string? ModuleRating { get; init; }

    public string? ModuleMount { get; init; }

    public string? Ship { get; init; }

    /// <summary>Vocabulary filters by name, validated against <see cref="GalaxySearchKind.Stations"/>.</summary>
    public IReadOnlyDictionary<string, string> Filters { get; init; } = new Dictionary<string, string>();
}

/// <summary>A station type a Commander names, and the kind it maps to.</summary>
public sealed record StationTypeName(string Name, StationKind Kind);

/// <summary>A station service a Commander names, and the service's spelling of it.</summary>
public sealed record StationServiceName(string Name, string Field);

public sealed record StationSearchResult(
    string? Reference,
    int Total,
    IReadOnlyList<StationSummary> Stations)
{
    /// <summary>Stronghold Carriers the Commander cannot dock at, dropped from the answer.</summary>
    public int LeftOut { get; init; }

    /// <summary>Whether the Commander was pledged to a Power when the answer was made.</summary>
    public bool Pledged { get; init; }

    /// <summary>Why nothing was searched, when the search could not be answered.</summary>
    public string? Refusal { get; init; }

    /// <summary>Whether the service has rows past the ones this answer used.</summary>
    public bool More { get; init; }

    /// <summary>Whether <see cref="Total"/> counts only the stations kept, because rows were checked after the service counted them.</summary>
    public bool Uncounted { get; init; }
}

/// <summary>A module to be sold at the station, by catalogue name, with an optional size, rating and mount.</summary>
public sealed record ModuleRequirement(string Name, string? Class = null, string? Rating = null, string? Mount = null)
{
    /// <summary>"a 5A Frame Shift Drive" or "a 2F Multi-Cannon, gimballed".</summary>
    public string Said =>
        $"a {Class}{Rating} {Name}".Replace("  ", " ", StringComparison.Ordinal)
        + (Mount is null ? string.Empty : $", {Mount}");

    /// <summary>Whether a station's stock rows are checked for the mount, which the service cannot filter on.</summary>
    public bool MountChecked => OutfittingItem.IsMountChecked(Name, Size, Rating, Mount);

    private int? Size =>
        int.TryParse(Class, NumberStyles.Integer, CultureInfo.InvariantCulture, out var size) ? size : null;
}

/// <summary>An outfitting search as asked, before <see cref="StationQuery"/> validates it.</summary>
public sealed record OutfittingSearch
{
    public string? ReferenceSystem { get; init; }

    public double? MaxDistance { get; init; }

    public int Size { get; init; }

    /// <summary>Comma-separated <see cref="OutfittingItem"/> display forms.</summary>
    public string? Items { get; init; }

    public string? MinPad { get; init; }

    public double? MaxStationDistance { get; init; }

    public bool SurfaceStations { get; init; } = true;

    public bool IncludeCarriers { get; init; }
}

/// <summary>A validated search for somewhere to buy something.</summary>
public sealed record StationQuery
{
    private StationQuery()
    {
    }

    public string? ReferenceSystem { get; init; }

    /// <summary>Centres the search on a point instead of <see cref="ReferenceSystem"/>.</summary>
    public StarPosition? ReferencePosition { get; init; }

    /// <summary>Minimum light years from the reference; null sends none.</summary>
    public double? MinDistance { get; init; }

    /// <summary>Maximum light years from the reference.</summary>
    public double MaxDistance { get; init; } = 50;

    /// <summary>Searches the whole galaxy; <see cref="MaxDistance"/> is ignored.</summary>
    public bool Unbounded { get; init; }

    /// <summary>Modules the station must sell every one of.</summary>
    public IReadOnlyList<ModuleRequirement> Modules { get; init; } = [];

    /// <summary>The catalogue name of a ship to be sold there, if that is the question.</summary>
    public string? Ship { get; init; }

    /// <summary>Whether to insist on a large landing pad.</summary>
    public bool LargePadOnly { get; init; }

    /// <summary>What kind of material trader, where that is the question — Raw, Manufactured or Encoded.</summary>
    public string? TraderType { get; init; }

    public int Size { get; init; } = 5;

    /// <summary>Which page of <see cref="Size"/> results, from 1.</summary>
    public int Page { get; init; } = 1;

    /// <summary>Oldest report to match, in hours; null sends no bound.</summary>
    public int? MaxReportAgeHours { get; init; }

    /// <summary>Sent as the service's station name filter; null sends none.</summary>
    public string? StationName { get; init; }

    /// <summary>What the results are sorted by; null is nearest first.</summary>
    public SearchSort? Sort { get; init; }

    /// <summary>The kinds of station to include; null sends no type filter.</summary>
    public IReadOnlyList<StationKind>? Kinds { get; init; }

    /// <summary>Searches for Stronghold Carriers by exact name and Stronghold state, with no type filter.</summary>
    public bool StrongholdCarriers { get; init; }

    /// <summary>False leaves out every surface port and settlement type, on top of <see cref="Kinds"/>.</summary>
    public bool SurfaceStations { get; init; } = true;

    /// <summary>Leaves out every type <see cref="IsFixedType"/> rejects, on top of <see cref="Kinds"/>.</summary>
    public bool FixedOnly { get; init; }

    /// <summary>Whether a station type is a finished station that does not move; false for no type.</summary>
    public static bool IsFixedType(string? type) =>
        type is { Length: > 0 } named
        && !MarketSnapshot.IsCarrierType(named)
        && !named.Equals("Mega ship", StringComparison.OrdinalIgnoreCase)
        && !named.Contains("Construction Depot", StringComparison.OrdinalIgnoreCase);

    /// <summary>The smallest pad the station must have: Medium or Large.</summary>
    public PadSize? MinPad { get; init; }

    /// <summary>Furthest from the system's entry point, in light seconds.</summary>
    public double? MaxStationDistance { get; init; }

    /// <summary>Services the station must have every one of, in the service's spelling.</summary>
    public IReadOnlyList<string> Services { get; init; } = [];

    /// <summary>Guardian or Human, where the station must have that technology broker.</summary>
    public string? TechnologyBroker { get; init; }

    /// <summary>Vocabulary filters, each honoured by the station index.</summary>
    public IReadOnlyList<GalaxyCriterion> Criteria { get; init; } = [];

    /// <summary>The station types <c>station_type</c> offers.</summary>
    public static IReadOnlyList<StationTypeName> StationTypes { get; } =
    [
        new("Starport", StationKind.Starport),
        new("Outpost", StationKind.Outpost),
        new("Surface port", StationKind.SurfacePort),
        new("Settlement", StationKind.Settlement),
        new("Fleet carrier", StationKind.FleetCarrier),
        new("Megaship", StationKind.Megaship),
    ];

    /// <summary>The tile name for Stronghold Carriers, which Spansh types as a planetary outpost.</summary>
    public const string StrongholdCarrierType = "Stronghold carrier";

    /// <summary>Every name <c>station_type</c> offers: <see cref="StationTypes"/> then <see cref="StrongholdCarrierType"/>.</summary>
    public static IReadOnlyList<string> StationTypeNames { get; } =
        [.. StationTypes.Select(type => type.Name), StrongholdCarrierType];

    /// <summary>The services <c>services</c> offers; the field is the name in <c>field_values/services</c>.</summary>
    public static IReadOnlyList<StationServiceName> ServiceNames { get; } =
    [
        new("Market", "Market"),
        new("Black Market", "Black Market"),
        new("Shipyard", "Shipyard"),
        new("Outfitting", "Outfitting"),
        new("Refuel", "Refuel"),
        new("Repair", "Repair"),
        new("Rearm", "Restock"),
        new("Interstellar Factors", "Interstellar Factors Contact"),
        new("Material Trader", "Material Trader"),
        new("Technology Broker", "Technology Broker"),
        new("Universal Cartographics", "Universal Cartographics"),
        new("Vista Genomics", "Vista Genomics"),
        new("Pioneer Supplies", "Pioneer Supplies"),
        new("Bartender", "Bartender"),
        new("Apex Interstellar", "Apex Interstellar"),
        new("Frontline Solutions", "Frontline Solutions"),
        new("Search and Rescue", "Search and Rescue"),
        new("Redemption Office", "Redemption Office"),
        new("Crew Lounge", "Crew Lounge"),
        new("Contacts", "Contacts"),
        new("Missions", "Missions"),
        new("Missions Generated", "Missions Generated"),
        new("On Dock Mission", "On Dock Mission"),
        new("Tuning", "Tuning"),
        new("Workshop", "Workshop"),
        new("Refinery Contact", "Refinery Contact"),
        new("Powerplay", "Powerplay"),
        new("Shop", "Shop"),
        new("Livery", "Livery"),
        new("Social Space", "Social Space"),
        new("Squadron Bank", "Squadron Bank"),
        new("System Colonisation", "System Colonisation"),
        new("Construction Services", "Construction Services"),
        new("Fleet Carrier Administration", "Fleet Carrier Administration"),
        new("Fleet Carrier Fuel", "Fleet Carrier Fuel"),
        new("Fleet Carrier Management", "Fleet Carrier Management"),
        new("Fleet Carrier Vendor", "Fleet Carrier Vendor"),
    ];

    /// <summary>The two kinds of technology broker, in the index's own spelling.</summary>
    public static IReadOnlyList<string> BrokerTypes { get; } = ["Guardian", "Human"];

    /// <summary>The pad sizes <c>min_pad</c> offers.</summary>
    public static IReadOnlyList<string> MinPads { get; } = ["Medium", "Large"];

    private static bool TryMinPad(string? minPad, out PadSize? pad, out string failure)
    {
        pad = null;
        failure = string.Empty;

        if (string.IsNullOrWhiteSpace(minPad))
        {
            return true;
        }

        if (MinPads.FirstOrDefault(size => Same(size, minPad.Trim())) is not { } match)
        {
            failure = $"'{minPad}' is not a pad size to insist on. It is Medium or Large.";
            return false;
        }

        pad = Enum.Parse<PadSize>(match);
        return true;
    }

    /// <summary>Builds a station search, or says what was wrong in words the model can act on.</summary>
    public static bool TryParse(
        string? referenceSystem,
        string? module,
        string? moduleClass,
        string? moduleRating,
        string? ship,
        bool largePadOnly,
        double? maxDistance,
        int size,
        out StationQuery query,
        out string failure,
        string? moduleMount = null)
    {
        query = new StationQuery();

        if (!TryMatchStock(module, moduleClass, moduleRating, moduleMount, ship, out var stock, out failure))
        {
            return false;
        }

        if (stock.Modules.Count == 0 && stock.Ship is null)
        {
            failure = "Say what to look for — a module or a ship.";
            return false;
        }

        query = stock with
        {
            ReferenceSystem = string.IsNullOrWhiteSpace(referenceSystem) ? null : referenceSystem.Trim(),
            LargePadOnly = largePadOnly,

            // Bounded on both ends.
            MaxDistance = Math.Max(maxDistance is > 0 ? maxDistance.Value : 50, 1),
            Size = Math.Clamp(size <= 0 ? GalaxyQuery.DefaultSize : size, 1, GalaxyQuery.MaxSize),
        };

        return true;
    }

    /// <summary>Builds an outfitting search for several items at one station, or says what was wrong.</summary>
    public static bool TryParse(OutfittingSearch search, out StationQuery query, out string failure)
    {
        query = new StationQuery();
        failure = string.Empty;

        var modules = new List<ModuleRequirement>();
        string? ship = null;

        foreach (var text in Split(search.Items ?? string.Empty))
        {
            if (!OutfittingItem.TryParse(text, out var item, out failure))
            {
                return false;
            }

            if (item.IsShip)
            {
                if (ship is not null && !Same(ship, item.Name))
                {
                    failure = "One ship per search.";
                    return false;
                }

                if (OutfittingCatalogue.IsUnlisted(item.Name))
                {
                    failure = Unlisted(item.Name);
                    return false;
                }

                ship = item.Name;
            }
            else
            {
                var required = new ModuleRequirement(
                    item.Name, item.Class?.ToString(CultureInfo.InvariantCulture), item.Rating, item.Mount);

                if (!modules.Contains(required))
                {
                    modules.Add(required);
                }
            }
        }

        if (modules.Count == 0 && ship is null)
        {
            failure = "Say what to look for — modules or a ship.";
            return false;
        }

        if (!TryMinPad(search.MinPad, out var pad, out failure))
        {
            return false;
        }

        query = new StationQuery
        {
            ReferenceSystem = string.IsNullOrWhiteSpace(search.ReferenceSystem) ? null : search.ReferenceSystem.Trim(),
            Modules = modules,
            Ship = ship,
            MinPad = pad,
            MaxStationDistance = search.MaxStationDistance is > 0 ? search.MaxStationDistance : null,
            SurfaceStations = search.SurfaceStations,
            Kinds =
            [
                .. Enum.GetValues<StationKind>().Where(kind =>
                    (search.IncludeCarriers || kind != StationKind.FleetCarrier)
                    && (search.SurfaceStations || kind is not (StationKind.SurfacePort or StationKind.Settlement))),
            ],
            MaxDistance = Math.Max(search.MaxDistance is > 0 ? search.MaxDistance.Value : 50, 1),
            Size = Math.Clamp(search.Size <= 0 ? GalaxyQuery.DefaultSize : search.Size, 1, GalaxyQuery.MaxSize),
        };

        return true;
    }

    /// <summary>Builds a search by station type, pad, services and filters, or says what was wrong.</summary>
    public static bool TryParse(StationSearch search, out StationQuery query, out string failure)
    {
        query = new StationQuery();

        if (!TryMatchStock(search.Module, search.ModuleClass, search.ModuleRating, search.ModuleMount, search.Ship, out var stock, out failure))
        {
            return false;
        }

        List<StationKind>? kinds = null;
        var strongholdCarriers = false;

        if (!string.IsNullOrWhiteSpace(search.StationTypes))
        {
            kinds = [];
            var names = Split(search.StationTypes).ToList();

            foreach (var name in names)
            {
                if (Same(StrongholdCarrierType, name))
                {
                    strongholdCarriers = true;
                }
                else if (StationTypes.FirstOrDefault(type => Same(type.Name, name)) is { } type)
                {
                    kinds.Add(type.Kind);
                }
                else
                {
                    failure = $"'{name}' is not a station type I know. It has to be one of: "
                              + $"{string.Join(", ", StationTypeNames)}.";
                    return false;
                }
            }

            if (strongholdCarriers)
            {
                if (names.Count > 1)
                {
                    failure = "Stronghold carrier is searched on its own; untick the other types.";
                    return false;
                }

                kinds = null;
            }
        }

        if (!TryMinPad(search.MinPad, out var pad, out failure))
        {
            return false;
        }

        var services = new List<string>();

        if (!string.IsNullOrWhiteSpace(search.Services))
        {
            foreach (var name in Split(search.Services))
            {
                if (ServiceNames.FirstOrDefault(service => Same(service.Name, name)) is not { } service)
                {
                    failure = $"'{name}' is not a station service I know. It has to be one of: "
                              + $"{string.Join(", ", ServiceNames.Select(known => known.Name))}.";
                    return false;
                }

                if (!services.Contains(service.Field))
                {
                    services.Add(service.Field);
                }
            }
        }

        string? trader = null;

        if (!string.IsNullOrWhiteSpace(search.MaterialTrader))
        {
            trader = TraderTypes.FirstOrDefault(type => Same(type, search.MaterialTrader.Trim()));

            if (trader is null)
            {
                failure = $"'{search.MaterialTrader}' is not a kind of material trader. It is Raw, Manufactured "
                          + "or Encoded.";
                return false;
            }
        }

        string? broker = null;

        if (!string.IsNullOrWhiteSpace(search.TechnologyBroker))
        {
            broker = BrokerTypes.FirstOrDefault(type => Same(type, search.TechnologyBroker.Trim()));

            if (broker is null)
            {
                failure = $"'{search.TechnologyBroker}' is not a kind of technology broker. It is Guardian or Human.";
                return false;
            }
        }

        if (!GalaxyCriteria.TryParse(GalaxySearchKind.Stations, search.Filters, out var criteria, out failure))
        {
            return false;
        }

        if (strongholdCarriers)
        {
            var states = criteria.Where(criterion => criterion.Filter.Name == "power_state").ToList();

            if (states.Any(state => !state.Choices.SequenceEqual(["Stronghold"], StringComparer.OrdinalIgnoreCase)))
            {
                failure = "Stronghold carrier is searched on its own, in Stronghold systems; leave power_state out "
                          + "or set it to Stronghold.";
                return false;
            }

            criteria = [.. criteria.Except(states)];
        }

        if (!SearchSorts.TryParse(GalaxySearchKind.Stations, search.OrderBy, search.Order, out var sort, out failure))
        {
            return false;
        }

        var furthest = search.MaxStationDistance is > 0 ? search.MaxStationDistance : null;
        var minimum = search.MinDistance is > 0 ? search.MinDistance : null;

        if (minimum is not null && search.MaxDistance is > 0 && search.MaxDistance <= minimum)
        {
            failure = "min_distance has to be less than max_distance.";
            return false;
        }

        if (kinds is null && !strongholdCarriers && pad is null && furthest is null && services.Count == 0 && trader is null
            && broker is null && stock.Modules.Count == 0 && stock.Ship is null && criteria.Count == 0)
        {
            failure =
                "That search has nothing but a distance, so it would match every station in range. Narrow it "
                + "with a station type, a pad size, a distance from arrival, a service, a material trader or "
                + "technology broker, a module or ship, or one of: "
                + $"{GalaxyFilters.Describe(GalaxySearchKind.Stations)}.";
            return false;
        }

        query = stock with
        {
            ReferenceSystem = string.IsNullOrWhiteSpace(search.ReferenceSystem) ? null : search.ReferenceSystem.Trim(),

            // Carriers are left out unless asked for, as the market search leaves them out.
            Kinds = strongholdCarriers ? null
                : kinds is null
                ? [.. Enum.GetValues<StationKind>().Where(kind => kind != StationKind.FleetCarrier)]
                : [.. kinds.Distinct()],
            StrongholdCarriers = strongholdCarriers,
            MinPad = pad,
            MaxStationDistance = furthest,
            Services = services,
            TraderType = trader,
            TechnologyBroker = broker,
            Criteria = criteria,
            Sort = sort,
            MinDistance = minimum,
            Unbounded = minimum is not null && search.MaxDistance is not > 0,
            MaxDistance = Math.Max(search.MaxDistance is > 0 ? search.MaxDistance.Value : 50, 1),
            Size = Math.Clamp(search.Size <= 0 ? GalaxyQuery.DefaultSize : search.Size, 1, GalaxyQuery.MaxSize),
        };

        return true;
    }

    /// <summary>Matches a module or ship against the outfitting catalogue; neither is required.</summary>
    private static bool TryMatchStock(
        string? module,
        string? moduleClass,
        string? moduleRating,
        string? moduleMount,
        string? ship,
        out StationQuery stock,
        out string failure)
    {
        stock = new StationQuery();
        failure = string.Empty;

        string? matchedModule = null;
        string? matchedShip = null;

        if (!string.IsNullOrWhiteSpace(module))
        {
            if (string.IsNullOrWhiteSpace(moduleMount) && OutfittingItem.TakeMount(module.Trim(), out var unmounted) is { } said)
            {
                module = unmounted;
                moduleMount = said;
            }

            matchedModule = OutfittingCatalogue.MatchModule(module);

            if (matchedModule is null)
            {
                failure = Unknown("module", module, OutfittingCatalogue.Near(OutfittingCatalogue.Modules, module));
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(ship))
        {
            matchedShip = OutfittingCatalogue.MatchShip(ship);

            if (matchedShip is null)
            {
                failure = Unknown("ship", ship, OutfittingCatalogue.Near(OutfittingCatalogue.Ships, ship));
                return false;
            }

            if (OutfittingCatalogue.IsUnlisted(matchedShip))
            {
                failure = Unlisted(matchedShip);
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(moduleClass)
            && !OutfittingCatalogue.Classes.Contains(moduleClass.Trim()))
        {
            failure = $"'{moduleClass}' is not a module size. They run 0 to 8.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(moduleRating)
            && !OutfittingCatalogue.Ratings.Contains(moduleRating.Trim().ToUpperInvariant()))
        {
            failure = $"'{moduleRating}' is not a module rating. They run A to I.";
            return false;
        }

        var size = string.IsNullOrWhiteSpace(moduleClass) ? null : moduleClass.Trim();
        var rating = string.IsNullOrWhiteSpace(moduleRating) ? null : moduleRating.Trim().ToUpperInvariant();
        string? mount = null;

        if (!string.IsNullOrWhiteSpace(moduleMount))
        {
            mount = OutfittingItem.ReadMount(moduleMount);

            if (mount is null)
            {
                failure = $"'{moduleMount}' is not a mount. It is fixed, gimballed or turreted.";
                return false;
            }

            if (matchedModule is null)
            {
                failure = "A mount needs a module to go with it.";
                return false;
            }

            if (!OutfittingItem.TryMount(
                    matchedModule,
                    size is null ? null : int.Parse(size, CultureInfo.InvariantCulture),
                    rating,
                    mount,
                    out failure))
            {
                return false;
            }
        }

        stock = new StationQuery
        {
            Modules = matchedModule is null
                ? []
                : [new ModuleRequirement(matchedModule, size, rating, mount)],
            Ship = matchedShip,
        };

        return true;
    }

    private static string[] Split(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static bool Same(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The fixed, finished stations in one named system whose name matches <paramref name="station"/>, for
    /// resolving a station a story names to its market id (Phase 47).
    /// </summary>
    public static StationQuery Within(string system, string station) => new()
    {
        ReferenceSystem = system.Trim(),
        StationName = station.Trim(),
        MaxDistance = 1,
        Size = 20,
        FixedOnly = true,
    };

    /// <summary>Every fixed, finished station in and within a light year of <paramref name="system"/>, with no service filter.</summary>
    public static StationQuery InSystem(string system) => new()
    {
        ReferenceSystem = system.Trim(),
        MaxDistance = 1,
        Size = GalaxyQuery.MaxSize,
        FixedOnly = true,
    };

    /// <summary>The fixed, finished stations nearest a system, for proposing real places a story may dock at (Phase 47).</summary>
    public static StationQuery Near(string referenceSystem, double maxDistance, int size) => new()
    {
        ReferenceSystem = referenceSystem.Trim(),
        MaxDistance = Math.Clamp(maxDistance, 1, 500),
        Size = Math.Clamp(size, 1, 20),
        FixedOnly = true,
    };

    /// <summary>The fixed, finished stations nearest a point, for proposing real places a story may dock at.</summary>
    public static StationQuery Near(StarPosition referencePosition, double maxDistance, int size) => new()
    {
        ReferencePosition = referencePosition,
        MaxDistance = Math.Clamp(maxDistance, 1, 500),
        Size = Math.Clamp(size, 1, 20),
        FixedOnly = true,
    };

    /// <summary>The most stations one item's search asks for.</summary>
    public const int OutfitterSearchSize = 25;

    /// <summary>The stations near a system that sell one item, carriers left out.</summary>
    public static StationQuery Selling(OutfittingItem item, string referenceSystem, double maxDistance, PadSize? minPad, int? maxReportAgeHours) => new()
    {
        ReferenceSystem = referenceSystem.Trim(),
        MaxReportAgeHours = maxReportAgeHours,
        Modules = item.IsShip
            ? []
            : [new ModuleRequirement(item.Name, item.Class?.ToString(CultureInfo.InvariantCulture), item.Rating, item.Mount)],
        Ship = item.IsShip ? item.Name : null,
        MinPad = minPad,
        Kinds = [.. Enum.GetValues<StationKind>().Where(kind => kind != StationKind.FleetCarrier)],
        MaxDistance = Math.Max(maxDistance, 1),
        Size = OutfitterSearchSize,
    };

    /// <summary>The three kinds of material trader, in the index's own spelling.</summary>
    public static IReadOnlyList<string> TraderTypes { get; } = ["Raw", "Manufactured", "Encoded"];

    /// <summary>
    /// A search for material traders, which asks a different question from the module and ship search
    /// above and so does not go through it — <see cref="TryParse"/> requires something to be sold,
    /// and a trader sells nothing.
    /// </summary>
    public static StationQuery ForTrader(string? referenceSystem, string? traderType, double? maxDistance, int size)
    {
        var matched = traderType is null
            ? null
            : TraderTypes.FirstOrDefault(type => string.Equals(type, traderType.Trim(), StringComparison.OrdinalIgnoreCase));

        return new StationQuery
        {
            ReferenceSystem = string.IsNullOrWhiteSpace(referenceSystem) ? null : referenceSystem.Trim(),
            TraderType = matched,
            IsAboutTraders = true,
            MaxDistance = Math.Clamp(maxDistance is > 0 ? maxDistance.Value : 50, 1, 500),
            Size = Math.Clamp(size <= 0 ? GalaxyQuery.DefaultSize : size, 1, GalaxyQuery.MaxSize),
        };
    }

    /// <summary>Whether this search is about traders rather than about something being sold.</summary>
    public bool IsAboutTraders { get; init; }

    private static string Unlisted(string ship) =>
        $"No shipyard in the station index sells the {ship}.";

    private static string Unknown(string kind, string spoken, IReadOnlyList<string> near) =>
        near.Count > 0
            ? $"I don't know a {kind} called '{spoken}'. Did you mean {string.Join(", ", near)}?"
            : $"I don't know a {kind} called '{spoken}'.";
}
