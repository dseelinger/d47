using System.Text.Json;
using System.Text.RegularExpressions;

namespace D47.Core.Journal;

/// <summary>The Commander's career statistics, from the last <c>Statistics</c> event.</summary>
public sealed partial record CareerStatistics
{
    public static readonly CareerStatistics Empty = new();

    /// <summary>When the last <c>Statistics</c> event was written.</summary>
    public DateTimeOffset? TakenAt { get; init; }

    public bool IsKnown => TakenAt is not null;

    private JsonElement? Raw { get; init; }

    /// <summary>The number at a <c>Section.Key</c> path, or null where there is none.</summary>
    public double? Read(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var dot = path.IndexOf('.', StringComparison.Ordinal);

        return Raw is { } raw && dot > 0 && raw.Object(path[..dot]) is { } section
            ? section.Double(path[(dot + 1)..])
            : null;
    }

    /// <summary>The section names the last event carried, in the order Elite wrote them.</summary>
    public IReadOnlyList<string> Sections =>
        Raw is { } raw
            ? [.. raw.EnumerateObject().Where(property => property.Value.ValueKind == JsonValueKind.Object)
                .Select(property => property.Name)]
            : [];

    /// <summary>
    /// Every numeric figure in one section, keyed by its journal name, matched against
    /// <see cref="Sections"/> without regard to case. Empty where the event has no such section.
    /// </summary>
    public IReadOnlyList<(string Key, double Value)> Section(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (Raw is not { } raw)
        {
            return [];
        }

        foreach (var property in raw.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Object ||
                !string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return [.. property.Value.EnumerateObject()
                .Where(figure => figure.Value.ValueKind == JsonValueKind.Number)
                .Select(figure => (figure.Name, figure.Value.GetDouble()))];
        }

        return [];
    }

    public CareerStatistics Apply(JournalEvent journalEvent)
    {
        ArgumentNullException.ThrowIfNull(journalEvent);

        return journalEvent.Kind == "Statistics"
            ? new CareerStatistics { TakenAt = journalEvent.Timestamp, Raw = journalEvent.Raw }
            : this;
    }

    private static readonly IReadOnlyDictionary<string, string> Labels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["FLEETCARRIER"] = "Fleet carrier",
            ["Material_Trader_Stats"] = "Material trader",
        };

    /// <summary>
    /// A section or figure name in words: the table above where a key reads badly, otherwise the key split
    /// on its underscores and at each lower-to-upper case change, so a key Frontier adds later still reads.
    /// </summary>
    public static string Label(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        return Labels.TryGetValue(key, out var name)
            ? name
            : string.Join(
                ' ',
                CamelBoundary().Replace(key, "_").Split('_', StringSplitOptions.RemoveEmptyEntries)
                    .Select(word => char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant()));
    }

    /// <summary>
    /// A figure in its unit, guessed from what the key names: Merc Coins for a <c>MercCoins_</c> figure;
    /// credits for a profit, a spend or a wealth figure; light years for a distance; hours and minutes
    /// for a time.
    /// </summary>
    public static string Format(string key, double value)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (key.StartsWith("MercCoins", StringComparison.OrdinalIgnoreCase))
        {
            return $"{value:N0} Merc Coins";
        }

        if (key.Contains("Distance", StringComparison.OrdinalIgnoreCase))
        {
            return $"{value:0.##} ly";
        }

        if (key.Contains("Time", StringComparison.OrdinalIgnoreCase))
        {
            return Duration(value);
        }

        if (key.Contains("Profit", StringComparison.OrdinalIgnoreCase)
            || key.Contains("Wealth", StringComparison.OrdinalIgnoreCase)
            || key.Contains("Debt", StringComparison.OrdinalIgnoreCase)
            || key.Contains("Spent", StringComparison.OrdinalIgnoreCase))
        {
            return $"{value:N0} cr";
        }

        return value == Math.Floor(value) ? value.ToString("N0") : value.ToString("0.##");
    }

    private static string Duration(double totalSeconds)
    {
        var span = TimeSpan.FromSeconds(totalSeconds);
        var hours = (int)span.TotalHours;
        var minutes = span.Minutes;

        return hours > 0
            ? $"{hours}h {minutes}m"
            : $"{minutes} minute{(minutes == 1 ? "" : "s")}";
    }

    [GeneratedRegex("(?<=[a-z])(?=[A-Z])")]
    private static partial Regex CamelBoundary();
}
