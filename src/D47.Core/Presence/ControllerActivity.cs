using D47.Core.Hotas;

namespace D47.Core.Presence;

/// <summary>
/// Whether any controller was pressed or had a hat moved since the readings before: a rise in a button's running
/// press count, or a changed hat value. A device's first reading is its baseline, and a device missing from a
/// reading keeps the last one seen.
/// </summary>
public sealed class ControllerActivity
{
    private readonly Dictionary<string, (int[] Presses, int[] Hats)> _last = new(StringComparer.Ordinal);

    public bool Moved(IReadOnlyList<HotasReading> readings)
    {
        var moved = false;

        foreach (var reading in readings)
        {
            int[] presses = [.. reading.Presses];
            int[] hats = [.. reading.Hats];

            if (_last.TryGetValue(reading.Id, out var last))
            {
                moved |= Rose(last.Presses, presses) || Changed(last.Hats, hats);
            }

            _last[reading.Id] = (presses, hats);
        }

        return moved;
    }

    private static bool Rose(int[] before, int[] after)
    {
        for (var index = 0; index < Math.Min(before.Length, after.Length); index++)
        {
            if (after[index] > before[index])
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Hats compared by index; a device whose hat count changed reads as a new baseline.</summary>
    private static bool Changed(int[] before, int[] after) =>
        before.Length == after.Length && !before.AsSpan().SequenceEqual(after);
}
