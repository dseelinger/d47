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

    public CareerStatistics Apply(JournalEvent journalEvent)
    {
        ArgumentNullException.ThrowIfNull(journalEvent);

        return journalEvent.Kind == "Statistics"
            ? new CareerStatistics { TakenAt = journalEvent.Timestamp, Raw = journalEvent.Raw }
            : this;
    }
}
