using System.Globalization;

namespace D47.Core.Loadout;

/// <summary>List operations shared by the ship and on-foot build records and plan services.</summary>
internal static class PlanLists
{
    /// <summary>The slots with <paramref name="plan"/> in place of the one for the same slot, or added at the end.</summary>
    public static List<T> WithSlot<T>(IReadOnlyList<T> slots, T plan, Func<T, string> slotOf)
    {
        var result = slots.ToList();
        var at = result.FindIndex(existing =>
            string.Equals(slotOf(existing), slotOf(plan), StringComparison.OrdinalIgnoreCase));

        if (at >= 0)
        {
            result[at] = plan;
        }
        else
        {
            result.Add(plan);
        }

        return result;
    }

    public static List<T> WithoutSlot<T>(IReadOnlyList<T> slots, string slot, Func<T, string> slotOf) =>
        [.. slots.Where(plan => !string.Equals(slotOf(plan), slot, StringComparison.OrdinalIgnoreCase))];

    /// <summary>The lowest <c>prefix</c>-number no id uses.</summary>
    public static string NextNumberedId(IEnumerable<string> ids, string prefix)
    {
        var taken = new HashSet<int>();

        foreach (var id in ids)
        {
            if (id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(id[prefix.Length..], CultureInfo.InvariantCulture, out var number))
            {
                taken.Add(number);
            }
        }

        var next = 1;

        while (taken.Contains(next))
        {
            next++;
        }

        return prefix + next.ToString(CultureInfo.InvariantCulture);
    }

    public static IReadOnlyList<T> Replacing<T>(IEnumerable<T> builds, T build, Func<T, string> idOf) =>
        [.. builds.Select(other => idOf(other) == idOf(build) ? build : other)];
}
