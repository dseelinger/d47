namespace D47.Core.Adventures;

/// <summary>Adventure keys no other adventure uses.</summary>
public static class AdventureKeys
{
    /// <summary>The first of <paramref name="wanted"/>, <c>wanted-2</c>, <c>wanted-3</c>… for which <paramref name="taken"/> is false; <paramref name="fallback"/> replaces a blank <paramref name="wanted"/>.</summary>
    public static string Unique(string wanted, string fallback, Func<string, bool> taken)
    {
        ArgumentNullException.ThrowIfNull(taken);

        var key = string.IsNullOrWhiteSpace(wanted) ? fallback : wanted;
        var candidate = key;
        var suffix = 2;

        while (taken(candidate))
        {
            candidate = $"{key}-{suffix++}";
        }

        return candidate;
    }
}
