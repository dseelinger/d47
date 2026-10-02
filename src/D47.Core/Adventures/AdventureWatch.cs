using System.Collections.Frozen;
using System.Globalization;
using D47.Core.Journal;

namespace D47.Core.Adventures;

/// <summary>What one <c>SuitLoadout</c> or <c>Loadout</c> changed against the last one seen for the same suit, weapon or ship.</summary>
public sealed record AdventureObservation(IReadOnlyDictionary<string, string> Seen, IReadOnlyList<string> NewMods, bool LiveryChanged);

/// <summary>Compares each loadout with the previous one, for the <see cref="TriggerKind.SuitMod"/> and <see cref="TriggerKind.Livery"/> beats.</summary>
public static class AdventureWatch
{
    /// <summary>The ship module slots that are cosmetic, as <c>Loadout</c> names them.</summary>
    public static readonly FrozenSet<string> CosmeticSlots = FrozenSet.ToFrozenSet(
        new[] { "PaintJob", "ShipName0", "ShipName1", "ShipID0", "ShipID1", "EngineColour", "WeaponColour", "StringLights", "VesselVoice" }
            .Concat(Enumerable.Range(1, 3).Select(number => $"Decal{number}"))
            .Concat(Enumerable.Range(1, 10).Select(number => $"Bobble{number:00}")),
        StringComparer.OrdinalIgnoreCase);

    public static bool IsCosmetic(string? slot) =>
        slot is { Length: > 0 }
        && (CosmeticSlots.Contains(slot) || slot.StartsWith("ShipKit", StringComparison.OrdinalIgnoreCase));

    public static readonly IReadOnlyDictionary<string, string> Nothing = new Dictionary<string, string>();

    /// <summary>
    /// Folds one event into <paramref name="seen"/>. A suit, weapon or ship with no earlier loadout is only remembered: it has no changes.
    /// </summary>
    public static AdventureObservation Observe(IReadOnlyDictionary<string, string> seen, JournalEvent journalEvent)
    {
        ArgumentNullException.ThrowIfNull(seen);
        ArgumentNullException.ThrowIfNull(journalEvent);

        return journalEvent.Kind switch
        {
            "SuitLoadout" => ObserveSuit(seen, journalEvent.Raw),
            "Loadout" => ObserveShip(seen, journalEvent.Raw),
            _ => new AdventureObservation(seen, [], false),
        };
    }

    /// <summary>How much an observation adds to a suit-mod or livery trigger's total.</summary>
    public static int Amount(AdventureTrigger trigger, AdventureObservation observation)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        ArgumentNullException.ThrowIfNull(observation);

        return trigger.Kind switch
        {
            TriggerKind.SuitMod => observation.NewMods.Count(mod => Same(trigger.Filter, mod)),
            TriggerKind.Livery => observation.LiveryChanged ? 1 : 0,
            _ => 0,
        };
    }

    private static bool Same(string? wanted, string mod) =>
        string.IsNullOrWhiteSpace(wanted) || string.Equals(wanted.Trim(), mod, StringComparison.OrdinalIgnoreCase);

    private static AdventureObservation ObserveSuit(IReadOnlyDictionary<string, string> seen, System.Text.Json.JsonElement raw)
    {
        var next = new Dictionary<string, string>(seen, StringComparer.Ordinal);
        List<string> added = [];

        void Compare(string key, IEnumerable<string> mods)
        {
            var current = mods.Select(mod => mod.ToLowerInvariant()).Distinct().Order(StringComparer.Ordinal).ToList();

            if (seen.TryGetValue(key, out var before))
            {
                var had = before.Split(';', StringSplitOptions.RemoveEmptyEntries);
                added.AddRange(current.Where(mod => !had.Contains(mod)));
            }

            next[key] = string.Join(';', current);
        }

        if (raw.Long("SuitID") is { } suit)
        {
            Compare("suit:" + suit.ToString(CultureInfo.InvariantCulture), Names(raw, "SuitMods"));
        }

        foreach (var module in raw.Items("Modules"))
        {
            if (module.Long("SuitModuleID") is { } id)
            {
                Compare("module:" + id.ToString(CultureInfo.InvariantCulture), Names(module, "WeaponMods"));
            }
        }

        return new AdventureObservation(next, added, false);
    }

    private static AdventureObservation ObserveShip(IReadOnlyDictionary<string, string> seen, System.Text.Json.JsonElement raw)
    {
        if (raw.Long("ShipID") is not { } ship)
        {
            return new AdventureObservation(seen, [], false);
        }

        var slots = raw.Items("Modules")
            .Where(module => IsCosmetic(module.String("Slot")))
            .Select(module => $"{module.String("Slot")}={module.String("Item")}")
            .Order(StringComparer.Ordinal);

        var key = "ship:" + ship.ToString(CultureInfo.InvariantCulture);
        var signature = string.Join(';', slots);
        var known = seen.TryGetValue(key, out var before);

        if (known && before == signature)
        {
            return new AdventureObservation(seen, [], false);
        }

        return new AdventureObservation(new Dictionary<string, string>(seen, StringComparer.Ordinal) { [key] = signature }, [], known);
    }

    private static IEnumerable<string> Names(System.Text.Json.JsonElement element, string property) =>
        element.TryGetProperty(property, out var list) && list.ValueKind == System.Text.Json.JsonValueKind.Array
            ? list.EnumerateArray().Select(entry => entry.GetString()).OfType<string>().Where(name => name.Length > 0)
            : [];
}
