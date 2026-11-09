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

    /// <summary>
    /// An FNV-1a hash of the story id and chapter number, and of <paramref name="replaced"/>: the replaced objective's index in
    /// a rewrite, or minus how many times the whole chapter has been written again.
    /// </summary>
    public static uint Seed(string storyId, int chapter, int? replaced = null)
    {
        ArgumentNullException.ThrowIfNull(storyId);

        return Hash(replaced is { } index ? $"{storyId}|{chapter}|{index}" : $"{storyId}|{chapter}");
    }

    /// <summary>The seed of a chapter's sightseeing draw, apart from <see cref="Seed"/>; <paramref name="rewrites"/> is how many times the chapter was written again.</summary>
    public static uint SightSeed(string storyId, int chapter, int rewrites = 0)
    {
        ArgumentNullException.ThrowIfNull(storyId);

        return Hash(rewrites > 0 ? $"{storyId}|{chapter}|sight|{rewrites}" : $"{storyId}|{chapter}|sight");
    }

    /// <summary>The seed of a chapter's roll for a drawn span in the black.</summary>
    public static uint BlackSeed(string storyId, int chapter)
    {
        ArgumentNullException.ThrowIfNull(storyId);

        return Hash($"{storyId}|{chapter}|black");
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
    /// Up to <paramref name="slots"/> activities; a career weighing zero is never drawn. <paramref name="recent"/> are <c>RefusedActivities</c> keys; <paramref name="fixedPlaces"/>
    /// is how many place objectives the chapter already must have. A comfort chapter's first slot is the least-done activity that
    /// passes the gates, counting ties from row <paramref name="comfortFirst"/>; a mission family in <paramref name="neverCompleted"/>
    /// that passes the gates takes that slot first. With <paramref name="stationsInReach"/> false, rows that need a station are
    /// never drawn, and with <paramref name="frontlineInReach"/> false, so is the on-foot bond row, and with <paramref name="warInReach"/> false, so is the space bond row. A mission family that requires faction states is drawn only when <paramref name="stateFamiliesInReach"/> holds it; null does not gate. <paramref name="limits"/> holds each slot's limit by index: a limited slot draws only rows its limit accepts,
    /// unless that leaves no row the gates allow, when it draws from the whole table. A slot whose rows are all drawn takes the
    /// first later slot's rows that are not. No event slot is drawn while a limit holds.
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
        IReadOnlyList<Func<DrawableActivity, bool>>? limits = null,
        bool frontlineInReach = true,
        IReadOnlySet<string>? stateFamiliesInReach = null,
        bool warInReach = true)
    {
        ArgumentNullException.ThrowIfNull(weights);
        ArgumentNullException.ThrowIfNull(ship);
        ArgumentNullException.ThrowIfNull(recent);
        ArgumentNullException.ThrowIfNull(statistics);

        var held = ChapterActivities.Held(ranks, ship, odyssey);
        var all = ChapterActivities.All.Where(row => weights.Weight(row.Career) > 0 && Drawable(row, held, stationsInReach, refused, frontlineInReach, stateFamiliesInReach, warInReach)).ToList();
        var pools = Enumerable.Range(0, Math.Max(1, slots))
            .Select(slot => limits?.ElementAtOrDefault(slot) is { } limit && all.Where(limit).ToList() is { Count: > 0 } limited ? limited : all)
            .ToList();
        var limitHolds = pools.Any(pool => !ReferenceEquals(pool, all));
        var eligible = pools[0];

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

        for (var index = drawn.Count; index < slots; index++)
        {
            var open = pools.Skip(index)
                .Select(pool => pool.Where(row => !drawn.Contains(row) && (!row.PlaceBefore || places < room)).ToList())
                .FirstOrDefault(rows => rows.Count > 0) ?? [];

            if (open.Count == 0)
            {
                continue;
            }

            var fresh = open.Where(row => drawn.All(done => done.Career != row.Career)).ToList();
            var chosen = Pick(fresh.Count > 0 ? fresh : open, weights, recent, random);

            drawn.Add(chosen);
            places += chosen.PlaceBefore ? 1 : 0;
        }

        var result = drawn.Select(row => new DrawnActivity(row.Kind, row.Family, Count(row, ship, random)) { Organic = row.Organic, PlaceBefore = row.PlaceBefore }).ToList();

        if (!limitHolds && events is not null && random.NextDouble() < EventShare
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

    /// <summary>Whether <see cref="Draw"/> may draw <paramref name="row"/>: its gates are held, it needs no station out of reach, it needs no Frontline Solutions station out of reach, it needs no war out of reach, a state-gated mission family is in <paramref name="stateFamiliesInReach"/> (null does not gate), and it is not refused.</summary>
    public static bool Drawable(DrawableActivity row, ActivityGate held, bool stationsInReach, IReadOnlyList<string>? refused, bool frontlineInReach = true, IReadOnlySet<string>? stateFamiliesInReach = null, bool warInReach = true)
    {
        ArgumentNullException.ThrowIfNull(row);

        return ChapterActivities.Passes(row, held) && (stationsInReach || !ChapterActivities.NeedsStation(row)) && (frontlineInReach || !ChapterActivities.NeedsFrontline(row)) && (warInReach || !ChapterActivities.NeedsWar(row))
            && (stateFamiliesInReach is null || !ChapterActivities.NeedsStateInReach(row) || stateFamiliesInReach.Contains(row.Family!)) && !RefusedActivities.Refuses(refused, row.Kind, row.Family);
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

        foreach (var row in events.Drawable.Where(row => ChapterActivities.Passes(row.Needs, held) && !(Enum.TryParse<Career>(row.Career, out var career) && weights.Weight(career) <= 0)))
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
        var chosen = pool[random.Weighted(values)];

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

            if (weights.Weight(Career.Crime) <= 0)
            {
                options = [.. options.Where(value => !CrimeObjectives.Is(TriggerKind.Event, null, chosen.Row.Event, new EventFilter([new EventField(field, value)])))];
            }

            if (options.Count > 0)
            {
                pair = new EventFilter([new EventField(field, options[random.Next(options.Count)])]);
            }
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

        return rows[random.Weighted(values)];
    }

    /// <summary>A count for <paramref name="row"/> drawn from <paramref name="seed"/> as the draw sets one: within its range, tons capped at the cargo capacity.</summary>
    public static int CountOf(DrawableActivity row, ShipLoadout ship, uint seed)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(ship);

        return Count(row, ship, new Random32(seed));
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
}
