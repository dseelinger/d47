namespace D47.Core.Journal;

/// <summary>One commodity a construction site is asking for.</summary>
public sealed record ConstructionResource(string Name, int Required, int Provided)
{
    /// <summary>
    /// The folded internal symbol, which is what joins this row to the hold and to the Commander's own
    /// contributions.
    /// </summary>
    public string? Symbol { get; init; }

    /// <summary>What is left.</summary>
    public int Remaining => Math.Max(0, Required - Provided);

    public bool IsMet => Provided >= Required;
}

/// <summary>One construction site, as of the last time the Commander was docked at it.</summary>
/// <param name="MarketId">The site's identity.</param>
public sealed record ConstructionSite(long MarketId)
{
    /// <summary>Where it is, taken from where the Commander was standing when the event landed.</summary>
    public string? StarSystem { get; init; }

    public string? StationName { get; init; }

    /// <summary>0 to 1, as Elite reports it.</summary>
    public double Progress { get; init; }

    /// <summary>
    /// The flag is what says finished, never "the events stopped" — a completed site keeps reporting, 2
    /// to 60 more events per site in the corpus.
    /// </summary>
    public bool Complete { get; init; }

    public bool Failed { get; init; }

    public IReadOnlyList<ConstructionResource> Resources { get; init; } = [];

    /// <summary>When the Commander last saw it.</summary>
    public DateTimeOffset SeenAt { get; init; }

    public IReadOnlyList<ConstructionResource> Outstanding =>
        [.. Resources.Where(resource => !resource.IsMet).OrderByDescending(resource => resource.Remaining)];

    public string Where => (StationName, StarSystem) switch
    {
        ({ } station, { } system) => $"{station}, {system}",
        ({ } station, null) => station,
        (null, { } system) => system,
        _ => "a construction site",
    };
}

/// <summary>One outstanding commodity at a site, netted against the hold and the carrier.</summary>
/// <param name="Resource">The manifest row.</param>
/// <param name="Remaining">What the site still needs.</param>
/// <param name="InHold">Tonnes in the hold.</param>
/// <param name="OnCarrier">Tonnes on the carrier, or null unless the Commander owns one whose ledger is reconciled.</param>
/// <param name="OrderOpen">Whether an order is open on the carrier for this commodity.</param>
/// <param name="ToBuy"><paramref name="Remaining"/> less the hold and the carrier, held at zero.</param>
public sealed record ConstructionNeeds(
    ConstructionResource Resource,
    int Remaining,
    int InHold,
    int? OnCarrier,
    bool OrderOpen,
    int ToBuy)
{
    /// <summary>Every outstanding commodity of one site.</summary>
    public static IReadOnlyList<ConstructionNeeds> For(
        ConstructionSite site,
        CargoHold hold,
        CarrierState carrier)
    {
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(hold);
        ArgumentNullException.ThrowIfNull(carrier);

        var counted = carrier.Owned && carrier.Hold.Reconciled == true;

        return
        [
            .. site.Outstanding.Select(resource =>
            {
                var inHold = hold.Of(resource.Symbol);
                int? onCarrier = counted && resource.Symbol is { } symbol
                    ? carrier.Hold.Holding(symbol) ?? 0
                    : null;

                return new ConstructionNeeds(
                    resource,
                    resource.Remaining,
                    inHold,
                    onCarrier,
                    carrier.Owned && resource.Symbol is { } named && carrier.Hold.OrderOpen(named),
                    Math.Max(0, resource.Remaining - inHold - (onCarrier ?? 0)));
            }),
        ];
    }

    /// <summary>The row a sourcing search asks for: <see cref="ToBuy"/> still required, none provided.</summary>
    public ConstructionResource ToSource() =>
        Resource with { Required = ToBuy, Provided = 0 };
}

/// <summary>A system the Commander claimed at the colonisation contact.</summary>
public sealed record ColonisationClaim(string StarSystem, DateTimeOffset ClaimedAt)
{
    public long? SystemAddress { get; init; }

    /// <summary>When the beacon was deployed in the system, or null while it has not been.</summary>
    public DateTimeOffset? BeaconDeployedAt { get; init; }

    /// <summary>The first construction site seen in the system, which is its primary port.</summary>
    public long? FirstSiteMarketId { get; init; }
}

/// <summary>
/// Every construction site the Commander's journal has reported (Phase 17, "A colonisation plan writes
/// the checklist").
/// </summary>
public sealed record ColonisationSites
{
    public static readonly ColonisationSites Empty = new();

    private IReadOnlyDictionary<long, ConstructionSite> Sites { get; init; } =
        new Dictionary<long, ConstructionSite>();

    /// <summary>What this Commander has handed over, per site, per commodity symbol.</summary>
    private IReadOnlyDictionary<long, IReadOnlyDictionary<string, int>> Contributions { get; init; } =
        new Dictionary<long, IReadOnlyDictionary<string, int>>();

    /// <summary>Systems claimed, oldest first.</summary>
    public IReadOnlyList<ColonisationClaim> Claims { get; private init; } = [];

    public IReadOnlyList<ConstructionSite> All =>
        [.. Sites.Values.OrderByDescending(site => site.SeenAt)];

    /// <summary>Sites still being built.</summary>
    public IReadOnlyList<ConstructionSite> Active =>
        [.. All.Where(site => !site.Complete && !site.Failed)];

    /// <summary>How recently an active site must have been seen to count as current.</summary>
    public static readonly TimeSpan CurrentFor = TimeSpan.FromDays(14);

    /// <summary>Active sites seen within <see cref="CurrentFor"/> of <paramref name="now"/>.</summary>
    public IReadOnlyList<ConstructionSite> Current(DateTimeOffset now) =>
        [.. Active.Where(site => now - site.SeenAt <= CurrentFor)];

    /// <summary>Active sites last seen longer ago than <see cref="CurrentFor"/>, newest first.</summary>
    public IReadOnlyList<ConstructionSite> NotSeenSince(DateTimeOffset now) =>
        [.. Active.Where(site => now - site.SeenAt > CurrentFor)];

    public bool IsKnown => Sites.Count > 0 || Claims.Count > 0;

    public ConstructionSite? ById(long marketId) => Sites.GetValueOrDefault(marketId);

    /// <summary>The site a name refers to, or null when that is nought or several.</summary>
    public ConstructionSite? Named(string? spoken)
    {
        if (string.IsNullOrWhiteSpace(spoken))
        {
            return null;
        }

        var wanted = spoken.Trim();

        var matches = All
            .Where(site =>
                string.Equals(site.StationName, wanted, StringComparison.OrdinalIgnoreCase)
                || string.Equals(site.StarSystem, wanted, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return matches.Count == 1 ? matches[0] : null;
    }

    /// <summary>Every site in one system, which is the scope a colonisation plan is keyed on.</summary>
    public IReadOnlyList<ConstructionSite> InSystem(string? system) =>
        string.IsNullOrWhiteSpace(system)
            ? []
            : [.. All.Where(site => string.Equals(site.StarSystem, system, StringComparison.OrdinalIgnoreCase))];

    /// <summary>What this Commander has delivered to one site this session, per commodity symbol.</summary>
    public IReadOnlyDictionary<string, int> MineDeliveredTo(long marketId) =>
        Contributions.GetValueOrDefault(marketId) ?? new Dictionary<string, int>();

    /// <summary>
    /// <param name="starSystem">Where the Commander was when the event landed, which is the
    /// site.</param> <param name="stationName">The same, for the depot itself.</param>
    /// </summary>
    /// <param name="starSystem">
    /// Where the Commander was when the event landed, which is the site.
    /// </param>
    /// <param name="stationName">The same, for the depot itself.</param>
    public ColonisationSites Apply(JournalEvent journalEvent, string? starSystem = null, string? stationName = null)
    {
        if (journalEvent.Kind == "ColonisationContribution")
        {
            return Contribute(journalEvent);
        }

        if (journalEvent.Kind == "ColonisationSystemClaim")
        {
            return Claim(journalEvent);
        }

        if (journalEvent.Kind == "ColonisationBeaconDeployed")
        {
            return Beacon(journalEvent, starSystem);
        }

        if (journalEvent.Kind != "ColonisationConstructionDepot"
            || journalEvent.Long("MarketID") is not { } marketId)
        {
            return this;
        }

        var resources = journalEvent
            .Items("ResourcesRequired")
            .Select(row => new ConstructionResource(
                // Name_Localised is on every row of all 120,208 measured, which is why this needs no
                // commodity table at all — not even the one d47 already reads.
                row.Named("Name") ?? "an unnamed commodity",
                row.Int("RequiredAmount") ?? 0,
                row.Int("ProvidedAmount") ?? 0)
            {
                Symbol = row.Symbol("Name"),
            })
            .ToList();

        var known = Sites.GetValueOrDefault(marketId);

        var site = new ConstructionSite(marketId)
        {
            // Kept where the event carries nothing, because the depot event names no system and no station:
            // the only thing that can say where a site is, is where the Commander was standing the first time
            // they saw it.
            StarSystem = starSystem ?? known?.StarSystem,
            StationName = stationName ?? known?.StationName,
            Progress = journalEvent.Double("ConstructionProgress") ?? 0,
            Complete = journalEvent.Bool("ConstructionComplete"),
            Failed = journalEvent.Bool("ConstructionFailed"),
            Resources = resources,
            SeenAt = journalEvent.Timestamp,
        };

        var updated = new Dictionary<long, ConstructionSite>(Sites) { [marketId] = site };

        return this with { Sites = updated, Claims = WithFirstSite(starSystem, marketId) };
    }

    /// <summary>The newest claim in <paramref name="starSystem"/> without a primary port yet gets this site.</summary>
    private IReadOnlyList<ColonisationClaim> WithFirstSite(string? starSystem, long marketId)
    {
        if (starSystem is null
            || Claims.Any(claim => claim.FirstSiteMarketId == marketId)
            || Claims.LastOrDefault(claim => Same(claim.StarSystem, starSystem)) is not { FirstSiteMarketId: null } open)
        {
            return Claims;
        }

        return [.. Claims.Select(claim => claim == open ? claim with { FirstSiteMarketId = marketId } : claim)];
    }

    private ColonisationSites Claim(JournalEvent journalEvent)
    {
        if (journalEvent.String("StarSystem") is not { Length: > 0 } system)
        {
            return this;
        }

        var claimed = new ColonisationClaim(system, journalEvent.Timestamp)
        {
            SystemAddress = journalEvent.Long("SystemAddress"),
        };

        return this with
        {
            Claims =
            [
                .. Claims.Where(claim => claim.ClaimedAt != claimed.ClaimedAt || !Same(claim.StarSystem, system)),
                claimed,
            ],
        };
    }

    /// <summary>The newest undeployed claim on the system the beacon went up in, or the newest when the system is unknown.</summary>
    private ColonisationSites Beacon(JournalEvent journalEvent, string? starSystem)
    {
        var pending = Claims.LastOrDefault(claim =>
            claim.BeaconDeployedAt is null && (starSystem is null || Same(claim.StarSystem, starSystem)));

        return pending is null
            ? this
            : this with
            {
                Claims =
                [
                    .. Claims.Select(claim =>
                        claim == pending ? claim with { BeaconDeployedAt = journalEvent.Timestamp } : claim),
                ],
            };
    }

    private static bool Same(string? left, string? right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// These sites under <paramref name="live"/>: each site is the one seen later, and a tie goes to the live
    /// one. Contributions are the live state's only.
    /// </summary>
    public ColonisationSites With(ColonisationSites live)
    {
        ArgumentNullException.ThrowIfNull(live);

        var merged = new Dictionary<long, ConstructionSite>(live.Sites);

        foreach (var (marketId, site) in Sites)
        {
            if (!merged.TryGetValue(marketId, out var held) || held.SeenAt < site.SeenAt)
            {
                merged[marketId] = site;
            }
        }

        var claims = new List<ColonisationClaim>(live.Claims);

        foreach (var claim in Claims)
        {
            var index = claims.FindIndex(
                held => held.ClaimedAt == claim.ClaimedAt && Same(held.StarSystem, claim.StarSystem));

            if (index < 0)
            {
                claims.Add(claim);
                continue;
            }

            var kept = claims[index];

            claims[index] = kept with
            {
                SystemAddress = kept.SystemAddress ?? claim.SystemAddress,
                BeaconDeployedAt = kept.BeaconDeployedAt ?? claim.BeaconDeployedAt,
                FirstSiteMarketId = kept.FirstSiteMarketId ?? claim.FirstSiteMarketId,
            };
        }

        return live with { Sites = merged, Claims = [.. claims.OrderBy(claim => claim.ClaimedAt)] };
    }

    /// <summary>One delivery, added to what this Commander has already handed over at that site.</summary>
    private ColonisationSites Contribute(JournalEvent journalEvent)
    {
        if (journalEvent.Long("MarketID") is not { } marketId)
        {
            return this;
        }

        var running = new Dictionary<string, int>(MineDeliveredTo(marketId));
        var moved = false;

        foreach (var row in journalEvent.Items("Contributions"))
        {
            // Mixed case on every distinct symbol this event writes, against none on the depot's — so folding
            // here is what makes a delivery findable against the thing it paid for.
            if (row.Symbol("Name") is not { } symbol || row.Int("Amount") is not { } amount)
            {
                continue;
            }

            running[symbol] = running.GetValueOrDefault(symbol) + amount;
            moved = true;
        }

        if (!moved)
        {
            return this;
        }

        var updated =
            new Dictionary<long, IReadOnlyDictionary<string, int>>(Contributions) { [marketId] = running };

        return this with { Contributions = updated };
    }
}
