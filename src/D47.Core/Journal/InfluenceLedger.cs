using System.Collections.Immutable;

namespace D47.Core.Journal;

/// <summary>The influence marks Elite reported on this session's completed missions, per faction.</summary>
public sealed record InfluenceLedger
{
    private const int FactionsNamed = 5;

    public static readonly InfluenceLedger Empty = new();

    private ImmutableArray<Mark> Marks { get; init; } = [];

    public bool IsEmpty => Marks.IsDefaultOrEmpty;

    /// <summary>Adds the marks of a MissionCompleted event; any other event returns this ledger.</summary>
    public InfluenceLedger Apply(JournalEvent journalEvent)
    {
        var added = Marks.IsDefault ? ImmutableArray.CreateBuilder<Mark>() : Marks.ToBuilder();
        var changed = false;

        foreach (var mark in Read(journalEvent))
        {
            added.Add(mark);
            changed = true;
        }

        return changed ? this with { Marks = added.ToImmutable() } : this;
    }

    /// <summary>The <c>+</c> marks one MissionCompleted event gives <paramref name="faction"/>, in <paramref name="systemAddress"/> when given; zero for any other event.</summary>
    public static int UpMarks(JournalEvent journalEvent, string faction, long? systemAddress = null)
    {
        ArgumentNullException.ThrowIfNull(journalEvent);
        ArgumentNullException.ThrowIfNull(faction);

        return Read(journalEvent)
            .Where(mark => mark.Up
                && string.Equals(mark.Faction, faction.Trim(), StringComparison.OrdinalIgnoreCase)
                && (systemAddress is null || mark.SystemAddress == systemAddress))
            .Sum(mark => Math.Max(mark.Text.Count(character => character == '+'), 1));
    }

    /// <summary>The <c>+</c> marks one MissionCompleted event gives any faction in <paramref name="systemAddress"/>; zero for any other event.</summary>
    public static int UpMarksIn(JournalEvent journalEvent, long systemAddress)
    {
        ArgumentNullException.ThrowIfNull(journalEvent);

        return Read(journalEvent)
            .Where(mark => mark.Up && mark.SystemAddress == systemAddress)
            .Sum(mark => Math.Max(mark.Text.Count(character => character == '+'), 1));
    }

    private static IEnumerable<Mark> Read(JournalEvent journalEvent)
    {
        if (journalEvent.Kind != "MissionCompleted")
        {
            yield break;
        }

        foreach (var effect in journalEvent.Items("FactionEffects"))
        {
            if (effect.String("Faction") is not { Length: > 0 } faction)
            {
                continue;
            }

            foreach (var influence in effect.Items("Influence"))
            {
                var up = influence.String("Trend") switch
                {
                    "UpGood" => true,
                    "DownBad" => false,
                    _ => (bool?)null,
                };

                if (up is not null)
                {
                    yield return new Mark(faction, up.Value, influence.String("Influence") is { Length: > 0 } marks ? marks : "+", influence.Long("SystemAddress"));
                }
            }
        }
    }

    /// <summary>The one-line report, or null when no marks were recorded.</summary>
    public string? Describe()
    {
        if (IsEmpty)
        {
            return null;
        }

        var factions = Marks
            .Select((mark, index) => (mark, index))
            .GroupBy(entry => entry.mark.Faction, StringComparer.Ordinal)
            .Select(group => (Faction: group.Key, First: group.Min(entry => entry.index), Marks: group.Select(entry => entry.mark).ToList()))
            .OrderByDescending(group => group.Marks.Count)
            .ThenBy(group => group.First)
            .ToList();

        var clauses = factions.Take(FactionsNamed).Select(Clause).ToList();

        if (factions.Count > FactionsNamed)
        {
            var more = factions.Count - FactionsNamed;
            clauses.Add($"and {more} more faction{(more == 1 ? "" : "s")}");
        }

        return $"Influence from missions: {string.Join("; ", clauses)}.";
    }

    private static string Clause((string Faction, int First, List<Mark> Marks) faction)
    {
        var directions = new List<string>();

        foreach (var up in (ReadOnlySpan<bool>)[true, false])
        {
            var counts = faction.Marks
                .Where(mark => mark.Up == up)
                .GroupBy(mark => mark.Text, StringComparer.Ordinal)
                .OrderByDescending(group => group.Count())
                .ThenByDescending(group => group.Key.Length)
                .Select(group => $"{group.Key} x{group.Count()}")
                .ToList();

            if (counts.Count > 0)
            {
                directions.Add($"{(up ? "up" : "down")} {string.Join(", ", counts)}");
            }
        }

        return $"{faction.Faction} {string.Join(", ", directions)}";
    }

    private sealed record Mark(string Faction, bool Up, string Text, long? SystemAddress = null);
}
