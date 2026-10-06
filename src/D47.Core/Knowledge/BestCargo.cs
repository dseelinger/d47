using D47.Core.Journal;

namespace D47.Core.Knowledge;

/// <summary>What set a pick's tonnes.</summary>
public enum CargoLimit
{
    /// <summary>The free hold.</summary>
    Hold,

    /// <summary>What the station here sells.</summary>
    Supply,

    /// <summary>What the buyer at the destination takes.</summary>
    Demand,
}

/// <summary>One commodity worth buying, and the station at the destination that pays for it.</summary>
/// <param name="Tonnes">
/// What can actually be moved: the free hold, the supply here, or the demand there, whichever is least.
/// </param>
/// <param name="Limit">Which of the three <paramref name="Tonnes"/> is.</param>
public sealed record CargoPick(string Commodity, string Station, int ProfitPerTonne, int Tonnes, CargoLimit Limit)
{
    public long Total => (long)ProfitPerTonne * Tonnes;
}

/// <summary>What to buy at the market you are standing in for a system you are about to fly to (#312).</summary>
public static class BestCargo
{
    /// <summary>How many commodities are named aloud.</summary>
    public const int Picks = 2;

    /// <summary>The tonnes a trade fills: the cargo capacity less the limpets aboard, which never sell.</summary>
    public static int FreeHold(CommanderGameState state) =>
        Math.Max(0, (state.Ship.CargoCapacity ?? 0) - state.Hold.Of(Callouts.LimpetCallout.Limpet));

    /// <summary>
    /// The <paramref name="count"/> commodities <paramref name="here"/> sells that pay most at
    /// <paramref name="destination"/>, largest total first. Credits are never considered: the balance is not
    /// read anywhere in d47.
    /// </summary>
    public static IReadOnlyList<CargoPick> Rank(
        MarketSnapshot here,
        IReadOnlyList<MarketSnapshot> destination,
        int hold,
        int count = Picks)
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
                .Take(count),
        ];
    }

    /// <summary>
    /// The first <see cref="Picks"/> picks as prose, shared by Trading Mode (#312) and
    /// <c>best_commodities_for</c> (#313) so the same result reads the same sentence either way.
    /// </summary>
    public static string Describe(string destination, BestCargoAnswer answer)
    {
        if (answer.Picks.Count == 0)
        {
            return $"Nothing here sells at a profit in {destination}.";
        }

        var named = answer.Picks.Take(Picks).Select(pick =>
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

            var (tonnes, limit) = Limited(hold, offer.Supply, sale.Demand);

            if (tonnes <= 0)
            {
                continue;
            }

            var candidate = new CargoPick(
                offer.Commodity,
                market.Station,
                sale.SellPrice - offer.BuyPrice,
                tonnes,
                limit);

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

    /// <summary>The least of the three, and which it was; a tie goes to the hold, then the supply.</summary>
    private static (int Tonnes, CargoLimit Limit) Limited(int hold, int supply, int demand)
    {
        if (hold <= supply && hold <= demand)
        {
            return (hold, CargoLimit.Hold);
        }

        return supply <= demand ? (supply, CargoLimit.Supply) : (demand, CargoLimit.Demand);
    }
}
