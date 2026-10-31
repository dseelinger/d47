using System.Globalization;
using System.Text.Json;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Adventures;

public enum NotableKind
{
    Death,
    Interdicted,
    Escaped,
    Promotion,
    Engineer,
    ShipBought,
    HullScare,
    Sale,
    ExplorationSale,
    Bounty,
    Discoveries,
}

/// <summary>One notable thing the journal shows the Commander did. <see cref="Amount"/> ranks the "largest" kinds and counts discoveries.</summary>
public sealed record Notable(NotableKind Kind, DateTimeOffset At, string Text, long Amount = 0, bool Pending = false);

/// <summary>Classifies journal events into <see cref="Notable"/> entries and keeps the list a chapter's standing carries.</summary>
public static class ChapterNotables
{
    public const int MostTold = 12;

    public const int MostDeathsTold = 3;

    private const double ScareBelow = 0.25;

    /// <summary>The entry one event makes on its own, or null. Events that change an earlier entry are handled by <see cref="Fold"/>.</summary>
    public static Notable? Of(JournalEvent journalEvent)
    {
        ArgumentNullException.ThrowIfNull(journalEvent);

        var raw = journalEvent.Raw;
        var at = journalEvent.Timestamp;

        switch (journalEvent.Kind)
        {
            case "Died":
                return new Notable(NotableKind.Death, at, Killed(raw));

            case "Interdicted":
                return new Notable(NotableKind.Interdicted, at, Interdicted(raw));

            case "EscapeInterdiction":
                return new Notable(
                    NotableKind.Escaped,
                    at,
                    raw.Bool("IsPlayer") ? "Escaped an interdiction by another Commander." : "Escaped an interdiction.");

            case "Promotion":
                return Careers.Keys.FirstOrDefault(key => raw.Int(key) is not null) is { } career
                    ? new Notable(NotableKind.Promotion, at, $"Promoted to {new RankStanding(career, raw.Int(career)!.Value).Say(withPercent: false)}.")
                    : null;

            case "EngineerProgress":
                return raw.String("Engineer") is { Length: > 0 } engineer && EngineerStages.Rank(raw.String("Progress")) > 0
                    ? new Notable(NotableKind.Engineer, at, $"The engineer {engineer} was {raw.String("Progress")!.Trim().ToLowerInvariant()}.")
                    : null;

            case "ShipyardBuy":
                return raw.String("ShipType") is { Length: > 0 } ship
                    ? new Notable(NotableKind.ShipBought, at, $"Bought a new {EliteSpecifications.HullSaid(ship)}.")
                    : null;

            case "MarketSell":
                return raw.Long("TotalSale") is { } sale and > 0
                    ? new Notable(NotableKind.Sale, at, $"Their largest sale was {Credits(sale)} of {(raw.Named("Type") is { Length: > 0 } goods ? goods : "cargo")}.", sale)
                    : null;

            case "MultiSellExplorationData":
                return raw.Long("TotalEarnings") is { } earned and > 0
                    ? new Notable(NotableKind.ExplorationSale, at, $"Their largest sale of exploration data was {Credits(earned)}.", earned)
                    : null;

            case "Bounty":
                return raw.Long("TotalReward") is { } reward and > 0
                    ? new Notable(NotableKind.Bounty, at, $"Their largest bounty was {Credits(reward)} for a {(JournalNames.IsSuitAi(raw.String("Target")) ? "soldier" : raw.Named("Target") is { Length: > 0 } target ? target : "ship")}.", reward)
                    : null;

            default:
                return null;
        }
    }

    /// <summary>The list after one more event: new entries added, the "largest" kinds and the discovery count replaced, a rebuy joined to its death.</summary>
    public static IReadOnlyList<Notable> Fold(IReadOnlyList<Notable> notables, JournalEvent journalEvent)
    {
        ArgumentNullException.ThrowIfNull(notables);
        ArgumentNullException.ThrowIfNull(journalEvent);

        var raw = journalEvent.Raw;

        switch (journalEvent.Kind)
        {
            case "Resurrect":
                return Rebought(notables, raw);

            case "Docked":
                return notables.Any(entry => entry.Pending)
                    ? [.. notables.Select(entry => entry.Pending ? entry with { Pending = false } : entry)]
                    : notables;

            case "HullDamage":
                return raw.Bool("PlayerPilot") && raw.Double("Health") is { } health and < ScareBelow
                    ? Scare(notables, journalEvent.Timestamp, health)
                    : notables;

            case "Scan":
                return IsFirstDiscovery(raw) ? Discovered(notables, journalEvent.Timestamp) : notables;
        }

        if (Of(journalEvent) is not { } entry)
        {
            return notables;
        }

        if (entry.Kind is NotableKind.Death)
        {
            return [.. notables.Where(existing => existing.Kind != NotableKind.HullScare || !existing.Pending), entry];
        }

        if (entry.Kind is NotableKind.Sale or NotableKind.ExplorationSale or NotableKind.Bounty)
        {
            var best = notables.FirstOrDefault(existing => existing.Kind == entry.Kind);

            return best is null
                ? [.. notables, entry]
                : entry.Amount > best.Amount ? [.. notables.Where(existing => existing.Kind != entry.Kind), entry] : notables;
        }

        return [.. notables, entry];
    }

    /// <summary>What the chapter writer is told: settled entries, newest first, at most three deaths and twelve entries.</summary>
    public static IReadOnlyList<Notable> Told(IReadOnlyList<Notable> notables)
    {
        ArgumentNullException.ThrowIfNull(notables);

        var settled = notables.Select((entry, index) => (entry, index)).Where(pair => !pair.entry.Pending);
        var newest = settled.OrderByDescending(pair => pair.entry.At).ThenByDescending(pair => pair.index).Select(pair => pair.entry).ToList();
        var deaths = 0;

        return [.. newest.Where(entry => entry.Kind != NotableKind.Death || ++deaths <= MostDeathsTold).Take(MostTold)];
    }

    private static IReadOnlyList<Notable> Rebought(IReadOnlyList<Notable> notables, JsonElement raw)
    {
        var index = -1;

        for (var at = notables.Count - 1; at >= 0; at--)
        {
            if (notables[at].Kind == NotableKind.Death)
            {
                index = at;
                break;
            }
        }

        if (index < 0 || notables[index].Amount > 0)
        {
            return notables;
        }

        var cost = raw.Long("Cost") ?? 0;
        var rebuy = raw.Bool("Bankrupt")
            ? " They were bankrupt."
            : cost > 0 ? $" The rebuy cost {Credits(cost)}." : string.Empty;

        if (rebuy.Length == 0)
        {
            return notables;
        }

        var list = notables.ToList();
        list[index] = list[index] with { Text = list[index].Text + rebuy, Amount = Math.Max(cost, 1) };

        return list;
    }

    private static IReadOnlyList<Notable> Scare(IReadOnlyList<Notable> notables, DateTimeOffset at, double health)
    {
        var pending = notables.FirstOrDefault(entry => entry.Kind == NotableKind.HullScare && entry.Pending);

        if (pending is not null && health >= pending.Amount / 1000.0)
        {
            return notables;
        }

        var scare = new Notable(
            NotableKind.HullScare,
            at,
            $"Their ship's hull fell to {Math.Round(health * 100).ToString("0", CultureInfo.InvariantCulture)}% and they got away.",
            (long)Math.Round(health * 1000),
            Pending: true);

        return [.. notables.Where(entry => !ReferenceEquals(entry, pending)), scare];
    }

    private static bool IsFirstDiscovery(JsonElement raw) =>
        raw.TryGetProperty("WasDiscovered", out var discovered)
        && discovered.ValueKind == JsonValueKind.False
        && raw.String("ScanType") is "AutoScan" or "Detailed";

    private static IReadOnlyList<Notable> Discovered(IReadOnlyList<Notable> notables, DateTimeOffset at)
    {
        var count = (notables.FirstOrDefault(entry => entry.Kind == NotableKind.Discoveries)?.Amount ?? 0) + 1;
        var text = count == 1
            ? "They were the first to discover a body."
            : $"They were the first to discover {count.ToString(CultureInfo.InvariantCulture)} bodies.";

        return [.. notables.Where(entry => entry.Kind != NotableKind.Discoveries), new Notable(NotableKind.Discoveries, at, text, count)];
    }

    private static string Killed(JsonElement raw)
    {
        var hull = raw.String("KillerShip") is { Length: > 0 } ship ? EliteSpecifications.HullSaid(ship).Replace('_', ' ') : null;
        var rank = raw.String("KillerRank") is { Length: > 0 } killerRank ? killerRank.Trim().ToLowerInvariant() : null;

        return (hull, rank) switch
        {
            (not null, not null) => $"Died, destroyed by a {rank} {hull}.",
            (not null, null) => $"Died, destroyed by a {hull}.",
            _ => "Died.",
        };
    }

    private static string Interdicted(JsonElement raw)
    {
        var by = raw.Bool("IsPlayer")
            ? "another Commander"
            : raw.String("Faction") is { Length: > 0 } faction ? $"a ship of {faction}" : "a ship";
        var response = raw.Bool("Submitted") ? "submitted" : "fought it";

        return $"Pulled out of supercruise by {by}; they {response}.";
    }

    private static string Credits(long amount) =>
        amount.ToString("N0", CultureInfo.InvariantCulture) + " credits";
}
