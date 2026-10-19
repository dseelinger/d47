using System.Text;
using D47.Core.Adventures;
using D47.Core.Journal;

namespace D47.Core.Stories;

/// <summary>One drawn activity: a trigger kind, a mission family or null, and a count (credits for a data sale).</summary>
public sealed record DrawnActivity(TriggerKind Kind, string? Family, int Count)
{
    /// <summary>For a data sale: true for organic data only, false for cartographic data only.</summary>
    public bool? Organic { get; init; }

    /// <summary>Whether a place objective must come just before it.</summary>
    public bool PlaceBefore { get; init; }
}

/// <summary>Draws a chapter's counted activities from <see cref="ChapterActivities"/>.</summary>
public static class ChapterDraw
{
    public const int Slots = 3;

    /// <summary>Most place objectives a chapter may hold.</summary>
    public const int MostPlaces = 2;

    /// <summary>The share of its weight a row keeps when the last chapters already asked for it.</summary>
    public const double RecentFactor = 0.25;

    /// <summary>An FNV-1a hash of the story id and chapter number, and of the replaced objective's index in a rewrite.</summary>
    public static uint Seed(string storyId, int chapter, int? replaced = null)
    {
        ArgumentNullException.ThrowIfNull(storyId);

        var text = replaced is { } index ? $"{storyId}|{chapter}|{index}" : $"{storyId}|{chapter}";
        var hash = 2166136261u;

        foreach (var value in Encoding.UTF8.GetBytes(text))
        {
            hash = (hash ^ value) * 16777619u;
        }

        return hash;
    }

    /// <summary>
    /// Up to <see cref="Slots"/> activities. <paramref name="recent"/> are <c>RefusedActivities</c> keys; <paramref name="fixedPlaces"/>
    /// is how many place objectives the chapter already must have. A comfort chapter's first slot is the least-done activity that
    /// passes the gates, counting ties from row <paramref name="comfortFirst"/>; a mission family in <paramref name="neverCompleted"/>
    /// that passes the gates takes that slot first. With <paramref name="stationsInReach"/> false, rows that need a station are
    /// never drawn.
    /// </summary>
    public static IReadOnlyList<DrawnActivity> Draw(
        CareerWeights weights,
        RankState ranks,
        ShipLoadout ship,
        bool odyssey,
        IReadOnlyList<string> recent,
        IReadOnlyList<string>? refused,
        bool comfort,
        CareerStatistics statistics,
        int fixedPlaces,
        uint seed,
        int comfortFirst = 0,
        bool stationsInReach = true,
        Func<string, bool>? neverCompleted = null)
    {
        ArgumentNullException.ThrowIfNull(weights);
        ArgumentNullException.ThrowIfNull(ship);
        ArgumentNullException.ThrowIfNull(recent);
        ArgumentNullException.ThrowIfNull(statistics);

        var held = ChapterActivities.Held(ranks, ship, odyssey);
        var eligible = ChapterActivities.All
            .Where(row => ChapterActivities.Passes(row, held) && (stationsInReach || !ChapterActivities.NeedsStation(row)) && !RefusedActivities.Refuses(refused, row.Kind, row.Family))
            .ToList();
        var random = new Random32(seed);
        var drawn = new List<DrawableActivity>();
        var places = 0;
        var room = Math.Max(0, MostPlaces - fixedPlaces);

        if (comfort
            && neverCompleted is not null
            && eligible.Where(row => row.Kind == TriggerKind.Mission && row.Family is not null && neverCompleted(row.Family) && (!row.PlaceBefore || places < room)).ToList() is { Count: > 0 } unrun)
        {
            var row = unrun[comfortFirst % unrun.Count];

            drawn.Add(row);
            places += row.PlaceBefore ? 1 : 0;
        }
        else if (comfort
            && ChapterFit.LeastDone(statistics, refused, comfortFirst, activity => eligible.Any(row => Same(activity, row))) is { } picked)
        {
            var rows = eligible.Where(row => Same(picked, row) && (!row.PlaceBefore || places < room)).ToList();

            if (rows.Count > 0)
            {
                var row = Pick(rows, weights, recent, random);

                drawn.Add(row);
                places += row.PlaceBefore ? 1 : 0;
            }
        }

        while (drawn.Count < Slots)
        {
            var open = eligible.Where(row => !drawn.Contains(row) && (!row.PlaceBefore || places < room)).ToList();

            if (open.Count == 0)
            {
                break;
            }

            var fresh = open.Where(row => drawn.All(done => done.Career != row.Career)).ToList();
            var chosen = Pick(fresh.Count > 0 ? fresh : open, weights, recent, random);

            drawn.Add(chosen);
            places += chosen.PlaceBefore ? 1 : 0;
        }

        return [.. drawn.Select(row => new DrawnActivity(row.Kind, row.Family, Count(row, ship, random)) { Organic = row.Organic, PlaceBefore = row.PlaceBefore })];
    }

    private static bool Same(AdventureActivity activity, DrawableActivity row) =>
        activity.Kind == row.Kind
        && (activity.MissionFamily is null
            || (row.Family is not null && row.Family.StartsWith(activity.MissionFamily, StringComparison.OrdinalIgnoreCase)));

    private static bool WasRecent(DrawableActivity row, IReadOnlyList<string> recent) =>
        recent.Any(key => string.Equals(key, row.Key, StringComparison.OrdinalIgnoreCase)
            || (row.Family is not null
                && key.StartsWith("mission:", StringComparison.Ordinal)
                && key["mission:".Length..].StartsWith(row.Family, StringComparison.OrdinalIgnoreCase)));

    private static DrawableActivity Pick(List<DrawableActivity> rows, CareerWeights weights, IReadOnlyList<string> recent, Random32 random)
    {
        var values = rows.Select(row => weights.Weight(row.Career) * (WasRecent(row, recent) ? RecentFactor : 1)).ToList();
        var total = values.Sum();

        if (total <= 0)
        {
            return rows[random.Next(rows.Count)];
        }

        var point = random.NextDouble() * total;

        for (var index = 0; index < rows.Count; index++)
        {
            point -= values[index];

            if (point < 0)
            {
                return rows[index];
            }
        }

        return rows[^1];
    }

    private static int Count(DrawableActivity row, ShipLoadout ship, Random32 random)
    {
        var min = row.Min;
        var max = row.Max;

        if (row.IsTons)
        {
            max = Math.Min(max, ship.CargoCapacity ?? 0);
            min = Math.Min(min, max);
        }

        return row.IsCredits
            ? min + (random.Next(((max - min) / ChapterActivities.CreditStep) + 1) * ChapterActivities.CreditStep)
            : min + random.Next(max - min + 1);
    }

    /// <summary>A generator whose sequence is fixed by its seed.</summary>
    private sealed class Random32(uint seed)
    {
        private uint state = seed;

        public int Next(int bound) => (int)(NextUInt() % (uint)bound);

        public double NextDouble() => NextUInt() / 4294967296.0;

        private uint NextUInt()
        {
            state += 0x9E3779B9u;

            var value = state;

            value = (value ^ (value >> 16)) * 0x85EBCA6Bu;
            value = (value ^ (value >> 13)) * 0xC2B2AE35u;

            return value ^ (value >> 16);
        }
    }
}
