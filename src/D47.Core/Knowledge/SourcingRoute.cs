namespace D47.Core.Knowledge;

/// <summary>Where a build's commodities are handed in.</summary>
/// <param name="Station">The station's name.</param>
/// <param name="System">The system it is in.</param>
/// <param name="Position">Galactic coordinates of the system, or null when they are not known.</param>
public sealed record HandIn(string Station, string System, (double X, double Y, double Z)? Position);

/// <summary>One stop in flying order.</summary>
/// <param name="Station">The station's name.</param>
/// <param name="System">The system it is in.</param>
/// <param name="IsHandIn">Whether the commodities are handed in here rather than bought.</param>
/// <param name="Distance">Straight-line light years from the previous measurable place, or null when unknown.</param>
public sealed record RouteLeg(string Station, string System, bool IsHandIn, double? Distance);

/// <summary>Puts sourcing stops and hand-in stations into flying order.</summary>
public static class SourcingRoute
{
    /// <summary>The most places searched exhaustively; above it the route is built nearest-first.</summary>
    public const int ExactLimit = 8;

    private readonly record struct Place(string Station, string System, bool IsHandIn, (double X, double Y, double Z)? Position);

    /// <summary>
    /// Every buy comes before every hand-in. Places without coordinates follow the measurable
    /// ones of their kind, in the order given, with an unknown distance.
    /// </summary>
    public static IReadOnlyList<RouteLeg> Order(
        (double X, double Y, double Z)? start,
        IReadOnlyList<SourcingStop> buys,
        IReadOnlyList<HandIn> handIns)
    {
        var places = buys
            .Select(stop => new Place(stop.Market.Station, stop.Market.System, false, (stop.Market.X, stop.Market.Y, stop.Market.Z)))
            .Concat(handIns.Select(handIn => new Place(handIn.Station, handIn.System, true, handIn.Position)))
            .ToArray();

        var known = places.Where(place => place.Position is not null).ToArray();
        var sequence = places.Length <= ExactLimit ? Exact(start, known) : Nearest(start, known);

        var ordered = sequence.Where(place => !place.IsHandIn)
            .Concat(places.Where(place => place.Position is null && !place.IsHandIn))
            .Concat(sequence.Where(place => place.IsHandIn))
            .Concat(places.Where(place => place.Position is null && place.IsHandIn))
            .ToArray();

        var legs = new List<RouteLeg>(ordered.Length);
        var at = start;

        foreach (var place in ordered)
        {
            double? distance = null;

            if (place.Position is { } position)
            {
                distance = at is { } from ? Between(from, position) : 0;
                at = position;
            }

            legs.Add(new RouteLeg(place.Station, place.System, place.IsHandIn, distance));
        }

        return legs;
    }

    private static Place[] Exact((double X, double Y, double Z)? start, Place[] known)
    {
        var best = known;
        var bestCost = double.PositiveInfinity;
        var chosen = new Place[known.Length];
        var used = new bool[known.Length];
        var buysLeft = known.Count(place => !place.IsHandIn);

        Search(0, start, 0, buysLeft);

        return best;

        void Search(int depth, (double X, double Y, double Z)? at, double cost, int buysRemaining)
        {
            if (cost >= bestCost)
            {
                return;
            }

            if (depth == known.Length)
            {
                bestCost = cost;
                best = (Place[])chosen.Clone();

                return;
            }

            for (var i = 0; i < known.Length; i++)
            {
                if (used[i] || (known[i].IsHandIn && buysRemaining > 0))
                {
                    continue;
                }

                var position = known[i].Position!.Value;
                used[i] = true;
                chosen[depth] = known[i];
                Search(depth + 1, position, cost + (at is { } from ? Between(from, position) : 0), buysRemaining - (known[i].IsHandIn ? 0 : 1));
                used[i] = false;
            }
        }
    }

    private static Place[] Nearest((double X, double Y, double Z)? start, Place[] known)
    {
        var remaining = known.ToList();
        var result = new List<Place>(known.Length);
        var at = start;

        while (remaining.Count > 0)
        {
            var pool = remaining.Any(place => !place.IsHandIn) ? remaining.Where(place => !place.IsHandIn).ToArray() : remaining.ToArray();
            var next = at is { } from
                ? pool.MinBy(place => Between(from, place.Position!.Value))
                : pool[0];

            result.Add(next);
            remaining.Remove(next);
            at = next.Position;
        }

        return [.. result];
    }

    private static double Between((double X, double Y, double Z) a, (double X, double Y, double Z) b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        var dz = a.Z - b.Z;

        return Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }
}
