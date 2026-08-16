namespace D47.Core.Journal;

/// <summary>
/// One commodity a construction site is asking for.
/// <para>
/// <b>Both numbers are the event's own and neither is accumulated.</b>
/// <c>ColonisationConstructionDepot</c> is a snapshot rather than a delta — measured over 6,330
/// events and 120,208 resource rows, with <c>RequiredAmount</c> never moving mid-build and
/// <c>ProvidedAmount</c> never decreasing across 119,887 consecutive comparisons. That is the trap
/// which caught <see cref="EngineerProgressState"/> and <see cref="ModuleStore"/>, silently both
/// times, and colonisation does not have it. See <c>docs/spikes/colonisation-sources.md</c>.
/// </para>
/// </summary>
public sealed record ConstructionResource(string Name, int Required, int Provided)
{
    /// <summary>
    /// The folded internal symbol, which is what joins this row to the hold and to the Commander's
    /// own contributions. <see cref="Name"/> is the spelling; this is the identity, and the two are
    /// never interchanged — see <see cref="JournalJson.Symbol(string?)"/>.
    /// </summary>
    public string? Symbol { get; init; }

    /// <summary>What is left. One subtraction, over the latest event — no history to keep.</summary>
    public int Remaining => Math.Max(0, Required - Provided);

    public bool IsMet => Provided >= Required;
}

/// <summary>
/// One construction site, as of the last time the Commander was docked at it.
/// </summary>
/// <param name="MarketId">
/// The site's identity. <b>Several sites can be open at once</b> — three at a time in the corpus —
/// so this is a collection with a selection rather than a "current site" field, and that is
/// expensive to change later.
/// </param>
public sealed record ConstructionSite(long MarketId)
{
    /// <summary>Where it is, taken from where the Commander was standing when the event landed.</summary>
    public string? StarSystem { get; init; }

    public string? StationName { get; init; }

    /// <summary>0 to 1, as Elite reports it. Never recomputed from the rows.</summary>
    public double Progress { get; init; }

    /// <summary>
    /// <b>The flag is what says finished, never "the events stopped"</b> — a completed site keeps
    /// reporting, 2 to 60 more events per site in the corpus.
    /// </summary>
    public bool Complete { get; init; }

    public bool Failed { get; init; }

    public IReadOnlyList<ConstructionResource> Resources { get; init; } = [];

    /// <summary>
    /// When the Commander last saw it. Taken off the event, never from a clock — and it is the
    /// figure that makes the freshness caveat honest rather than decorative.
    /// </summary>
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
/// Every construction site the Commander's journal has reported (list.md Phase 17, "A colonisation
/// plan writes the checklist").
/// <para>
/// <b>What d47 knows is exactly as fresh as the Commander's last visit, and it says so.</b> The
/// event fires while docked at that very site — 6,307 of 6,330 events — so this is a record of
/// where they have been rather than a live feed, and reporting it as live would be a wrong answer
/// that looks like a right one.
/// </para>
/// <para>
/// The fold lives here rather than in the checklist because it is journal state like every other
/// part of <see cref="CommanderGameState"/>, and because Phase 18's tracking item reads the same
/// thing. Phase 17 needs it to have something to diff a plan against; the reporting surface —
/// what is in the hold, what is on the carrier, the callouts — is that item and is not this one.
/// </para>
/// </summary>
public sealed record ColonisationSites
{
    public static readonly ColonisationSites Empty = new();

    private IReadOnlyDictionary<long, ConstructionSite> Sites { get; init; } =
        new Dictionary<long, ConstructionSite>();

    /// <summary>
    /// What this Commander has handed over, per site, per commodity symbol.
    /// <para>
    /// <b>Kept apart from the sites rather than folded into them</b>, and for two reasons. A
    /// contribution can name a <c>MarketID</c> no depot event has described yet, and turning that
    /// into a site record would invent a construction site out of a delivery note. And the two
    /// numbers mean different things: the depot's <c>ProvidedAmount</c> is what <em>everybody</em>
    /// has delivered, while this is what <em>you</em> did — the distinction that matters on a build
    /// several Commanders are hauling for.
    /// </para>
    /// <para>
    /// <b>This one really is an accumulation, and its scope is the session.</b> Each event reports
    /// one delivery rather than a running total, so it is summed — and the spine tails the newest
    /// journal, so what is summed is this session's deliveries and not a career's. Anything reading
    /// it has to say so.
    /// </para>
    /// </summary>
    private IReadOnlyDictionary<long, IReadOnlyDictionary<string, int>> Contributions { get; init; } =
        new Dictionary<long, IReadOnlyDictionary<string, int>>();

    public IReadOnlyList<ConstructionSite> All =>
        [.. Sites.Values.OrderByDescending(site => site.SeenAt)];

    /// <summary>Sites still being built. What "what am I working on" means here.</summary>
    public IReadOnlyList<ConstructionSite> Active =>
        [.. All.Where(site => !site.Complete && !site.Failed)];

    public bool IsKnown => Sites.Count > 0;

    public ConstructionSite? ById(long marketId) => Sites.GetValueOrDefault(marketId);

    /// <summary>
    /// The site a name refers to, or null when that is nought or several. Matched on the station
    /// and then on the system, because a Commander says "Ratraii" or "the outpost" rather than a
    /// market id.
    /// </summary>
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

    /// <summary>
    /// What this Commander has delivered to one site <b>this session</b>, per commodity symbol.
    /// Empty for a site they have not hauled to since d47 started reading.
    /// </summary>
    public IReadOnlyDictionary<string, int> MineDeliveredTo(long marketId) =>
        Contributions.GetValueOrDefault(marketId) ?? new Dictionary<string, int>();

    /// <param name="starSystem">Where the Commander was when the event landed, which is the site.</param>
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
                // Name_Localised is on every row of all 120,208 measured, which is why this needs
                // no commodity table at all — not even the one d47 already reads.
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
            // Kept where the event carries nothing, because the depot event names no system and
            // no station: the only thing that can say where a site is, is where the Commander was
            // standing the first time they saw it.
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

    /// <summary>
    /// One delivery, added to what this Commander has already handed over at that site.
    /// <para>
    /// Summed rather than replaced, which is the opposite of how the depot event is treated three
    /// lines above — and deliberately so. That one is a snapshot and this one is a receipt, and the
    /// only reason d47 can be sure which is which is that both were measured.
    /// </para>
    /// </summary>
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
            // Mixed case on every distinct symbol this event writes, against none on the depot's —
            // so folding here is what makes a delivery findable against the thing it paid for.
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
