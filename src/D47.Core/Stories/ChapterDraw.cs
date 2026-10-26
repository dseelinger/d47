using System.Text;
using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Stories;

/// <summary>One drawn activity: a trigger kind, a mission family or null, and a count (credits for a data sale).</summary>
public sealed record DrawnActivity(TriggerKind Kind, string? Family, int Count)
{
    /// <summary>For a data sale: true for organic data only, false for cartographic data only.</summary>
    public bool? Organic { get; init; }

    /// <summary>Whether a place objective must come just before it.</summary>
    public bool PlaceBefore { get; init; }

    /// <summary>For an event slot: the event's name as the step event table spells it.</summary>
    public string? Event { get; init; }

    /// <summary>For an event slot: the field test the step carries, or none.</summary>
    public EventFilter Where { get; init; } = EventFilter.None;

    /// <summary>For a tool run slot: the phrase the Commander says to run the tool.</summary>
    public string? Say { get; init; }
}

/// <summary>A tool a story may send the Commander to run, with the first phrase that runs it.</summary>
public sealed record DrawableTool(string Name, string Phrase);

/// <summary>Draws a chapter's counted activities from <see cref="ChapterActivities"/>.</summary>
public static class ChapterDraw
{
    public const int Slots = 3;

    /// <summary>The share of chapters whose last slot is an event step.</summary>
    public const double EventShare = 1.0 / 3;

    /// <summary>Most journal events an event slot counts.</summary>
    public const int MostEventCount = 3;

    /// <summary>Most place objectives a chapter may hold.</summary>
    public const int MostPlaces = 2;

    /// <summary>The share of its weight a row keeps when the last chapters already asked for it.</summary>
    public const double RecentFactor = 0.25;

    /// <summary>The share of chapters that are sightseeing chapters at an even Exploration weight.</summary>
    public const double SightShare = 0.2;

    /// <summary>The most the share of sightseeing chapters is scaled up to.</summary>
    public const double MostSightShare = 0.4;

    /// <summary>An FNV-1a hash of the story id and chapter number, and of the replaced objective's index in a rewrite.</summary>
    public static uint Seed(string storyId, int chapter, int? replaced = null)
    {
        ArgumentNullException.ThrowIfNull(storyId);

        return Hash(replaced is { } index ? $"{storyId}|{chapter}|{index}" : $"{storyId}|{chapter}");
    }

    /// <summary>The seed of a chapter's sightseeing draw, apart from <see cref="Seed"/>.</summary>
    public static uint SightSeed(string storyId, int chapter)
    {
        ArgumentNullException.ThrowIfNull(storyId);

        return Hash($"{storyId}|{chapter}|sight");
    }

    /// <summary>The chance a chapter is a sightseeing chapter: scaled by the Exploration weight, capped, and reduced after a sightseeing chapter.</summary>
    public static double SightChance(CareerWeights weights, bool previousWasSight)
    {
        ArgumentNullException.ThrowIfNull(weights);

        var chance = Math.Min(MostSightShare, SightShare * weights.Weight(Career.Exploration) / CareerWeights.Default);

        return previousWasSight ? chance * RecentFactor : chance;
    }

    /// <summary>Whether the chapter with this <paramref name="seed"/> is a sightseeing chapter.</summary>
    public static bool Sightsee(CareerWeights weights, bool previousWasSight, uint seed) =>
        new Random32(seed).NextDouble() < SightChance(weights, previousWasSight);

    private static uint Hash(string text)
    {
        var hash = 2166136261u;

        foreach (var value in Encoding.UTF8.GetBytes(text))
        {
            hash = (hash ^ value) * 16777619u;
        }

        return hash;
    }

    /// <summary>
    /// Up to <paramref name="slots"/> activities. <paramref name="recent"/> are <c>RefusedActivities</c> keys; <paramref name="fixedPlaces"/>
    /// is how many place objectives the chapter already must have. A comfort chapter's first slot is the least-done activity that
    /// passes the gates, counting ties from row <paramref name="comfortFirst"/>; a mission family in <paramref name="neverCompleted"/>
    /// that passes the gates takes that slot first. With <paramref name="stationsInReach"/> false, rows that need a station are
    /// never drawn. With <paramref name="limit"/>, only rows it accepts are drawn
    /// and no event slot is drawn, unless it leaves no row the gates allow, when the whole table is drawn from as usual.
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
        Func<string, bool>? neverCompleted = null,
        StepEventCatalogue? events = null,
        IReadOnlyList<DrawableTool>? tools = null,
        int slots = Slots,
        Func<DrawableActivity, bool>? limit = null)
    {
        ArgumentNullException.ThrowIfNull(weights);
        ArgumentNullException.ThrowIfNull(ship);
        ArgumentNullException.ThrowIfNull(recent);
        ArgumentNullException.ThrowIfNull(statistics);

        var held = ChapterActivities.Held(ranks, ship, odyssey);
        var eligible = ChapterActivities.All.Where(row => Drawable(row, held, stationsInReach, refused)).ToList();

        if (limit is not null && eligible.Where(limit).ToList() is { Count: > 0 } limited)
        {
            eligible = limited;
        }
        else
        {
            limit = null;
        }

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

        while (drawn.Count < slots)
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

        var result = drawn.Select(row => new DrawnActivity(row.Kind, row.Family, Count(row, ship, random)) { Organic = row.Organic, PlaceBefore = row.PlaceBefore }).ToList();

        if (limit is null && events is not null && random.NextDouble() < EventShare
            && DrawEvent(events, tools ?? [], held, refused, recent, weights, [.. drawn.Take(Math.Min(drawn.Count, slots - 1))], random) is { } slot)
        {
            if (result.Count >= slots)
            {
                result.RemoveAt(result.Count - 1);
            }

            result.Add(slot);
        }

        return result;
    }

    /// <summary>Whether <see cref="Draw"/> may draw <paramref name="row"/>: its gates are held, it needs no station out of reach, and it is not refused.</summary>
    public static bool Drawable(DrawableActivity row, ActivityGate held, bool stationsInReach, IReadOnlyList<string>? refused)
    {
        ArgumentNullException.ThrowIfNull(row);

        return ChapterActivities.Passes(row, held) && (stationsInReach || !ChapterActivities.NeedsStation(row)) && !RefusedActivities.Refuses(refused, row.Kind, row.Family);
    }

    /// <summary>One event slot from the drawable events whose gates are held and which are not refused, or null when none is left.</summary>
    private static DrawnActivity? DrawEvent(
        StepEventCatalogue events,
        IReadOnlyList<DrawableTool> tools,
        ActivityGate held,
        IReadOnlyList<string>? refused,
        IReadOnlyList<string> recent,
        CareerWeights weights,
        List<DrawableActivity> kept,
        Random32 random)
    {
        var candidates = new List<(StepEvent Row, string? Tool, string? Say)>();

        foreach (var row in events.Drawable.Where(row => ChapterActivities.Passes(row.Needs, held)))
        {
            if (row.Event == D47Event.ToolRun)
            {
                candidates.AddRange(tools
                    .Where(tool => events.IsDrawableValue(row.Event, "Tool", tool.Name) && !RefusedActivities.Refuses(refused, TriggerKind.Event, null, row.Event, tool.Name))
                    .Select(tool => (row, (string?)tool.Name, (string?)tool.Phrase)));
            }
            else if (!RefusedActivities.Refuses(refused, TriggerKind.Event, null, row.Event))
            {
                candidates.Add((row, null, null));
            }
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        var fresh = candidates.Where(candidate => !Enum.TryParse<Career>(candidate.Row.Career, out var career) || kept.All(done => done.Career != career)).ToList();
        var pool = fresh.Count > 0 ? fresh : candidates;
        var values = pool.Select(candidate =>
                (Enum.TryParse<Career>(candidate.Row.Career, out var career) ? weights.Weight(career) : 1)
                * (recent.Contains(RefusedActivities.Key(TriggerKind.Event, null, candidate.Row.Event, candidate.Tool) ?? string.Empty, StringComparer.OrdinalIgnoreCase) ? RecentFactor : 1))
            .ToList();
        var total = values.Sum();
        var chosen = pool[^1];

        if (total <= 0)
        {
            chosen = pool[random.Next(pool.Count)];
        }
        else
        {
            var point = random.NextDouble() * total;

            for (var index = 0; index < pool.Count; index++)
            {
                point -= values[index];

                if (point < 0)
                {
                    chosen = pool[index];
                    break;
                }
            }
        }

        if (chosen.Tool is not null)
        {
            return new DrawnActivity(TriggerKind.Event, null, 1)
            {
                Event = chosen.Row.Event,
                Where = new EventFilter([new EventField("Tool", chosen.Tool)]),
                Say = chosen.Say,
            };
        }

        var withValues = events.FieldsOf(chosen.Row.Event).Where(field => events.DrawableValuesOf(chosen.Row.Event, field).Count > 0).ToList();
        var pair = EventFilter.None;

        if (withValues.Count > 0 && chosen.Row.Source == StepEventCatalogue.JournalSource)
        {
            var field = withValues[random.Next(withValues.Count)];
            var options = events.DrawableValuesOf(chosen.Row.Event, field);

            pair = new EventFilter([new EventField(field, options[random.Next(options.Count)])]);
        }

        return new DrawnActivity(TriggerKind.Event, null, chosen.Row.Source == StepEventCatalogue.JournalSource ? 1 + random.Next(MostEventCount) : 1)
        {
            Event = chosen.Row.Event,
            Where = pair,
        };
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
