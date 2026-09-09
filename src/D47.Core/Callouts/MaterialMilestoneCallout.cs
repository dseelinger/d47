using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>
/// Progress towards filling a material (Phase 8, "Call out material-gathering milestones"): the first
/// unit, then 25/50/75%, a running count above 75%, and full.
/// </summary>
public sealed class MaterialMilestoneCallout : ICallout
{
    public string Id => "materials";

    /// <summary>How many of a material can be held, or null when that is not known.</summary>
    public Func<string, int?> Capacity { get; set; } = _ => null;

    private static readonly int[] Thresholds = [25, 50, 75];

    /// <summary>The highest milestone already announced per material, so each fires once.</summary>
    private readonly Dictionary<string, int> _announced = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Materials seen at all, which is what makes "the first unit" meaningful.</summary>
    private readonly HashSet<string> _seen = new(StringComparer.OrdinalIgnoreCase);

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        if (context.State is not { } state)
        {
            yield break;
        }

        // Before anything is announced, and on every tick rather than only on the ticks that carry a
        // collection: a material spent is a material whose milestones are ahead of it again.
        Follow(state);

        foreach (var journalEvent in context.Events)
        {
            if (journalEvent.Kind != "MaterialCollected" || journalEvent.String("Name") is not { } name)
            {
                continue;
            }

            var holding = state.Materials.Find(name);
            var count = holding?.Count ?? journalEvent.Int("Count") ?? 0;
            var spoken = holding?.Speak() ?? journalEvent.Named("Name") ?? name;

            var firstEver = _seen.Add(name);

            // Priming folds without announcing.
            if (context.IsPriming)
            {
                RecordSilently(name, count);
                continue;
            }

            if (firstEver)
            {
                yield return new Announcement("materials.first." + name, $"First {spoken}.");
            }

            if (Capacity(name) is not { } capacity || capacity <= 0)
            {
                // No capacity, no percentages.
                continue;
            }

            var percent = (int)Math.Floor(count * 100.0 / capacity);
            var already = _announced.GetValueOrDefault(name, 0);

            if (count >= capacity && already < 100)
            {
                _announced[name] = 100;
                yield return new Announcement("materials.full." + name, $"{spoken} is full. {count} of {capacity}.");
                continue;
            }

            // Above 75% every collection is worth a running count — that is the range where the Commander is
            // deciding whether to keep going or move on.
            if (percent >= 75 && already >= 75)
            {
                yield return new Announcement(
                    "materials.running." + name, $"{spoken}, {count} of {capacity}.")
                {
                    Cooldown = TimeSpan.FromSeconds(10),
                };
                continue;
            }

            var crossed = Thresholds.Where(threshold => percent >= threshold && already < threshold).ToArray();

            if (crossed.Length == 0)
            {
                continue;
            }

            // The highest crossed, not each one: collecting six units at once can cross two thresholds, and
            // announcing both is saying the same thing twice.
            var reached = crossed.Max();
            _announced[name] = reached;

            yield return new Announcement(
                "materials.milestone." + name, $"{spoken} at {reached} percent. {count} of {capacity}.");
        }
    }

    /// <summary>Lowers each material's record to where the Commander's holding actually is now.</summary>
    private void Follow(CommanderGameState state)
    {
        foreach (var name in _announced.Keys.ToArray())
        {
            if (Capacity(name) is not { } capacity || capacity <= 0)
            {
                continue;
            }

            var held = state.Materials.Find(name)?.Count ?? 0;
            var standing = Standing(held, capacity, (int)Math.Floor(held * 100.0 / capacity));

            if (standing < _announced[name])
            {
                _announced[name] = standing;
            }
        }
    }

    /// <summary>The milestone a holding of this size is at, said or not.</summary>
    private static int Standing(int count, int capacity, int percent) =>
        count >= capacity
            ? 100
            : Thresholds.Where(threshold => percent >= threshold).DefaultIfEmpty(0).Max();

    /// <summary>
    /// Brings the tracker up to date with a backlog without saying any of it, so the first live
    /// collection is judged against where the Commander actually is.
    /// </summary>
    private void RecordSilently(string name, int count)
    {
        if (Capacity(name) is not { } capacity || capacity <= 0)
        {
            return;
        }

        _announced[name] = Standing(count, capacity, (int)Math.Floor(count * 100.0 / capacity));
    }
}
