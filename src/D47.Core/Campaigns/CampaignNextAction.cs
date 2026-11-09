using System.Globalization;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Campaigns;

/// <summary>The one thing to do for a front, and at most one sentence naming work that does not count there.</summary>
public sealed record CampaignNextAction(Front Front, string Sentence, string? NotCounting)
{
    /// <summary>
    /// The next action for <paramref name="front"/>, from the Commander's kills and BGS work over the last week.
    /// Page numbers are from The Complete BGS Guide 2025.
    /// </summary>
    public static CampaignNextAction For(Front front, string faction, CombatRecord combat, BgsWork work, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(front);
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(work);

        var system = front.System;
        var bounties = Unredeemed(combat, work, faction, KillKind.Bounty, now);
        var bonds = Unredeemed(combat, work, faction, KillKind.Bond, now);
        var rival = front.Opponent is { Length: > 0 } named ? named : "the nearest rival";

        string Raise(string what) =>
            bounties > 0
                ? $"Redeem your {Credits(bounties)} in bounties for {faction} at a station in {system}, then {what}."
                : $"{char.ToUpperInvariant(what[0])}{what[1..]}.";

        return front.Kind switch
        {
            FrontKind.Conflict when front.Conflict is { } conflict && conflict.Type.Contains("election", StringComparison.OrdinalIgnoreCase) =>
                new(front, $"Run missions or trade for {faction} in {system}.", "Combat does not count in an election, and firing a weapon may void some election missions (guide p.61)."),
            FrontKind.Conflict =>
                new(
                    front,
                    bonds > 0
                        ? $"Redeem your {Credits(bonds)} in combat bonds for {faction} at a station in {system}, then fight for {faction} in a conflict zone there."
                        : $"Fight for {faction} in a conflict zone in {system}.",
                    "Trade does not count in a war (guide p.61)."),
            FrontKind.Coup => new(front, Raise($"run missions and trade for {faction} in {system} to lift its influence over {rival}"), null),
            FrontKind.Retreat => new(front, Raise($"run missions and trade for {faction} in {system} to lift its influence clear of retreat"), null),
            FrontKind.ThinMargin => new(front, Raise($"run missions and trade for {faction} in {system} to widen its lead over {rival}"), null),
            FrontKind.BadState => BadState(front, faction, bounties),
            FrontKind.UnwantedExpansion => new(front, $"Stop adding to {faction}'s influence in {system}: it is near 75% and the intent is to stay under it.", null),
            FrontKind.Expansion => new(front, $"Leave {faction}'s influence in {system} as it is until the expansion completes.", null),
            _ => new(front, $"Visit {system} to refresh the report of {faction}'s standing there.", null),
        };
    }

    private static CampaignNextAction BadState(Front front, string faction, long bounties)
    {
        var system = front.System;

        if (front.States.Contains("Lockdown", StringComparer.OrdinalIgnoreCase))
        {
            return new(
                front,
                bounties > 0
                    ? $"Redeem your {Credits(bounties)} in bounties for {faction} at a station in {system}."
                    : $"Hunt bounties for {faction} in {system} and redeem them there.",
                "Only bounties help during a Lockdown (guide p.87).");
        }

        if (front.States.Contains("Outbreak", StringComparer.OrdinalIgnoreCase))
        {
            return new(front, $"Run missions or trade for {faction} in {system}.", "Bounty vouchers do nothing during an Outbreak (guide p.88).");
        }

        return new(front, $"Keep {faction}'s influence in {system} up with missions and trade while {string.Join(" and ", front.States)} lasts.", null);
    }

    /// <summary>
    /// Credits the faction has paid for kills in the last week that no redemption for it has followed. A redemption
    /// counts everything before it.
    /// </summary>
    private static long Unredeemed(CombatRecord combat, BgsWork work, string faction, KillKind kind, DateTimeOffset now)
    {
        var redeemed = kind == KillKind.Bounty ? BgsWorkKind.Bounty : BgsWorkKind.CombatBond;

        var last = work.Entries
            .Where(entry => entry.Kind == redeemed && string.Equals(entry.Faction, faction, StringComparison.OrdinalIgnoreCase))
            .Select(entry => (DateTimeOffset?)entry.At)
            .Max();

        return combat.Kills
            .Where(kill => kill.Kind == kind && kill.At >= now - BgsTickWatch.MostBehind && (last is null || kill.At > last))
            .SelectMany(kill => kill.PaidBy)
            .Where(payment => string.Equals(payment.Faction, faction, StringComparison.OrdinalIgnoreCase))
            .Sum(payment => payment.Reward);
    }

    private static string Credits(long amount) => amount.ToString("N0", CultureInfo.InvariantCulture) + " credits";
}
