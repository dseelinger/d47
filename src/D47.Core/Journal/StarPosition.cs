using System.Text.Json;

namespace D47.Core.Journal;

/// <summary>
/// A point in the galaxy, in light years on Frontier's own axes — the numbers Elite writes as
/// <c>StarPos</c> (Phase 28, "Where every engineer is").
/// <para>
/// <b>One distance formula in Core, and this is it.</b> <see cref="Knowledge.IGalaxyService"/> answers the
/// same question correctly over the network and is the wrong shape for a page that re-ranks
/// thirty-eight engineers every time a plan moves; a service call cannot be made in flight and
/// cannot be made at all offline. So a coordinate that is already known is arithmetic, and only a
/// coordinate that has to be looked up is a call.
/// </para>
/// <para>
/// A struct, and a small one, because these are held per engineer and per route hop and compared
/// far more often than they are created.
/// </para>
/// </summary>
public readonly record struct StarPosition(double X, double Y, double Z)
{
    /// <summary>
    /// Sol, which is where Elite's axes are measured from. Named rather than written as three
    /// zeroes at the call site, because "distance from the origin" and "distance from Sol" are
    /// the same number and only one of them means anything to a Commander.
    /// </summary>
    public static readonly StarPosition Origin = new(0, 0, 0);

    /// <summary>Light years to another point. Euclidean, which is what Elite's axes are.</summary>
    public double DistanceTo(StarPosition other) => Between(
        (X, Y, Z), (other.X, other.Y, other.Z));

    /// <summary>
    /// The distance between two raw triples, for the callers that hold coordinates as tuples —
    /// <see cref="RouteHop"/> has since Phase 8. Shared so there is one square root rather than
    /// two that can drift.
    /// </summary>
    public static double Between((double X, double Y, double Z) from, (double X, double Y, double Z) to)
    {
        var dx = from.X - to.X;
        var dy = from.Y - to.Y;
        var dz = from.Z - to.Z;

        return Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }

    /// <summary>
    /// The <c>StarPos</c> of an event that carries one, or null.
    /// <para>
    /// <b>All three axes or nothing.</b> A coordinate missing one axis would produce a confident
    /// wrong distance, which is worse than no distance at all — the same rule <c>NavRoute</c>
    /// applies to the route file, and for the same reason: everything downstream of a position is
    /// a number somebody acts on.
    /// </para>
    /// </summary>
    public static StarPosition? Read(JsonElement element)
    {
        if (!element.TryGetProperty("StarPos", out var position) ||
            position.ValueKind != JsonValueKind.Array ||
            position.GetArrayLength() != 3)
        {
            return null;
        }

        var axes = new double[3];

        for (var index = 0; index < 3; index++)
        {
            var axis = position[index];

            if (axis.ValueKind != JsonValueKind.Number || !axis.TryGetDouble(out axes[index]))
            {
                return null;
            }
        }

        return new StarPosition(axes[0], axes[1], axes[2]);
    }

    public override string ToString() =>
        $"{X.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}, "
        + $"{Y.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}, "
        + $"{Z.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}";
}
