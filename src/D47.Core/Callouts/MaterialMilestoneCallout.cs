using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>
/// Progress towards filling a material (list.md Phase 8, "Call out material-gathering
/// milestones"): the first unit, then 25/50/75%, a running count above 75%, and full.
/// <para>
/// <b>The tracker is primed from the session backlog at startup</b>, which the checklist calls
/// out explicitly and which is the whole reason <see cref="CalloutContext.IsPriming"/> exists.
/// Without it, starting d47 after Elite means every material already gathered counts as
/// "first unit" the moment the backlog is read, and then the real milestones never fire because
/// they have already been passed silently.
/// </para>
/// <para>
/// <b>The percentage milestones need a per-material capacity, and Elite reports one nowhere</b> —
/// not in a journal event, not in Status.json, not in the inventory snapshot. Capacity is set by
/// grade, and the grade is not in the journal either. Capacity is therefore injected rather than
/// looked up here, and <see cref="MaterialGrades"/> supplies it from a table that is
/// <em>derived</em> from the canonical community id list rather than hand-written — see that
/// class for why the distinction is the whole argument. A material the table does not recognise
/// answers null, and its percentage milestones stay silent rather than being announced against a
/// made-up number. The first-unit milestone needs no capacity and works either way.
/// </para>
/// </summary>
public sealed class MaterialMilestoneCallout : ICallout
{
    public string Id => "materials";

    /// <summary>
    /// How many of a material can be held, or null when that is not known. Defaulting to null
    /// keeps this class testable against both answers, and null stays a correct answer rather
    /// than a stub: it silences the percentage milestones instead of guessing where they fall.
    /// </summary>
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

            // Priming folds without announcing. This is the line that makes starting d47 after
            // Elite behave the same as starting it before.
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
                // No capacity, no percentages. Announcing a fraction of a number d47 does not
                // have would be inventing the number.
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

            // Above 75% every collection is worth a running count — that is the range where the
            // Commander is deciding whether to keep going or move on.
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

            // The highest crossed, not each one: collecting six units at once can cross two
            // thresholds, and announcing both is saying the same thing twice.
            var reached = crossed.Max();
            _announced[name] = reached;

            yield return new Announcement(
                "materials.milestone." + name, $"{spoken} at {reached} percent. {count} of {capacity}.");
        }
    }

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

        var percent = (int)Math.Floor(count * 100.0 / capacity);

        _announced[name] = count >= capacity
            ? 100
            : Thresholds.Where(threshold => percent >= threshold).DefaultIfEmpty(0).Max();
    }
}
