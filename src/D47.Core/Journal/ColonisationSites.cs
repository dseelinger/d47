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

    public IReadOnlyList<ConstructionSite> All =>
        [.. Sites.Values.OrderByDescending(site => site.SeenAt)];

    /// <summary>Sites still being built.</summary>
    public IReadOnlyList<ConstructionSite> Active =>
        [.. All.Where(site => !site.Complete && !site.Failed)];

    public bool IsKnown => Sites.Count > 0;

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

        return this with { Sites = updated };
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
