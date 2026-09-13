using System.Collections.Immutable;

namespace D47.Core.Journal;

/// <summary>The Commander's reputation with one superpower or faction, and when it was read.</summary>
public sealed record FactionReading(double MyReputation, DateTimeOffset SeenAt);

/// <summary>
/// The Commander's reputation, folded from <c>Reputation</c> for the four superpowers and from the
/// <c>Factions</c> of <c>Location</c>, <c>FSDJump</c> and <c>CarrierJump</c> for every other faction.
/// </summary>
public sealed record ReputationState
{
    private static readonly string[] Superpowers = ["Empire", "Federation", "Alliance", "Independent"];

    public static readonly ReputationState Empty = new();

    private ImmutableDictionary<string, FactionReading> SuperpowerReadings { get; init; } =
        ImmutableDictionary.Create<string, FactionReading>(StringComparer.OrdinalIgnoreCase);

    private ImmutableDictionary<string, FactionReading> FactionReadings { get; init; } =
        ImmutableDictionary.Create<string, FactionReading>(StringComparer.OrdinalIgnoreCase);

    /// <summary>The last reading for a superpower or faction. A faction absent from later events keeps it.</summary>
    public FactionReading? Reading(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var readings = Superpowers.Contains(name, StringComparer.OrdinalIgnoreCase)
            ? SuperpowerReadings
            : FactionReadings;

        return readings.TryGetValue(name, out var reading) ? reading : null;
    }

    /// <summary>The same superpower readings, with no faction readings.</summary>
    internal ReputationState WithoutFactions() => this with { FactionReadings = FactionReadings.Clear() };

    public ReputationState Apply(JournalEvent journalEvent)
    {
        ArgumentNullException.ThrowIfNull(journalEvent);

        return journalEvent.Kind switch
        {
            "Reputation" => WithSuperpowers(journalEvent),
            "Location" => WithFactions(journalEvent),
            "FSDJump" => WithFactions(journalEvent),
            "CarrierJump" => WithFactions(journalEvent),
            _ => this,
        };
    }

    private ReputationState WithSuperpowers(JournalEvent journalEvent)
    {
        var readings = SuperpowerReadings;

        foreach (var superpower in Superpowers)
        {
            if (journalEvent.Double(superpower) is { } value)
            {
                readings = readings.SetItem(superpower, new FactionReading(value, journalEvent.Timestamp));
            }
        }

        return readings == SuperpowerReadings ? this : this with { SuperpowerReadings = readings };
    }

    private ReputationState WithFactions(JournalEvent journalEvent)
    {
        var seen = journalEvent.Items("Factions")
            .Select(faction => (Name: faction.String("Name"), Value: faction.Double("MyReputation")))
            .Where(pair => pair.Name is not null && pair.Value is not null)
            .Select(pair => KeyValuePair.Create(pair.Name!, new FactionReading(pair.Value!.Value, journalEvent.Timestamp)))
            .ToList();

        return seen.Count == 0 ? this : this with { FactionReadings = FactionReadings.SetItems(seen) };
    }
}
