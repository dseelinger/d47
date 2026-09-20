using D47.Core.Journal;

namespace D47.Core.Knowledge;

/// <summary>One commodity worth buying, and the station at the destination that pays for it.</summary>
/// <param name="Tonnes">
/// What can actually be moved: the free hold, the supply here, or the demand there, whichever is least.
/// </param>
public sealed record CargoPick(string Commodity, string Station, int ProfitPerTonne, int Tonnes)
{
    public long Total => (long)ProfitPerTonne * Tonnes;
}

/// <summary>What to buy at the market you are standing in for a system you are about to fly to (#312).</summary>
public static class BestCargo
{
    /// <summary>How many commodities are named.</summary>
    public const int Picks = 2;

    /// <summary>
    /// The commodities <paramref name="here"/> sells that pay most at <paramref name="destination"/>,
    /// largest total first. Credits are never considered: the balance is not read anywhere in d47.
    /// </summary>
    public static IReadOnlyList<CargoPick> Rank(
        MarketSnapshot here,
        IReadOnlyList<MarketSnapshot> destination,
        int hold)
    {
        if (hold <= 0 || destination.Count == 0)
        {
            return [];
        }

        var picks = new List<CargoPick>();

        foreach (var offer in here.Quotes.Values)
        {
            if (offer.BuyPrice <= 0 || offer.Supply <= 0)
            {
                continue;
            }

            if (Best(offer, destination, hold) is { } pick)
            {
                picks.Add(pick);
            }
        }

        return
        [
            .. picks
                .OrderByDescending(pick => pick.Total)
                .ThenByDescending(pick => pick.ProfitPerTonne)
                .ThenBy(pick => pick.Commodity, StringComparer.Ordinal)
                .Take(Picks),
        ];
    }

    /// <summary>
    /// The picks as prose, shared by Trading Mode (#312) and <c>best_commodities_for</c> (#313) so the
    /// same result reads the same sentence either way.
    /// </summary>
    public static string Describe(string destination, BestCargoAnswer answer)
    {
        if (answer.Picks.Count == 0)
        {
            return $"Nothing here sells at a profit in {destination}.";
        }

        var named = answer.Picks.Select(pick =>
            $"{pick.Commodity} to {pick.Station}, {Credits(pick.ProfitPerTonne)} a tonne, "
            + $"{Credits(pick.Total)} for {pick.Tonnes} tonnes");

        return $"Best cargo for {destination}: {string.Join("; then ", named)}.";
    }

    private static string Credits(long amount)
    {
        var banded = SpokenCredits.Band(amount);

        return amount < 1_000_000 ? banded + " Cr" : banded;
    }

    private static CargoPick? Best(MarketQuote offer, IReadOnlyList<MarketSnapshot> destination, int hold)
    {
        CargoPick? best = null;

        foreach (var market in destination)
        {
            if (market.Quote(offer.Commodity) is not { } sale
                || sale.Demand <= 0
                || sale.SellPrice <= offer.BuyPrice)
            {
                continue;
            }

            var tonnes = Math.Min(hold, Math.Min(offer.Supply, sale.Demand));

            if (tonnes <= 0)
            {
                continue;
            }

            var candidate = new CargoPick(
                offer.Commodity,
                market.Station,
                sale.SellPrice - offer.BuyPrice,
                tonnes);

            if (best is null
                || candidate.Total > best.Total
                || (candidate.Total == best.Total
                    && string.CompareOrdinal(candidate.Station, best.Station) < 0))
            {
                best = candidate;
            }
        }

        return best;
    }
}
