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

    /// <summary>Light seconds from the system's entry point.</summary>
    public double? DistanceToArrival { get; init; }

    public string? Type { get; init; }

    public bool HasLargePad { get; init; }

    /// <summary>Pads of each size; null where the search does not give the count.</summary>
    public int? SmallPads { get; init; }

    public int? MediumPads { get; init; }

    public int? LargePads { get; init; }

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

    /// <summary>When the service last saw this station's stock.</summary>
    public DateTimeOffset? StockLastSeen { get; init; }

    /// <summary>The services the station offers, in the service's spelling.</summary>
    public IReadOnlyList<string> Services { get; init; } = [];

    /// <summary>The station's controlling minor faction.</summary>
    public string? ControllingFaction { get; init; }

    /// <summary>When the service last had a report of this station.</summary>
    public DateTimeOffset? UpdatedAt { get; init; }
}

/// <summary>A station search as asked, before <see cref="StationQuery"/> validates it.</summary>
public sealed record StationSearch
{
    public string? ReferenceSystem { get; init; }

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

    public string? TechnologyBroker { get; init; }

    public string? Module { get; init; }

    public string? ModuleClass { get; init; }

    public string? ModuleRating { get; init; }

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
    IReadOnlyList<StationSummary> Stations);

/// <summary>A validated search for somewhere to buy something.</summary>
public sealed record StationQuery
{
    private StationQuery()
    {
    }

    public string? ReferenceSystem { get; init; }

    /// <summary>Maximum light years from the reference.</summary>
    public double MaxDistance { get; init; } = 50;

    /// <summary>Searches the whole galaxy; <see cref="MaxDistance"/> is ignored.</summary>
    public bool Unbounded { get; init; }

    /// <summary>The catalogue name of a module to be sold there, if that is the question.</summary>
    public string? Module { get; init; }

    public string? ModuleClass { get; init; }

    public string? ModuleRating { get; init; }

    /// <summary>The catalogue name of a ship to be sold there, if that is the question.</summary>
    public string? Ship { get; init; }

    /// <summary>Whether to insist on a large landing pad.</summary>
    public bool LargePadOnly { get; init; }

    /// <summary>What kind of material trader, where that is the question — Raw, Manufactured or Encoded.</summary>
    public string? TraderType { get; init; }

    public int Size { get; init; } = 5;

    /// <summary>The kinds of station to include; null sends no type filter.</summary>
    public IReadOnlyList<StationKind>? Kinds { get; init; }

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
    ];

    /// <summary>The two kinds of technology broker, in the index's own spelling.</summary>
    public static IReadOnlyList<string> BrokerTypes { get; } = ["Guardian", "Human"];

    /// <summary>The pad sizes <c>min_pad</c> offers.</summary>
    public static IReadOnlyList<string> MinPads { get; } = ["Medium", "Large"];

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
        out string failure)
    {
        query = new StationQuery();

        if (!TryMatchStock(module, moduleClass, moduleRating, ship, out var stock, out failure))
        {
            return false;
        }

        if (stock.Module is null && stock.Ship is null)
        {
            failure = "Say what to look for — a module or a ship.";
            return false;
        }

        query = stock with
        {
            ReferenceSystem = string.IsNullOrWhiteSpace(referenceSystem) ? null : referenceSystem.Trim(),
            LargePadOnly = largePadOnly,

            // Bounded on both ends.
            MaxDistance = Math.Clamp(maxDistance is > 0 ? maxDistance.Value : 50, 1, 500),
            Size = Math.Clamp(size <= 0 ? 5 : size, 1, 20),
        };

        return true;
    }

    /// <summary>Builds a search by station type, pad, services and filters, or says what was wrong.</summary>
    public static bool TryParse(StationSearch search, out StationQuery query, out string failure)
    {
        query = new StationQuery();

        if (!TryMatchStock(search.Module, search.ModuleClass, search.ModuleRating, search.Ship, out var stock, out failure))
        {
            return false;
        }

        List<StationKind>? kinds = null;

        if (!string.IsNullOrWhiteSpace(search.StationTypes))
        {
            kinds = [];

            foreach (var name in Split(search.StationTypes))
            {
                if (StationTypes.FirstOrDefault(type => Same(type.Name, name)) is not { } type)
                {
                    failure = $"'{name}' is not a station type I know. It has to be one of: "
                              + $"{string.Join(", ", StationTypes.Select(known => known.Name))}.";
                    return false;
                }

                kinds.Add(type.Kind);
            }
        }

        PadSize? pad = null;

        if (!string.IsNullOrWhiteSpace(search.MinPad))
        {
            if (MinPads.FirstOrDefault(size => Same(size, search.MinPad.Trim())) is not { } size)
            {
                failure = $"'{search.MinPad}' is not a pad size to insist on. It is Medium or Large.";
                return false;
            }

            pad = Enum.Parse<PadSize>(size);
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

        var furthest = search.MaxStationDistance is > 0 ? search.MaxStationDistance : null;

        if (kinds is null && pad is null && furthest is null && services.Count == 0 && trader is null
            && broker is null && stock.Module is null && stock.Ship is null && criteria.Count == 0)
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
            Kinds = kinds is null
                ? [.. Enum.GetValues<StationKind>().Where(kind => kind != StationKind.FleetCarrier)]
                : [.. kinds.Distinct()],
            MinPad = pad,
            MaxStationDistance = furthest,
            Services = services,
            TraderType = trader,
            TechnologyBroker = broker,
            Criteria = criteria,
            MaxDistance = Math.Clamp(search.MaxDistance is > 0 ? search.MaxDistance.Value : 50, 1, 500),
            Size = Math.Clamp(search.Size <= 0 ? 5 : search.Size, 1, 20),
        };

        return true;
    }

    /// <summary>Matches a module or ship against the outfitting catalogue; neither is required.</summary>
    private static bool TryMatchStock(
        string? module,
        string? moduleClass,
        string? moduleRating,
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

        stock = new StationQuery
        {
            Module = matchedModule,
            ModuleClass = string.IsNullOrWhiteSpace(moduleClass) ? null : moduleClass.Trim(),
            ModuleRating = string.IsNullOrWhiteSpace(moduleRating) ? null : moduleRating.Trim().ToUpperInvariant(),
            Ship = matchedShip,
        };

        return true;
    }

    private static string[] Split(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static bool Same(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Every station in one named system, for resolving a station a story names to its market id (Phase
    /// 47).
    /// </summary>
    public static StationQuery Within(string system) => new()
    {
        ReferenceSystem = system.Trim(),
        MaxDistance = 1,
        Size = 20,
    };

    /// <summary>The stations nearest a system, for proposing real places a story may dock at (Phase 47).</summary>
    public static StationQuery Near(string referenceSystem, double maxDistance, int size) => new()
    {
        ReferenceSystem = referenceSystem.Trim(),
        MaxDistance = Math.Clamp(maxDistance, 1, 500),
        Size = Math.Clamp(size, 1, 20),
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
            Size = Math.Clamp(size <= 0 ? 5 : size, 1, 20),
        };
    }

    /// <summary>Whether this search is about traders rather than about something being sold.</summary>
    public bool IsAboutTraders { get; init; }

    private static string Unknown(string kind, string spoken, IReadOnlyList<string> near) =>
        near.Count > 0
            ? $"I don't know a {kind} called '{spoken}'. Did you mean {string.Join(", ", near)}?"
            : $"I don't know a {kind} called '{spoken}'.";
}
