using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// Everything one build still needs, and where to buy it (list.md Phase 50).
/// <para>
/// The covering arithmetic and — first, because it is the thing most likely to ship broken — the
/// join between the depot's folded symbols and the markets' display names.
/// </para>
/// </summary>
public class ColonisationSourcingTests
{
    private static ConstructionResource Needs(string name, int required, int provided = 0, string? symbol = null) =>
        new(name, required, provided) { Symbol = symbol ?? JournalJson.Symbol(name) };

    private static MarketSnapshot Market(
        string station,
        double x = 0,
        bool largePad = true,
        string? type = "Coriolis Starport",
        params (string Commodity, int Buy, int Supply)[] quotes) => new()
        {
            Station = station,
            System = station,
            X = x,
            Type = type,
            HasLargePad = largePad,
            UpdatedAt = DateTimeOffset.UnixEpoch,
            Quotes = quotes.ToDictionary(
                q => q.Commodity,
                q => new MarketQuote(q.Commodity) { BuyPrice = q.Buy, Supply = q.Supply },
                StringComparer.OrdinalIgnoreCase),
        };

    private static MarketSnapshot Origin() => Market("Home");

    // ---- The join ----------------------------------------------------------------------------

    /// <summary>
    /// The acceptance the plan calls blunt and not negotiable. The depot writes
    /// <c>$lowtemperaturediamond_name;</c> and the market writes <c>Low Temperature Diamonds</c>,
    /// and those have to meet.
    /// </summary>
    [Fact]
    public void ADepotSymbolMeetsTheMarketsDisplayName()
    {
        var plan = ColonisationSourcing.Plan(
            [Needs("Low Temperature Diamonds", 100, symbol: "lowtemperaturediamond")],
            [Market("Somewhere", quotes: [("Low Temperature Diamonds", 500, 1000)])],
            Origin());

        var stop = Assert.Single(plan.Stops);
        var lot = Assert.Single(stop.Lots);

        Assert.Equal(100, lot.Tonnes);
        Assert.Empty(plan.Unpriced);
        Assert.True(plan.IsComplete);
    }

    /// <summary>
    /// <b>Why the join is on the name and not the symbol</b>, asserted so nobody puts it back.
    /// <para>
    /// The first attempt at this folded the market's display name and compared it to the depot's
    /// symbol, and it found nothing. Elite's symbol is not derivable from the spelling: the fold
    /// lowercases and strips decoration, and it cannot remove the spaces or the plural. A lookup
    /// table would be needed to go one way, and there is no need for one — the depot writes
    /// <c>Name_Localised</c> on every row of all 120,208 measured, and that is what both market
    /// sources are keyed by.
    /// </para>
    /// </summary>
    [Fact]
    public void FoldingTheDisplayNameDoesNotProduceElitesSymbol()
    {
        Assert.NotEqual("lowtemperaturediamond", JournalJson.Symbol("Low Temperature Diamonds"));
        Assert.Equal("low temperature diamonds", JournalJson.Symbol("Low Temperature Diamonds"));
    }

    /// <summary>
    /// Phase 17 measured the fold going wrong in exactly this direction: the decoration stripped
    /// and the case left alone joined the depot to the hold perfectly and matched nothing else.
    /// </summary>
    [Fact]
    public void TheJoinIsCaseInsensitiveBecauseThatIsWhereItWentWrongBefore()
    {
        var plan = ColonisationSourcing.Plan(
            [Needs("Steel", 10, symbol: "steel")],
            [Market("Somewhere", quotes: [("STEEL", 500, 1000)])],
            Origin());

        Assert.Single(plan.Stops);
        Assert.Empty(plan.Unpriced);
    }

    /// <summary>
    /// <b>Nothing is dropped in silence.</b> A commodity no market could price is named in the
    /// answer, because a sourcing plan quietly missing one is a Commander's week.
    /// </summary>
    [Fact]
    public void ACommodityNobodyPricesIsNamedRatherThanOmitted()
    {
        var plan = ColonisationSourcing.Plan(
            [Needs("Steel", 10), Needs("Insulating Membrane", 5)],
            [Market("Somewhere", quotes: [("Steel", 500, 1000)])],
            Origin());

        Assert.Single(plan.Stops);
        Assert.Equal("Insulating Membrane", Assert.Single(plan.Unpriced));
        Assert.False(plan.IsComplete);
    }

    /// <summary>
    /// Found but short is a different failure from never found, and they are reported separately:
    /// "widen the search" is right for one and useless for the other.
    /// </summary>
    [Fact]
    public void FoundButShortIsADifferentAnswerFromNeverFound()
    {
        var plan = ColonisationSourcing.Plan(
            [Needs("Steel", 1000)],
            [Market("Somewhere", quotes: [("Steel", 500, 400)])],
            Origin());

        Assert.Empty(plan.Unpriced);
        Assert.Equal(600, plan.Shortfalls["Steel"]);
        Assert.False(plan.IsComplete);
    }

    // ---- The covering ------------------------------------------------------------------------

    /// <summary>
    /// The objective is trips, not credits. One station carrying all three beats two cheaper ones
    /// carrying the same three between them.
    /// </summary>
    [Fact]
    public void OneStationThatCarriesEverythingBeatsTwoThatDoNot()
    {
        var plan = ColonisationSourcing.Plan(
            [Needs("Steel", 10), Needs("Titanium", 10), Needs("Copper", 10)],
            [
                Market("Everything", quotes:
                    [("Steel", 900, 100), ("Titanium", 900, 100), ("Copper", 900, 100)]),
                Market("Cheap steel", quotes: [("Steel", 1, 100)]),
                Market("Cheap rest", quotes: [("Titanium", 1, 100), ("Copper", 1, 100)]),
            ],
            Origin());

        var stop = Assert.Single(plan.Stops);

        Assert.Equal("Everything", stop.Market.Station);
        Assert.Equal(3, stop.Covers);
        Assert.True(plan.IsComplete);
    }

    /// <summary>Where no one station covers it, the fewest that do.</summary>
    [Fact]
    public void WhereNoOneStationCoversItTheFewestThatDo()
    {
        var plan = ColonisationSourcing.Plan(
            [Needs("Steel", 10), Needs("Titanium", 10), Needs("Copper", 10)],
            [
                Market("Two of them", quotes: [("Steel", 500, 100), ("Titanium", 500, 100)]),
                Market("The third", quotes: [("Copper", 500, 100)]),
                Market("One of them", quotes: [("Steel", 400, 100)]),
            ],
            Origin());

        Assert.Equal(2, plan.Stops.Count);
        Assert.Equal("Two of them", plan.Stops[0].Market.Station);
        Assert.True(plan.IsComplete);
    }

    /// <summary>
    /// Two stations covering the same set are separated by what the trip actually costs, rather
    /// than by whichever the sweep happened to return first.
    /// </summary>
    [Fact]
    public void TwoStationsCoveringTheSameSetAreSeparatedByCost()
    {
        var plan = ColonisationSourcing.Plan(
            [Needs("Steel", 10)],
            [
                Market("Dear", quotes: [("Steel", 900, 100)]),
                Market("Cheap", quotes: [("Steel", 100, 100)]),
            ],
            Origin());

        Assert.Equal("Cheap", Assert.Single(plan.Stops).Market.Station);
    }

    /// <summary>And where cost ties too, the nearer one.</summary>
    [Fact]
    public void AndWhereCostTiesTheNearerOne()
    {
        var plan = ColonisationSourcing.Plan(
            [Needs("Steel", 10)],
            [
                Market("Far", x: 40, quotes: [("Steel", 500, 100)]),
                Market("Near", x: 2, quotes: [("Steel", 500, 100)]),
            ],
            Origin());

        Assert.Equal("Near", Assert.Single(plan.Stops).Market.Station);
    }

    /// <summary>
    /// The quantity is the site's remaining need rather than its requirement, and it is capped at
    /// what the station actually holds — a plan promising tonnage that is not there is the
    /// arithmetic lying quietly.
    /// </summary>
    [Fact]
    public void TheTonnageIsWhatIsLeftCappedAtWhatTheStationHas()
    {
        var plan = ColonisationSourcing.Plan(
            [Needs("Steel", required: 1000, provided: 900)],
            [Market("Somewhere", quotes: [("Steel", 500, 60)])],
            Origin());

        var lot = Assert.Single(Assert.Single(plan.Stops).Lots);

        Assert.Equal(60, lot.Tonnes);
        Assert.Equal(40, plan.Shortfalls["Steel"]);
    }

    /// <summary>A row already met is not part of the shopping list.</summary>
    [Fact]
    public void SomethingAlreadyDeliveredIsNotSourcedAgain()
    {
        var plan = ColonisationSourcing.Plan(
            [Needs("Steel", required: 100, provided: 100)],
            [Market("Somewhere", quotes: [("Steel", 500, 1000)])],
            Origin());

        Assert.Empty(plan.Stops);
        Assert.True(plan.IsComplete);
    }

    /// <summary>
    /// The carrier is out of the arithmetic entirely. Its prices are player-set, it moves, and
    /// what is on it is not derivable — the reconciliation came out wrong 679 times against right
    /// 347 and drove eleven commodities negative.
    /// </summary>
    [Fact]
    public void AFleetCarrierIsNeverAStop()
    {
        var plan = ColonisationSourcing.Plan(
            [Needs("Steel", 10)],
            [
                Market("Carrier", type: "Drake-Class Carrier", quotes: [("Steel", 1, 1000)]),
                Market("Station", quotes: [("Steel", 500, 1000)]),
            ],
            Origin());

        Assert.Equal("Station", Assert.Single(plan.Stops).Market.Station);
    }

    [Fact]
    public void ALargePadFilterDropsTheOutposts()
    {
        var plan = ColonisationSourcing.Plan(
            [Needs("Steel", 10)],
            [
                Market("Outpost", largePad: false, quotes: [("Steel", 1, 1000)]),
                Market("Starport", quotes: [("Steel", 500, 1000)]),
            ],
            Origin(),
            largePadOnly: true);

        Assert.Equal("Starport", Assert.Single(plan.Stops).Market.Station);
    }

    /// <summary>
    /// A station that sells it and holds none of it is not a source. Elite writes zero supply for
    /// a commodity a station lists but has run out of.
    /// </summary>
    [Fact]
    public void AStationHoldingNoneOfItIsNotASource()
    {
        var plan = ColonisationSourcing.Plan(
            [Needs("Steel", 10)],
            [Market("Empty", quotes: [("Steel", 500, 0)])],
            Origin());

        Assert.Empty(plan.Stops);
        Assert.Equal("Steel", Assert.Single(plan.Unpriced));
    }

    /// <summary>
    /// The ceiling exists so an answer stays one a Commander can act on rather than a tour of the
    /// bubble, and what it could not fit is still reported rather than dropped.
    /// </summary>
    [Fact]
    public void TheStopCeilingIsHonouredAndWhatItCouldNotFitIsStillReported()
    {
        var needs = Enumerable.Range(0, 6).Select(index => Needs($"Thing {index}", 10)).ToArray();

        var markets = Enumerable.Range(0, 6)
            .Select(index => Market($"Station {index}", quotes: [($"Thing {index}", 500, 100)]))
            .ToArray();

        var plan = ColonisationSourcing.Plan(needs, markets, Origin(), maxStops: 2);

        Assert.Equal(2, plan.Stops.Count);
        Assert.Equal(4, plan.Shortfalls.Count);
        Assert.False(plan.IsComplete);
    }

    /// <summary>Nothing outstanding is a finished build rather than an empty answer to explain.</summary>
    [Fact]
    public void AFinishedBuildNeedsNothing()
    {
        var plan = ColonisationSourcing.Plan([], [Market("Somewhere", quotes: [("Steel", 500, 1000)])], Origin());

        Assert.Empty(plan.Stops);
        Assert.True(plan.IsComplete);
    }

    /// <summary>
    /// A depot row that somehow carries no symbol falls back to folding its own name, so the join
    /// still happens rather than the commodity vanishing.
    /// </summary>
    [Fact]
    public void ARowWithNoSymbolStillJoinsOnItsFoldedName()
    {
        var plan = ColonisationSourcing.Plan(
            [new ConstructionResource("Steel", 10, 0)],
            [Market("Somewhere", quotes: [("Steel", 500, 1000)])],
            Origin());

        Assert.Single(plan.Stops);
        Assert.Empty(plan.Unpriced);
    }
}
