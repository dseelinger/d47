using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>The fight in the Commander's system, as the carrier in that system knows it.</summary>
public static class CarrierFight
{
    /// <summary>
    /// What d47 knows of the fight, or null when no carrier is owned, the carrier is in another system, or the
    /// fight has no time (it was read while priming).
    /// </summary>
    public static string? Describe(FightSnapshot fight, string? commanderSystem, CarrierState carrier, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(fight);
        ArgumentNullException.ThrowIfNull(carrier);

        if (!carrier.Owned
            || carrier.StarSystem is not { Length: > 0 } carrierSystem
            || commanderSystem is not { Length: > 0 } here
            || !string.Equals(carrierSystem, here, StringComparison.OrdinalIgnoreCase)
            || fight.LastActionAt is not { } actionAt)
        {
            return null;
        }

        if (fight.Kills == 0 || fight.LastKillAt is not { } killAt)
        {
            return $"The Commander was attacked in this system {Ago(actionAt, now)}.";
        }

        var faction = fight.LastVictimFaction is { Length: > 0 } name ? $", of {name}" : string.Empty;
        var fact = $"The Commander has destroyed {fight.Kills} {(fight.Kills == 1 ? "ship" : "ships")} in this system; "
                   + $"the last was {Ago(killAt, now)}{faction}.";

        return actionAt > killAt ? $"{fact} They were attacked {Ago(actionAt, now)}." : fact;
    }

    /// <summary>The line that lets a brief mention <paramref name="fact"/> and nothing beyond it.</summary>
    public static string Instruction(string fact) =>
        $" You know of a fight in the Commander's system: {fact} You may mention it, in your own words. "
        + "Invent nothing beyond it: no attacker, no damage, no names.";

    private static string Ago(DateTimeOffset at, DateTimeOffset now)
    {
        var minutes = (int)Math.Max(0, (now - at).TotalMinutes);

        return minutes switch
        {
            0 => "less than a minute ago",
            1 => "1 minute ago",
            _ => $"{minutes} minutes ago",
        };
    }
}
