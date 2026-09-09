namespace D47.Core.Knowledge;

/// <summary>d47's own trade route arithmetic (Phase 36).</summary>
public static class TradePlanner
{
    /// <summary>How many partial routes survive each hop.</summary>
    public const int Beam = 200;

    /// <summary>How many different commodities may be loaded on one leg.</summary>
    public const int LoadsPerLeg = 2;

    /// <summary>How many commodities are ranked before those loads are chosen.</summary>
    private const int Considered = 6;

    /// <summary>
    /// Plans a route, or answers null where the origin's own market is not among the snapshots — which
    /// is "I cannot see the board you are standing in front of", a different answer from a route with
    /// no stops.
    /// </summary>
    public static TradeRoute? Plan(TradeQuery query, IReadOnlyList<MarketSnapshot> markets)
    {
        var carriers = 0;
        var usable = new List<MarketSnapshot>();

        foreach (var market in markets)
        {
            if (market.IsCarrier)
            {
                // Counted rather than silently dropped: a Commander who is told forty carriers were left out
                // knows why d47's best price is not the best price on the board.
                carriers++;
                continue;
            }

            usable.Add(market);
        }

        var found = usable.FindIndex(market => market.IsSamePlaceAs(query.Station, query.System));

        if (found < 0)
        {
            return null;
        }

        // The origin survives every filter below.
        var candidates = new List<MarketSnapshot>();
        var origin = 0;

        for (var index = 0; index < usable.Count; index++)
        {
            var market = usable[index];

            if (index == found)
            {
                origin = candidates.Count;
                candidates.Add(market);
                continue;
            }

            if (query.LargePadOnly && !market.HasLargePad)
            {
                continue;
            }

            if (market.DistanceToArrival is { } arrival && arrival > query.MaxSystemDistance)
            {
                continue;
            }

            candidates.Add(market);
        }

        var board = new Board(candidates, query);
        var route = Search(query, board, origin);

        return route with
        {
            Capital = query.Capital,
            Loop = query.Loop,
            MarketsConsidered = candidates.Count,
            CarriersIgnored = carriers,
            MarketsSeenInPerson = candidates.Count(market => market.Source == PriceSource.Seen),
        };
    }

    /// <summary>The markets as dense arrays rather than as records.</summary>
    private sealed class Board
    {
        public Board(IReadOnlyList<MarketSnapshot> markets, TradeQuery query)
        {
            Markets = markets;

            var ids = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var market in markets)
            {
                foreach (var quote in market.Quotes.Values)
                {
                    // Rares are priced per station, capped at a few tonnes by the game and worth less the
                    // closer they are sold to home.
                    if (quote.IsRare)
                    {
                        continue;
                    }

                    if (!ids.ContainsKey(quote.Commodity))
                    {
                        ids[quote.Commodity] = ids.Count;
                    }
                }
            }

            Commodities = new string[ids.Count];

            foreach (var (name, id) in ids)
            {
                Commodities[id] = name;
            }

            var stations = markets.Count;

            Buy = new int[stations][];
            Sell = new int[stations][];
            Supply = new int[stations][];
            Demand = new int[stations][];
            Stock = new int[stations][];

            Ceiling = new int[ids.Count];
            CeilingDemand = new int[ids.Count];

            for (var station = 0; station < stations; station++)
            {
                var buy = new int[ids.Count];
                var sell = new int[ids.Count];
                var supply = new int[ids.Count];
                var demand = new int[ids.Count];
                var stock = new List<int>();

                foreach (var quote in markets[station].Quotes.Values)
                {
                    if (quote.IsRare || !ids.TryGetValue(quote.Commodity, out var id))
                    {
                        continue;
                    }

                    buy[id] = quote.BuyPrice;
                    sell[id] = quote.SellPrice;
                    supply[id] = quote.Supply;
                    demand[id] = quote.Demand;

                    if (quote.BuyPrice > 0 && quote.Supply > 0)
                    {
                        stock.Add(id);
                    }

                    if (quote.SellPrice > Ceiling[id] && quote.Demand > 0)
                    {
                        Ceiling[id] = quote.SellPrice;
                        CeilingDemand[id] = quote.Demand;
                    }
                }

                Buy[station] = buy;
                Sell[station] = sell;
                Supply[station] = supply;
                Demand[station] = demand;
                Stock[station] = [.. stock];
            }

            Reachable = new int[stations][];
            Legs = new double[stations][];

            for (var station = 0; station < stations; station++)
            {
                var reachable = new List<int>();
                var legs = new List<double>();

                for (var other = 0; other < stations; other++)
                {
                    if (other == station)
                    {
                        continue;
                    }

                    var distance = markets[station].DistanceTo(markets[other]);

                    if (distance > query.MaxHopDistance)
                    {
                        continue;
                    }

                    reachable.Add(other);
                    legs.Add(distance);
                }

                Reachable[station] = [.. reachable];
                Legs[station] = [.. legs];
            }
        }

        public IReadOnlyList<MarketSnapshot> Markets { get; }

        public string[] Commodities { get; }

        public int[][] Buy { get; }

        public int[][] Sell { get; }

        public int[][] Supply { get; }

        public int[][] Demand { get; }

        /// <summary>Which commodities are worth buying at a station: priced, and in stock.</summary>
        public int[][] Stock { get; }

        /// <summary>Which stations are one leg away.</summary>
        public int[][] Reachable { get; }

        /// <summary>How far each of those is, in light years, aligned with <see cref="Reachable"/>.</summary>
        public double[][] Legs { get; }

        /// <summary>
        /// The best price anywhere on the board for each commodity, and the demand that goes with it.
        /// </summary>
        public int[] Ceiling { get; }

        public int[] CeilingDemand { get; }

        /// <summary>What a station will actually pay, which is zero where it wants none.</summary>
        public int Pays(int station, int commodity) =>
            Demand[station][commodity] > 0 ? Sell[station][commodity] : 0;
    }

    private readonly record struct Lot(int Commodity, int Amount, int UnitPrice);

    private sealed class Node
    {
        public Node? Previous { get; init; }

        public int Station { get; init; }

        public long Credits { get; init; }

        /// <summary>What is aboard on arrival here.</summary>
        public Lot[] Cargo { get; init; } = [];

        /// <summary>Sold at the previous station before leaving it.</summary>
        public Lot[] Sold { get; init; } = [];

        /// <summary>Kept aboard past the previous station, at what it would have paid.</summary>
        public Lot[] Held { get; init; } = [];

        public Lot[] Bought { get; init; } = [];

        /// <summary>Light years flown to get here.</summary>
        public double Distance { get; init; }

        /// <summary>Whether a lot leaving the previous station was cut short by demand.</summary>
        public bool Capped { get; init; }

        /// <summary>Credits plus what the hold would fetch here.</summary>
        public long Score { get; init; }
    }

    private static TradeRoute Search(TradeQuery query, Board board, int origin)
    {
        var start = new Node
        {
            Station = origin,
            Credits = query.Capital,
            Score = query.Capital,
        };

        var frontier = new List<Node> { start };

        Node? best = null;
        var bestCredits = query.Capital;

        for (var hop = 1; hop <= query.MaxHops && frontier.Count > 0; hop++)
        {
            var next = new List<Node>(frontier.Count * 4);

            foreach (var node in frontier)
            {
                var reachable = board.Reachable[node.Station];
                var legs = board.Legs[node.Station];

                for (var index = 0; index < reachable.Length; index++)
                {
                    var destination = reachable[index];
                    var closing = destination == origin;

                    if (closing && !query.Loop)
                    {
                        continue;
                    }

                    // The last hop of a loop has to be the one that closes it.
                    if (query.Loop && !closing && hop == query.MaxHops)
                    {
                        continue;
                    }

                    if (!closing && Visited(node, destination))
                    {
                        continue;
                    }

                    // With an empty hold both sell variants are the same route, and running the second
                    // doubles the search for nothing.
                    var variants = node.Cargo.Length == 0 ? 1 : 2;

                    for (var variant = 0; variant < variants; variant++)
                    {
                        var successor = Step(query, board, node, destination, legs[index], variant, closing);

                        if (successor is null)
                        {
                            continue;
                        }

                        // A loop's closing hop is worth taking at any depth: a four-hop loop that pays better
                        // than the ten-hop one asked for is a better answer, not a shortfall.
                        if (successor.Score > bestCredits && (!query.Loop || closing))
                        {
                            best = successor;
                            bestCredits = successor.Score;
                        }

                        if (!closing)
                        {
                            next.Add(successor);
                        }
                    }
                }
            }

            next.Sort(static (left, right) => right.Score.CompareTo(left.Score));

            frontier = next.Count > Beam ? next.GetRange(0, Beam) : next;
        }

        return best is null ? new TradeRoute([]) : Reconstruct(board, best, query.Capital);
    }

    private static bool Visited(Node node, int station)
    {
        for (var walk = node; walk is not null; walk = walk.Previous)
        {
            if (walk.Station == station)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>One leg: sell what is worth selling here, fill the hold, and fly.</summary>
    /// <param name="variant">
    /// 0 sells everything this station pays for, which is what every planner does. 1 keeps any lot the
    /// destination pays more for, which is the phase.
    /// </param>
    /// <param name="closing">Whether this leg closes a loop.</param>
    private static Node? Step(
        TradeQuery query,
        Board board,
        Node node,
        int destination,
        double distance,
        int variant,
        bool closing)
    {
        var here = node.Station;
        var credits = node.Credits;
        var sold = new List<Lot>();
        var held = new List<Lot>();
        var capped = false;
        var kept = false;

        foreach (var lot in node.Cargo)
        {
            var pays = board.Pays(here, lot.Commodity);
            var elsewhere = board.Pays(destination, lot.Commodity);

            if (variant == 1 && elsewhere > pays)
            {
                held.Add(lot with { UnitPrice = pays });
                kept = true;
                continue;
            }

            if (pays <= 0)
            {
                // Nowhere to sell it here at any price.
                held.Add(lot with { UnitPrice = 0 });
                continue;
            }

            var amount = Math.Min(lot.Amount, board.Demand[here][lot.Commodity]);

            if (amount < lot.Amount)
            {
                capped = true;
                held.Add(new Lot(lot.Commodity, lot.Amount - amount, pays));
            }

            if (amount <= 0)
            {
                continue;
            }

            sold.Add(new Lot(lot.Commodity, amount, pays));
            credits += (long)amount * pays;
        }

        // The retention variant that retained nothing is the ordinary one, arrived at twice.
        if (variant == 1 && !kept)
        {
            return null;
        }

        var space = query.CargoCapacity - held.Sum(lot => lot.Amount);

        var bought = space > 0 && credits > 0
            ? Load(board, here, destination, closing, space, ref credits, ref capped)
            : [];

        // A leg with an empty ship that sells nothing and buys nothing is not a plan, and it is dropped
        // rather than ranked: it would otherwise fill the beam with routes indistinguishable from each other
        // and from doing nothing. A leg that carries cargo and does nothing else is a different thing
        // entirely — it is the phase — so the test is on the hold rather than on the trading.
        if (sold.Count == 0 && bought.Count == 0 && held.Count == 0)
        {
            return null;
        }

        var cargo = new List<Lot>(held.Count + bought.Count);
        cargo.AddRange(held);
        cargo.AddRange(bought);

        var arrived = Merge(cargo);

        return new Node
        {
            Previous = node,
            Station = destination,
            Credits = credits,
            Cargo = arrived,
            Sold = [.. sold],
            Held = [.. held],
            Bought = [.. bought],
            Distance = distance,
            Capped = capped,
            Score = credits + Liquidation(board, destination, arrived),
        };
    }

    /// <summary>What to fill the hold with, given where it is going next.</summary>
    private static List<Lot> Load(
        Board board,
        int here,
        int destination,
        bool closing,
        int space,
        ref long credits,
        ref bool capped)
    {
        Span<int> ranked = stackalloc int[Considered];
        Span<int> margins = stackalloc int[Considered];
        var found = 0;

        foreach (var commodity in board.Stock[here])
        {
            var cost = board.Buy[here][commodity];
            var pays = board.Pays(destination, commodity);
            var target = closing ? pays : Math.Max(pays, board.Ceiling[commodity]);
            var margin = target - cost;

            if (margin <= 0 || cost > credits)
            {
                continue;
            }

            var slot = found < Considered ? found++ : -1;

            if (slot < 0)
            {
                var worst = 0;

                for (var index = 1; index < Considered; index++)
                {
                    if (margins[index] < margins[worst])
                    {
                        worst = index;
                    }
                }

                if (margins[worst] >= margin)
                {
                    continue;
                }

                slot = worst;
            }

            ranked[slot] = commodity;
            margins[slot] = margin;
        }

        var loads = new List<Lot>(LoadsPerLeg);

        for (var taken = 0; taken < LoadsPerLeg && space > 0 && credits > 0; taken++)
        {
            var pick = -1;

            for (var index = 0; index < found; index++)
            {
                if (margins[index] > 0 && (pick < 0 || margins[index] > margins[pick]))
                {
                    pick = index;
                }
            }

            if (pick < 0)
            {
                break;
            }

            var commodity = ranked[pick];
            margins[pick] = 0;

            var cost = board.Buy[here][commodity];
            var pays = board.Pays(destination, commodity);

            // Never more than somebody actually wants.
            var wanted = !closing && board.Ceiling[commodity] > pays
                ? Math.Max(board.Demand[destination][commodity], board.CeilingDemand[commodity])
                : board.Demand[destination][commodity];

            var affordable = (int)Math.Min(credits / cost, int.MaxValue);
            var room = Math.Min(space, Math.Min(affordable, board.Supply[here][commodity]));
            var amount = Math.Min(room, wanted);

            if (amount <= 0)
            {
                continue;
            }

            if (wanted < room)
            {
                capped = true;
            }

            loads.Add(new Lot(commodity, amount, cost));
            credits -= (long)amount * cost;
            space -= amount;
        }

        return loads;
    }

    /// <summary>Two loads of the same commodity are one stack in the hold, priced at what it cost.</summary>
    private static Lot[] Merge(List<Lot> lots)
    {
        if (lots.Count < 2)
        {
            return [.. lots];
        }

        var merged = new List<Lot>(lots.Count);

        foreach (var lot in lots)
        {
            if (lot.Amount <= 0)
            {
                continue;
            }

            var index = merged.FindIndex(existing => existing.Commodity == lot.Commodity);

            if (index < 0)
            {
                merged.Add(lot);
                continue;
            }

            var running = merged[index];
            var amount = running.Amount + lot.Amount;

            merged[index] = new Lot(
                lot.Commodity,
                amount,
                (int)((((long)running.Amount * running.UnitPrice) + ((long)lot.Amount * lot.UnitPrice)) / amount));
        }

        return [.. merged];
    }

    /// <summary>What the hold would fetch here and now, capped at what the station wants.</summary>
    private static long Liquidation(Board board, int station, Lot[] cargo)
    {
        var total = 0L;

        foreach (var lot in cargo)
        {
            var pays = board.Pays(station, lot.Commodity);

            if (pays <= 0)
            {
                continue;
            }

            total += (long)Math.Min(lot.Amount, board.Demand[station][lot.Commodity]) * pays;
        }

        return total;
    }

    private static TradeRoute Reconstruct(Board board, Node last, long capital)
    {
        var chain = new List<Node>();

        for (var walk = last; walk is not null; walk = walk.Previous)
        {
            chain.Add(walk);
        }

        chain.Reverse();

        var stops = new List<TradeStop>(chain.Count);

        for (var index = 0; index < chain.Count; index++)
        {
            var node = chain[index];
            var market = board.Markets[node.Station];
            var departure = index + 1 < chain.Count ? chain[index + 1] : null;

            stops.Add(new TradeStop(market.System, market.Station)
            {
                Distance = index == 0 ? null : node.Distance,
                DistanceToArrival = market.DistanceToArrival,
                Sell = departure is null ? Final(board, node) : Name(board, departure.Sold),
                Hold = departure is null
                    ? Name(board, Leftover(board, node))
                    : Name(board, departure.Held),
                Buy = departure is null ? [] : Name(board, departure.Bought),
                Credits = departure?.Credits ?? node.Credits + Liquidation(board, node.Station, node.Cargo),
                PricesSeen = market.UpdatedAt,
                PricesAreYours = market.Source == PriceSource.Seen,
                CappedByDemand = departure?.Capped ?? false,
            });
        }

        return new TradeRoute(stops) { TotalProfit = stops[^1].Credits - capital };
    }

    /// <summary>Everything the last station will take, which is where the route's money is realised.</summary>
    private static IReadOnlyList<TradeLot> Final(Board board, Node node)
    {
        var lots = new List<TradeLot>();

        foreach (var lot in node.Cargo)
        {
            var pays = board.Pays(node.Station, lot.Commodity);
            var amount = pays > 0 ? Math.Min(lot.Amount, board.Demand[node.Station][lot.Commodity]) : 0;

            if (amount > 0)
            {
                lots.Add(new TradeLot(board.Commodities[lot.Commodity], amount, pays));
            }
        }

        return lots;
    }

    /// <summary>What is still aboard when the route ends.</summary>
    private static Lot[] Leftover(Board board, Node node)
    {
        var lots = new List<Lot>();

        foreach (var lot in node.Cargo)
        {
            var pays = board.Pays(node.Station, lot.Commodity);
            var sold = pays > 0 ? Math.Min(lot.Amount, board.Demand[node.Station][lot.Commodity]) : 0;

            if (lot.Amount > sold)
            {
                lots.Add(new Lot(lot.Commodity, lot.Amount - sold, 0));
            }
        }

        return [.. lots];
    }

    private static IReadOnlyList<TradeLot> Name(Board board, IReadOnlyList<Lot> lots)
    {
        if (lots.Count == 0)
        {
            return [];
        }

        var named = new List<TradeLot>(lots.Count);

        foreach (var lot in lots)
        {
            named.Add(new TradeLot(board.Commodities[lot.Commodity], lot.Amount, lot.UnitPrice));
        }

        return named;
    }
}
