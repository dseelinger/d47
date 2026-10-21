namespace D47.Core.Knowledge;

/// <summary>A perk or rebuy cut at its latest value, and the rank it last changed at.</summary>
public sealed record HeldBenefit(string Name, string Value, int Since);

public sealed record HeldModule(string Name, int Rank);

/// <summary>One rank in a chapter that adds or improves a lasting benefit. <see cref="Old"/> is null for a first value or a module.</summary>
public sealed record BenefitRow(int Rank, string Name, string? Old, string? New, bool IsModule)
{
    /// <summary>The change as text, such as +20% → +30%, or MODULE.</summary>
    public string Change => IsModule ? "MODULE" : Old is null ? New! : Old + " → " + New;
}

/// <summary>A stretch of ranks still ahead, with the rows that change something and a note on its mini care packages.</summary>
public sealed record BenefitChapter(
    string Label,
    bool IsCurrent,
    int Merits,
    double Share,
    IReadOnlyList<BenefitRow> Rows,
    string? CarePackages);

/// <summary>What a Power's ranks give: what is held at a rank and what comes after it.</summary>
public sealed record PowerplayBenefits(
    string Power,
    int Rank,
    IReadOnlyList<HeldBenefit> Perks,
    IReadOnlyList<HeldModule> Modules,
    HeldModule? FirstModule,
    IReadOnlyList<int> CarePackageRanks,
    bool HasDecal,
    IReadOnlyList<BenefitChapter> ToCome,
    IReadOnlyList<int> TickRanks)
{
    /// <summary>The benefits for a pledged Power at a rank, or null where the table does not know the Power.</summary>
    public static PowerplayBenefits? For(string? power, int rank)
    {
        var rewards = PowerplayRanks.RewardsFor(power);

        if (rewards.Count == 0)
        {
            return null;
        }

        rank = Math.Clamp(rank, 1, PowerplayRanks.LastTabulatedRank);

        var name = rewards[0].Power;
        var held = rewards.Where(r => r.Rank <= rank).ToList();

        var perks = held
            .Where(r => IsLasting(r) && r.Kind != PowerplayRewardKind.Module)
            .GroupBy(r => (r.Kind, r.Subject))
            .Select(g => (First: g.First(), Last: g.Last()))
            .OrderBy(g => g.First.Kind)
            .ThenBy(g => g.First.Rank)
            .Select(g => new HeldBenefit(
                PowerplayWording.BenefitName(g.Last, name),
                PowerplayWording.SignedPercent(g.Last.Value),
                g.Last.Rank))
            .ToList();

        var modules = held
            .Where(r => r.Kind == PowerplayRewardKind.Module)
            .Select(r => new HeldModule(PowerplayWording.ModuleName(r), r.Rank))
            .ToList();

        var first = modules.Count > 0
            ? null
            : rewards
                .Where(r => r.Kind == PowerplayRewardKind.Module)
                .Select(r => new HeldModule(PowerplayWording.ModuleName(r), r.Rank))
                .FirstOrDefault();

        return new PowerplayBenefits(
            name,
            rank,
            perks,
            modules,
            first,
            held.Where(r => r.Kind == PowerplayRewardKind.CarePackage).Select(r => r.Rank).ToList(),
            held.Any(r => r.Kind == PowerplayRewardKind.Decal),
            Chapters(rewards, rank, name),
            rewards.Where(IsLasting).Select(r => r.Rank).Distinct().ToList());
    }

    private static bool IsLasting(PowerplayReward reward) =>
        reward.Kind is PowerplayRewardKind.Perk
            or PowerplayRewardKind.RebuyOwnTerritory
            or PowerplayRewardKind.RebuyRival
            or PowerplayRewardKind.Module;

    private static List<BenefitChapter> Chapters(IReadOnlyList<PowerplayReward> rewards, int rank, string power)
    {
        var chapters = new List<BenefitChapter>();
        var spans = new List<(int From, int To)> { (2, 10) };

        for (var start = 11; start <= 91; start += 10)
        {
            spans.Add((start, start + 9));
        }

        var whole = PowerplayRanks.MeritsNeeded(PowerplayRanks.LastTabulatedRank) ?? 1;

        foreach (var (from, to) in spans)
        {
            if (to <= rank)
            {
                continue;
            }

            var firstAhead = Math.Max(from, rank + 1);
            var rows = new List<BenefitRow>();

            foreach (var reward in rewards.Where(r => r.Rank >= firstAhead && r.Rank <= to && IsLasting(r)))
            {
                var before = rewards
                    .LastOrDefault(r => r.Rank < reward.Rank && r.Kind == reward.Kind && r.Subject == reward.Subject);

                rows.Add(reward.Kind == PowerplayRewardKind.Module
                    ? new BenefitRow(reward.Rank, PowerplayWording.ModuleName(reward), null, null, true)
                    : new BenefitRow(
                        reward.Rank,
                        PowerplayWording.BenefitName(reward, power),
                        before is null ? null : PowerplayWording.SignedPercent(before.Value),
                        PowerplayWording.SignedPercent(reward.Value),
                        false));
            }

            var packages = rewards
                .Where(r => r.Rank >= firstAhead && r.Rank <= to && r.Kind == PowerplayRewardKind.CarePackage)
                .Select(r => r.Rank)
                .ToList();

            var note = packages.Count == 0
                ? null
                : rows.Count > 0
                    ? $"Mini care package at the other {packages.Count} {(packages.Count == 1 ? "rank" : "ranks")}."
                    : $"Mini care package at {(packages.Count == 1 ? "rank" : "ranks")} {string.Join(", ", packages)}.";

            var merits = (PowerplayRanks.MeritsNeeded(to) ?? 0) - (PowerplayRanks.MeritsNeeded(from - 1) ?? 0);

            chapters.Add(new BenefitChapter(
                $"{from}–{to}",
                rank >= from - 1 && rank < to,
                merits,
                merits / (double)whole,
                rows,
                note));
        }

        return chapters;
    }
}
