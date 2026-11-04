using System.Globalization;
using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>Facts about the system the Commander is in, offered to idle chatter as something to talk about.</summary>
internal static class SystemTopics
{
    private static readonly bool[] OneInThree = [false, false, true];

    internal const int OneInThreeOffset = 41;
    internal const int TopicOffset = 47;
    internal const int ShipRemarkOffset = 53;

    /// <summary>Every topic the journal location supports; a null or <c>None</c> field gives none.</summary>
    internal static List<string> Known(JournalLocation? place)
    {
        List<string> known = [];

        if (place?.SystemEconomy is { Length: > 0 } system)
        {
            known.Add($"how the {system} trade here is going");
        }

        if (place?.StationEconomy is { Length: > 0 } station && station != place.SystemEconomy)
        {
            known.Add($"how the {station} trade at this station is going");
        }

        if (place?.SystemFactionState is { Length: > 0 } state)
        {
            known.Add($"living with the {state} the local faction is in");
        }

        if (place?.Population is > 0 and var population)
        {
            known.Add($"living in {Describe(population)}");
        }

        if (place?.ControllingPower is { Length: > 0 } power && !IsNone(power))
        {
            known.Add($"living under {power}");
        }

        if (place?.PowerplayState is { Length: > 0 } powerplay && !IsNone(powerplay))
        {
            known.Add(string.Equals(powerplay, "Unoccupied", StringComparison.OrdinalIgnoreCase)
                ? "being in an Unoccupied system nobody has claimed"
                : $"being in a {powerplay} system");
        }

        return known;
    }

    /// <summary>One topic for the exchange at <paramref name="index"/>, on one index in three; null otherwise.</summary>
    internal static string? For(int index, int oneInThreeOffset, int topicOffset, JournalLocation? place)
    {
        var known = Known(place);

        return known.Count == 0 || !NpcChatter.Pick(index, oneInThreeOffset, OneInThree)
            ? null
            : NpcChatter.Pick(index, topicOffset, known.ToArray());
    }

    internal static string Describe(long population) => population switch
    {
        < 1_000 => "a small colony of a few hundred",
        < 10_000 => "a small colony of a few thousand",
        < 1_000_000 => $"a system of about {Round(population / 1_000.0)} thousand people",
        < 1_000_000_000 => $"a system of about {Round(population / 1_000_000.0)} million people",
        _ => $"a system of about {Round(population / 1_000_000_000.0)} billion people",
    };

    private static string Round(double value) =>
        value >= 10
            ? Math.Round(value).ToString("0", CultureInfo.InvariantCulture)
            : Math.Round(value, 1).ToString("0.#", CultureInfo.InvariantCulture);

    private static bool IsNone(string value) => string.Equals(value, "None", StringComparison.OrdinalIgnoreCase);
}
