using D47.Core.Journal;

namespace D47.Core.Knowledge;

/// <summary>
/// One station's part in a sourcing plan.
/// </summary>
/// <param name="Market">Where.</param>
/// <param name="Lots">What it covers, and at what price.</param>
/// <param name="Distance">Light years from the reference, or zero where nothing could be measured.</param>
public sealed record SourcingStop(MarketSnapshot Market, IReadOnlyList<SourcingLot> Lots, double Distance)
{
    /// <summary>What the whole stop costs.</summary>
    public long Total => Lots.Sum(lot => lot.Total);

    /// <summary>How many of the outstanding commodities this one station clears.</summary>
    public int Covers => Lots.Count;
}

/// <param name="Commodity">The market's spelling, which is what a Commander reads on the board.</param>
/// <param name="Symbol">The folded symbol, which is what it joined on.</param>
/// <param name="Tonnes">How many to buy here — never more than the station has or the site needs.</param>
/// <param name="UnitPrice">Per tonne.</param>
public sealed record SourcingLot(string Commodity, string Symbol, int Tonnes, int UnitPrice)
{
    public long Total => (long)UnitPrice * Tonnes;
}

/// <summary>
/// Everything one build still needs, and where to buy it (list.md Phase 50).
/// </summary>
/// <param name="Stops">The stations, best first.</param>
/// <param name="Unpriced">
/// Outstanding commodities no market in range could price, by display name.
/// <para>
/// <b>The single most important field here.</b> A sourcing plan that quietly omitted a commodity
/// because its name did not join is Phase 17's seam failing with a Commander's week on it, so
/// nothing is ever dropped in silence: every outstanding row either resolves to a market row or is
/// named here.
/// </para>
/// </param>
/// <param name="Shortfalls">
/// Commodities that were found but not in enough quantity, with how many tonnes are still short
/// after every station in range is counted.
/// </param>
public sealed record SourcingPlan(
    IReadOnlyList<SourcingStop> Stops,
    IReadOnlyList<string> Unpriced,
    IReadOnlyDictionary<string, int> Shortfalls)
{
    public static readonly SourcingPlan Empty = new([], [], new Dictionary<string, int>(StringComparer.Ordinal));

    public long Total => Stops.Sum(stop => stop.Total);

    /// <summary>Whether every outstanding commodity is covered somewhere, in full.</summary>
    public bool IsComplete => Unpriced.Count == 0 && Shortfalls.Count == 0;
}

/// <summary>
/// Where to buy everything one construction site still needs (list.md Phase 50).
/// <para>
/// <b>A covering problem, and not the trade planner.</b> <see cref="TradePlanner"/> maximises
/// credits over hops and carries credits and cargo between them, because holding a commodity past a
/// poor buyer is sometimes the move. None of that is this: the cargo is decided before the
/// Commander leaves — it is the depot's list — the objective is trips and time rather than profit,
/// and the binding constraint is supply where you buy rather than demand where you sell.
/// </para>
/// <para>
/// <b>Pure, and beside the planner for the reason that made Phase 36 testable</b>: handed a list of
/// markets it answers with a plan, reads no clock, opens no socket, and knows nothing about Spansh.
/// </para>
/// <para>
/// <b>Greedy, and that is not a compromise at this size.</b> Minimum set cover is NP-hard in
/// general and entirely untroubled by it here — twenty commodities against a few hundred markets —
/// and the greedy answer is within a log factor of optimal on a problem where a Commander could not
/// tell the difference between four stops and the theoretical three.
/// </para>
/// </summary>
public static class ColonisationSourcing
{
    /// <summary>
    /// Builds a plan.
    /// </summary>
    /// <param name="outstanding">
    /// What the site still needs. Taken as given and never recomputed —
    /// <c>ColonisationConstructionDepot</c> is a snapshot rather than a delta, and this is a fact
    /// off the Commander's own disk rather than anything predicted.
    /// </param>
    /// <param name="markets">Everything in range, already aged out by whoever knew the time.</param>
    /// <param name="origin">Where to measure from, or null when nothing could be placed.</param>
    /// <param name="largePadOnly">Whether to insist on a large pad.</param>
    /// <param name="maxStops">A ceiling on stations, so an answer stays one a Commander can act on.</param>
    public static SourcingPlan Plan(
        IReadOnlyList<ConstructionResource> outstanding,
        IReadOnlyList<MarketSnapshot> markets,
        MarketSnapshot? origin,
        bool largePadOnly = false,
        int maxStops = 6)
    {
        var wanted = new Dictionary<string, Want>(StringComparer.Ordinal);

        foreach (var resource in outstanding)
        {
            if (resource.Remaining <= 0)
            {
                continue;
            }

            // The symbol is the identity and the name is the spelling, and the two are never
            // interchanged. A depot row carries the folded symbol already; one that somehow does
            // not gets its name folded the same way rather than being matched on the spelling.
            var symbol = resource.Symbol ?? JournalJson.Symbol(resource.Name);

            if (symbol is null)
            {
                continue;
            }

            wanted[symbol] = new Want(symbol, resource.Name, resource.Remaining);
        }

        if (wanted.Count == 0)
        {
            return SourcingPlan.Empty;
        }

        var usable = markets
            .Where(market => !market.IsCarrier)
            .Where(market => !largePadOnly || market.HasLargePad)
            .ToArray();

        var outstandingBySymbol = wanted.ToDictionary(pair => pair.Key, pair => pair.Value.Tonnes, StringComparer.Ordinal);
        var stops = new List<SourcingStop>();

        while (outstandingBySymbol.Count > 0 && stops.Count < maxStops)
        {
            var best = Best(usable, outstandingBySymbol, wanted, origin, stops);

            if (best is null)
            {
                break;
            }

            stops.Add(best);

            foreach (var lot in best.Lots)
            {
                var left = outstandingBySymbol[lot.Symbol] - lot.Tonnes;

                if (left <= 0)
                {
                    outstandingBySymbol.Remove(lot.Symbol);
                }
                else
                {
                    outstandingBySymbol[lot.Symbol] = left;
                }
            }
        }

        // What is left over is two different failures and they are reported as two. A commodity no
        // market priced at all is a join that may have gone wrong or a thing nobody stocks nearby;
        // one that was found and ran short is a quantity problem. Telling a Commander to widen the
        // search is right for the first and useless for the second.
        var unpriced = new List<string>();
        var shortfalls = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var (symbol, left) in outstandingBySymbol)
        {
            var name = wanted[symbol].Name;

            if (usable.Any(market => Supply(market, wanted[symbol]) > 0))
            {
                shortfalls[name] = left;
            }
            else
            {
                unpriced.Add(name);
            }
        }

        unpriced.Sort(StringComparer.OrdinalIgnoreCase);

        return new SourcingPlan(stops, unpriced, shortfalls);
    }

    /// <summary>
    /// The station that clears most of what is left. Ties break on cost and then on distance, so
    /// two stations covering the same six are separated by what the trip actually costs rather
    /// than by which the sweep happened to return first.
    /// </summary>
    private static SourcingStop? Best(
        IReadOnlyList<MarketSnapshot> markets,
        IReadOnlyDictionary<string, int> left,
        IReadOnlyDictionary<string, Want> wanted,
        MarketSnapshot? origin,
        IReadOnlyList<SourcingStop> already)
    {
        SourcingStop? best = null;

        foreach (var market in markets)
        {
            if (already.Any(stop => stop.Market.IsSamePlaceAs(market.Station, market.System)))
            {
                continue;
            }

            var lots = new List<SourcingLot>();

            foreach (var (symbol, tonnes) in left)
            {
                var quote = Quote(market, wanted[symbol]);

                if (quote is null || quote.BuyPrice <= 0 || quote.Supply <= 0)
                {
                    continue;
                }

                lots.Add(new SourcingLot(
                    quote.Commodity,
                    symbol,
                    Math.Min(tonnes, quote.Supply),
                    quote.BuyPrice));
            }

            if (lots.Count == 0)
            {
                continue;
            }

            var stop = new SourcingStop(market, lots, origin?.DistanceTo(market) ?? 0);

            if (best is null || Better(stop, best))
            {
                best = stop;
            }
        }

        return best;

        static bool Better(SourcingStop candidate, SourcingStop incumbent) =>
            candidate.Covers != incumbent.Covers
                ? candidate.Covers > incumbent.Covers
                : candidate.Total != incumbent.Total
                    ? candidate.Total < incumbent.Total
                    : candidate.Distance < incumbent.Distance;
    }

    /// <summary>
    /// A market's quote for one outstanding row. <b>The join, and the thing most likely to ship
    /// broken.</b>
    /// <para>
    /// <b>It is the display name that joins here, not the symbol</b>, and getting that the wrong
    /// way round is the mistake this was written with on the first attempt. Elite's internal symbol
    /// is not derivable from the spelling: <em>Low Temperature Diamonds</em> is
    /// <c>$lowtemperaturediamond_name;</c>, which loses the spaces <em>and</em> the plural, so no
    /// fold produces one from the other and a lookup table would be needed to try. There is no need
    /// for one. <c>ColonisationConstructionDepot</c> writes <c>Name_Localised</c> on every row of
    /// all 120,208 measured, and both market sources are keyed by that same display name —
    /// <c>Market.json</c> by <c>Name_Localised</c> and spansh by its <c>commodity</c> field — so
    /// the two meet directly.
    /// </para>
    /// <para>
    /// The symbol is still the identity elsewhere and is not being retired: it is what joins the
    /// depot to the <em>hold</em>, which is Phase 17's business and carries symbols rather than
    /// spellings. Two joins for two purposes, and using one where the other is meant is exactly the
    /// failure Phase 17 measured going the other way.
    /// </para>
    /// <para>
    /// The symbol fold survives here as a fallback for the one case the display join cannot cover:
    /// a row where Elite omitted <c>_Localised</c>, so both sides are holding a raw symbol.
    /// </para>
    /// </summary>
    private static MarketQuote? Quote(MarketSnapshot market, Want want)
    {
        if (market.Quote(want.Name) is { } byName)
        {
            return byName;
        }

        foreach (var (name, quote) in market.Quotes)
        {
            if (string.Equals(JournalJson.Symbol(name), want.Symbol, StringComparison.Ordinal))
            {
                return quote;
            }
        }

        return null;
    }

    private static int Supply(MarketSnapshot market, Want want) => Quote(market, want)?.Supply ?? 0;

    private readonly record struct Want(string Symbol, string Name, int Tonnes);
}

/// <summary>
/// The last sourcing answer, so the spoken one and the drawn one are one answer (list.md Phase 50).
/// <para>
/// <b>The same arrangement <see cref="CommodityBoard"/> makes for one commodity</b>, and in memory
/// for the same reason: the shopping list is built out of network prices that age in hours, and one
/// restored from a file would look current because it was saved rather than because it is true.
/// A route plan survives a restart; this deliberately does not.
/// </para>
/// </summary>
public sealed class SourcingBoard
{
    private readonly Lock _gate = new();

    private SourcingPosting? _last;

    /// <summary>What was asked and what came back, or null if nothing has been asked yet.</summary>
    public SourcingPosting? Last
    {
        get
        {
            lock (_gate)
            {
                return _last;
            }
        }
    }

    public void Post(SourcingPosting posting)
    {
        lock (_gate)
        {
            _last = posting;
        }
    }

    /// <summary>Raised when a new answer lands, so a surface can redraw without polling.</summary>
    public event Action? Posted;

    public void Announce() => Posted?.Invoke();
}

/// <param name="Site">Which site it was about, in the words the Commander would use for it.</param>
/// <param name="Answer">What came back.</param>
/// <param name="Near">The system it was measured from.</param>
/// <param name="Carrier">
/// What the Commander said was already on their carrier and was therefore taken off the shopping
/// list. Empty where they have said nothing — and never derived, which is the ruling this whole
/// field exists under.
/// </param>
/// <param name="AskedAt">When. Shown on the page, because the answer has its own age.</param>
public sealed record SourcingPosting(
    string Site,
    SourcingAnswer Answer,
    string Near,
    IReadOnlyList<CarrierStock> Carrier,
    DateTimeOffset AskedAt);
