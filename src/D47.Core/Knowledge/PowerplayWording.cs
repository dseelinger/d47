using System.Globalization;

namespace D47.Core.Knowledge;

/// <summary>The words a Powerplay reward is spoken and shown as, shared by the voice answer and the Goals page.</summary>
public static class PowerplayWording
{
    private const string Minus = "−";

    public static string PerkLabel(PowerplayReward reward) =>
        PowerplayRanks.PerkLabel(reward.Subject) ?? reward.Subject;

    public static string ModuleName(PowerplayReward reward) =>
        EliteSpecifications.Modules
            .FirstOrDefault(m => string.Equals(m.Entitlement, reward.Subject, StringComparison.OrdinalIgnoreCase))
            ?.Name
        ?? reward.Subject;

    /// <summary>What a perk or rebuy cut changes, without its value.</summary>
    public static string BenefitName(PowerplayReward reward, string power) => reward.Kind switch
    {
        PowerplayRewardKind.RebuyOwnTerritory => $"rebuy in {power}'s territory",
        PowerplayRewardKind.RebuyRival => "rebuy when a rival Power's ship kills you",
        _ => PerkLabel(reward),
    };

    /// <summary>A perk's or rebuy's value as a signed percent, such as +20% or −40%.</summary>
    public static string SignedPercent(int? value)
    {
        var percent = Math.Abs(value ?? 0).ToString(CultureInfo.InvariantCulture);

        return (value < 0 ? Minus : "+") + percent + "%";
    }

    public static string PerkPhrase(PowerplayReward reward, string power)
    {
        var percent = Math.Abs(reward.Value ?? 0).ToString(CultureInfo.InvariantCulture);
        var lower = reward.Value < 0;

        return reward.Kind switch
        {
            PowerplayRewardKind.RebuyOwnTerritory =>
                $"{percent}% {(lower ? "off" : "on top of")} rebuy in {power}'s territory",
            PowerplayRewardKind.RebuyRival =>
                $"{percent}% {(lower ? "off" : "on top of")} rebuy when a rival Power's ship kills you outside {power}'s territory",
            _ => $"{PerkLabel(reward)} {(lower ? "down" : "up")} {percent}% in {power}'s territory",
        };
    }
}
