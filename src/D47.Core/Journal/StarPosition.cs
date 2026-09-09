using System.Text.Json;

namespace D47.Core.Journal;

/// <summary>
/// A point in the galaxy, in light years on Frontier's own axes — the numbers Elite writes as
/// <c>StarPos</c> (Phase 28, "Where every engineer is").
/// </summary>
public readonly record struct StarPosition(double X, double Y, double Z)
{
    /// <summary>Sol, which is where Elite's axes are measured from.</summary>
    public static readonly StarPosition Origin = new(0, 0, 0);

    /// <summary>Light years to another point.</summary>
    public double DistanceTo(StarPosition other) => Between(
        (X, Y, Z), (other.X, other.Y, other.Z));

    /// <summary>
    /// The distance between two raw triples, for the callers that hold coordinates as tuples — <see
    /// cref="RouteHop"/> has since Phase 8.
    /// </summary>
    public static double Between((double X, double Y, double Z) from, (double X, double Y, double Z) to)
    {
        var dx = from.X - to.X;
        var dy = from.Y - to.Y;
        var dz = from.Z - to.Z;

        return Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }

    /// <summary>The <c>StarPos</c> of an event that carries one, or null.</summary>
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
