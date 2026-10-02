namespace D47.Core.Adventures;

/// <summary>
/// The activities a Commander has refused in a story, kept as keys: a counted or engineer kind in lower case, and for a
/// mission with a family <c>mission:</c> and the family. Place kinds are never refused.
/// </summary>
public static class RefusedActivities
{
    private const string MissionKey = "mission";

    /// <summary>The key a beat of this kind and mission family is refused under, or null for a place kind.</summary>
    public static string? Key(TriggerKind kind, string? family)
    {
        if (kind < TriggerKind.Bounty)
        {
            return null;
        }

        return kind == TriggerKind.Mission && !string.IsNullOrWhiteSpace(family)
            ? $"{MissionKey}:{family.Trim()}"
            : kind.ToString().ToLowerInvariant();
    }

    /// <summary>Whether a beat of this kind and mission family is one of the refused activities.</summary>
    public static bool Refuses(IReadOnlyList<string>? refused, TriggerKind kind, string? family)
    {
        if (refused is null || kind < TriggerKind.Bounty)
        {
            return false;
        }

        foreach (var key in refused)
        {
            if (key.StartsWith(MissionKey + ":", StringComparison.Ordinal))
            {
                if (kind == TriggerKind.Mission
                    && !string.IsNullOrWhiteSpace(family)
                    && family.Trim().StartsWith(key[(MissionKey.Length + 1)..], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            else if (string.Equals(key, kind.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>What a refused activity is, as something the Commander does: "collect bounties", "complete courier missions".</summary>
    public static string Phrase(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (key.StartsWith(MissionKey + ":", StringComparison.Ordinal))
        {
            return $"complete {MissionFamilies.Word(key[(MissionKey.Length + 1)..])} missions";
        }

        return Enum.TryParse<TriggerKind>(key, ignoreCase: true, out var kind) ? Phrase(kind) : key;
    }

    /// <summary>The activity a beat of this kind asks for, or null for a place kind.</summary>
    public static string? Phrase(TriggerKind kind, string? family) => Key(kind, family) is { } key ? Phrase(key) : null;

    private static string Phrase(TriggerKind kind) => kind switch
    {
        TriggerKind.Bounty => "collect bounties",
        TriggerKind.Bond => "earn combat kill bonds",
        TriggerKind.Mission => "complete missions",
        TriggerKind.Sell => "sell cargo",
        TriggerKind.Mine => "mine and refine",
        TriggerKind.OnFoot => "step out on foot onto planets",
        TriggerKind.Collect => "collect items on foot",
        TriggerKind.Organic => "analyse organic samples",
        TriggerKind.Map => "map bodies with the surface mapper",
        TriggerKind.Signal => "survey bodies with signals",
        TriggerKind.Wreck => "touch down at crashed ships",
        TriggerKind.Codex => "log codex entries",
        TriggerKind.DataSale => "sell exploration data",
        TriggerKind.Salvage => "pick up cargo canisters",
        TriggerKind.Uss => "drop into signal sources",
        TriggerKind.Rescue => "hand in rescue items",
        TriggerKind.Engineer => "work toward an engineer",
        TriggerKind.Srv => "launch the SRV",
        TriggerKind.Crew => "hire crew",
        TriggerKind.SuitMod => "apply suit mods",
        TriggerKind.Livery => "change your ship's livery",
        TriggerKind.CarrierBuy => "buy a fleet carrier",
        TriggerKind.CarrierJump => "jump a fleet carrier",
        TriggerKind.Wing => "join a wing",
        TriggerKind.Multicrew => "join another Commander's crew",
        TriggerKind.Squadron => "join a squadron",
        TriggerKind.SquadronFound => "found a squadron",
        _ => kind.ToString().ToLowerInvariant(),
    };
}
