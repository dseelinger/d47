namespace D47.Core.Adventures;

/// <summary>Which objectives ask the Commander to commit a crime.</summary>
public static class CrimeObjectives
{
    /// <summary>Crime mission families, besides every <see cref="MissionFamilies.IsIllegal"/> family.</summary>
    public static IReadOnlyList<string> Families { get; } =
    [
        "Mission_Smuggle", "Mission_OnFoot_Smuggle", "Mission_Piracy", "Mission_OnFoot_Heist", "Mission_OnFoot_ProductionHeist", "Mission_OnFoot_Sabotage",
    ];

    /// <summary>Whether a mission of this family is a crime.</summary>
    public static bool IsFamily(string? family) =>
        !string.IsNullOrWhiteSpace(family)
        && (MissionFamilies.IsIllegal(family) || Families.Any(known => family.Trim().StartsWith(known, StringComparison.OrdinalIgnoreCase)));

    /// <summary>Whether the step is a mission of a crime family, or an event step that tests stolen goods as true.</summary>
    public static bool Is(AdventureTrigger trigger)
    {
        ArgumentNullException.ThrowIfNull(trigger);

        return Is(trigger.Kind, trigger.MissionFamily, trigger.Event, trigger.Where);
    }

    /// <summary>Whether a beat of this kind, mission family, event and field tests is a crime.</summary>
    public static bool Is(TriggerKind kind, string? family, string? eventName, EventFilter where)
    {
        ArgumentNullException.ThrowIfNull(where);

        if (kind == TriggerKind.Event)
        {
            var field = eventName?.Trim() switch
            {
                "CollectCargo" or "CollectItems" => "Stolen",
                "MarketSell" => "StolenGoods",
                _ => null,
            };

            return field is not null
                && where.Any(pair => string.Equals(pair.Field, field, StringComparison.OrdinalIgnoreCase) && string.Equals(pair.Value?.Trim(), "true", StringComparison.OrdinalIgnoreCase));
        }

        return IsFamily(family);
    }
}
