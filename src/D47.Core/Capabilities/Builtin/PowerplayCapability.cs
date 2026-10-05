using System.Globalization;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Capabilities.Builtin;

/// <summary>What a Powerplay rank gives, for the Commander's own Power or any other the table lists.</summary>
public static class PowerplayCapability
{
    public const string Id = "powerplay";

    public const string Tool = "get_powerplay_rewards";

    private const int MaxNamed = 3;

    public static CapabilityDescriptor Create(Func<CommanderGameState?> state) => new()
    {
        Id = Id,
        Group = "Knowledge",
        Name = "Powerplay ranks",
        Summary = "What a Powerplay rank gives: perks, rebuy reductions, modules, the next rank and the merits to reach it.",
        Examples =
        [
            "what does my next Powerplay rank give me",
            "what would Mahon give me",
            "when do I get the first Powerplay module",
        ],
        Display = new CapabilityDisplay { PanelTitle = "Powerplay ranks", Order = 29, ShowOnPanel = false },
        Tools =
        [
            new ToolDefinition
            {
                Name = Tool,
                Description =
                    "What a Powerplay rank gives: each perk and rebuy reduction held at that rank, the modules "
                    + "unlocked (three named, the rest counted), what the next rank gives, and the next perk and "
                    + "module with their ranks. Merits to reach a rank are given for the Commander's own Power only. "
                    + "Defaults to the pledged Power at the Commander's rank; for another Power it defaults to rank 100.",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "power",
                        Type = ToolParameterType.String,
                        Description = "The Power to ask about. Defaults to the Power the Commander is pledged to.",
                        AllowedValues = PowerplayRanks.Powers,
                    },
                    new ToolParameter
                    {
                        Name = "rank",
                        Type = ToolParameterType.Integer,
                        Description =
                            "A rank from 1 to 100. Defaults to the Commander's rank for their own Power, and to 100 "
                            + "for any other.",
                    },
                ],
                Handler = (arguments, _) => Task.FromResult(Answer(state(), arguments)),
            },
        ],
    };

    public static string Describe(CommanderGameState? state, string? power = null, int? rank = null)
    {
        var pledge = state?.Pledge ?? PowerplayPledge.None;

        if (power is not { Length: > 0 })
        {
            if (!pledge.IsPledged)
            {
                return (pledge.IsKnown ? "You are not pledged to a Power." : "I do not know which Power you are pledged to.")
                    + " Name a Power and I will say what it gives.";
            }

            power = pledge.Power;
        }

        var rewards = PowerplayRanks.RewardsFor(power);

        if (rewards.Count == 0)
        {
            return $"I have no rank table for {power}.";
        }

        var name = rewards[0].Power;
        var own = pledge.IsPledged && string.Equals(pledge.Power, name, StringComparison.OrdinalIgnoreCase);

        if (rank is null)
        {
            if (own && pledge.Rank <= 0)
            {
                return $"I do not know your rank with {name} yet. Give me a rank and I will say what it holds.";
            }

            rank = own ? pledge.Rank : PowerplayRanks.LastTabulatedRank;
        }

        var held = Math.Min(rank.Value, PowerplayRanks.LastTabulatedRank);
        var merits = own ? pledge.Merits : null;
        var said = new List<string> { $"Rank {rank} with {name}." };

        said.AddRange(Held(rewards, held, name));

        if (held >= PowerplayRanks.LastTabulatedRank)
        {
            said.Add($"Every rank past {PowerplayRanks.LastTabulatedRank} gives a full care package.");

            return string.Join(' ', said);
        }

        var next = rewards.Where(r => r.Rank == held + 1).ToList();

        if (next.Count > 0)
        {
            var gives = JoinWords(next.Select(r => Gives(r, name)));

            var away = Away(held + 1, merits);

            said.Add($"Rank {held + 1}{away}{(away.Length > 0 ? "," : "")} gives {gives}.");
        }

        if (rewards.FirstOrDefault(r => r.Rank > held + 1 && IsPerk(r)) is { } perk)
        {
            said.Add($"The next perk is at rank {perk.Rank}{Away(perk.Rank, merits)}: {PerkPhrase(perk, name)}.");
        }

        if (rewards.FirstOrDefault(r => r.Rank > held + 1 && r.Kind == PowerplayRewardKind.Module) is { } module)
        {
            var which = rewards.Any(r => r.Kind == PowerplayRewardKind.Module && r.Rank <= held) ? "next" : "first";

            said.Add($"The {which} module is the {ModuleName(module)} at rank {module.Rank}{Away(module.Rank, merits)}.");
        }

        return string.Join(' ', said);
    }

    private static ToolResult Answer(CommanderGameState? state, ToolArguments arguments)
    {
        int? rank = null;

        if (arguments.TryGetString("rank", out var raw) && raw.Length > 0)
        {
            if (!arguments.TryGetInt32("rank", out var parsed) || parsed < 1 || parsed > PowerplayRanks.LastTabulatedRank)
            {
                return ToolResult.Error($"rank must be a whole number from 1 to {PowerplayRanks.LastTabulatedRank}.");
            }

            rank = parsed;
        }

        arguments.TryGetString("power", out var power);

        return ToolResult.Ok(Describe(state, power, rank));
    }

    private static IEnumerable<string> Held(IReadOnlyList<PowerplayReward> rewards, int held, string name)
    {
        var current = rewards
            .Where(r => r.Rank <= held && IsPerk(r))
            .GroupBy(r => (r.Kind, r.Subject))
            .Select(g => (First: g.First(), Last: g.Last()))
            .OrderBy(g => g.First.Kind)
            .ThenBy(g => g.First.Rank);

        foreach (var (_, reward) in current)
        {
            var percent = Math.Abs(reward.Value ?? 0).ToString(CultureInfo.InvariantCulture);
            var lower = reward.Value < 0;

            yield return reward.Kind switch
            {
                PowerplayRewardKind.RebuyOwnTerritory =>
                    $"In {name}'s territory your rebuy is {percent}% {(lower ? "lower" : "higher")}.",
                PowerplayRewardKind.RebuyRival =>
                    $"When a rival Power's ship kills you outside {name}'s territory your rebuy is {percent}% {(lower ? "lower" : "higher")}.",
                _ => $"In {name}'s territory, {Label(reward)} {(lower ? "down" : "up")} {percent}%.",
            };
        }

        var modules = rewards.Where(r => r.Kind == PowerplayRewardKind.Module && r.Rank <= held).ToList();

        if (modules.Count > 0)
        {
            var named = modules.Take(MaxNamed).Select(ModuleName).ToList();
            var more = modules.Count - named.Count;

            yield return more > 0
                ? $"Modules unlocked: {string.Join(", ", named)} and {more} more."
                : $"Modules unlocked: {JoinWords(named)}.";
        }
    }

    private static bool IsPerk(PowerplayReward reward) =>
        reward.Kind is PowerplayRewardKind.Perk
            or PowerplayRewardKind.RebuyOwnTerritory
            or PowerplayRewardKind.RebuyRival;

    private static string Gives(PowerplayReward reward, string name) => reward.Kind switch
    {
        PowerplayRewardKind.Decal => $"a {reward.Subject} decal",
        PowerplayRewardKind.CarePackage => $"a {reward.Subject} care package",
        PowerplayRewardKind.Module => $"the {ModuleName(reward)}",
        _ => PerkPhrase(reward, name),
    };

    private static string PerkPhrase(PowerplayReward reward, string name)
    {
        var percent = Math.Abs(reward.Value ?? 0).ToString(CultureInfo.InvariantCulture);
        var lower = reward.Value < 0;

        return reward.Kind switch
        {
            PowerplayRewardKind.RebuyOwnTerritory =>
                $"{percent}% {(lower ? "off" : "on top of")} rebuy in {name}'s territory",
            PowerplayRewardKind.RebuyRival =>
                $"{percent}% {(lower ? "off" : "on top of")} rebuy when a rival Power's ship kills you outside {name}'s territory",
            _ => $"{Label(reward)} {(lower ? "down" : "up")} {percent}% in {name}'s territory",
        };
    }

    private static string Label(PowerplayReward reward) =>
        PowerplayRanks.PerkLabel(reward.Subject) ?? reward.Subject;

    private static string ModuleName(PowerplayReward reward) =>
        EliteSpecifications.Modules
            .FirstOrDefault(m => string.Equals(m.Entitlement, reward.Subject, StringComparison.OrdinalIgnoreCase))
            ?.Name
        ?? reward.Subject;

    private static string Away(int rank, long? merits) =>
        merits is { } have && PowerplayRanks.MeritsNeeded(rank) is { } needed && needed > have
            ? $", {needed - have:N0} merits away"
            : "";

    private static string JoinWords(IEnumerable<string> items)
    {
        var list = items.ToList();

        return list.Count <= 1
            ? string.Concat(list)
            : $"{string.Join(", ", list.Take(list.Count - 1))} and {list[^1]}";
    }
}
