using D47.Core.Journal;

namespace D47.Core.Campaigns;

/// <summary>The hub of the first rows of the Campaign page's Systems table.</summary>
public static class CampaignHub
{
    /// <summary>How many rows of the table the hub is taken over.</summary>
    public const int Span = 10;

    /// <summary>
    /// The index in <paramref name="positions"/> of the position with the smallest total distance to the other positions,
    /// the earlier one on a tie; null entries are left out, and null is returned when fewer than two entries have a position.
    /// </summary>
    public static int? IndexOf(IReadOnlyList<StarPosition?> positions)
    {
        ArgumentNullException.ThrowIfNull(positions);

        var placed = Enumerable.Range(0, positions.Count).Where(index => positions[index] is not null).ToList();

        if (placed.Count < 2)
        {
            return null;
        }

        int? hub = null;
        var least = double.PositiveInfinity;

        foreach (var index in placed)
        {
            var from = positions[index]!.Value;
            var total = placed.Where(other => other != index).Sum(other => from.DistanceTo(positions[other]!.Value));

            if (total < least)
            {
                least = total;
                hub = index;
            }
        }

        return hub;
    }
}
