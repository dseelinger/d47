using System.Text.Json;

namespace D47.Core.Journal;

/// <summary>The Commander's career statistics, from the last <c>Statistics</c> event.</summary>
public sealed record CareerStatistics
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
}
