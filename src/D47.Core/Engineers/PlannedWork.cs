using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Loadout;
using D47.Core.Ships;

namespace D47.Core.Engineers;

/// <summary>
/// One planned thing that somebody has to roll, and who could roll it (list.md Phase 28, "The
/// fastest way in").
/// </summary>
/// <param name="What">The ship and slot, or the item and mod slot, that wants it.</param>
/// <param name="Wants">The blueprint or modification, as the plan states it.</param>
/// <param name="Grade">The grade planned, or null for a wildcard.</param>
/// <param name="Engineers">
/// Everybody who can do it, by name. <b>Any one of them satisfies it</b> — which is exactly why
/// the best next unlock is not the shortest chain: one person can cover several of these, and
/// several people can cover one.
/// </param>
public sealed record PlannedWork(
    string What,
    string Wants,
    int? Grade,
    IReadOnlyList<string> Engineers)
{
    /// <summary>The rank the roll needs, which is the grade — grade N cannot be rolled below rank N.</summary>
    public int Rank => Math.Clamp(Grade ?? 1, 1, 5);

    /// <summary>Whether somebody already unlocked can roll this today, at the grade wanted.</summary>
    public bool CanBeRolled(EngineerProgressState? progress) =>
        Engineers.Any(name => EngineerDirectory.ByName(name) is { } engineer
                              && progress?.For(engineer.Id) is { IsUnlocked: true } standing
                              && Math.Max(standing.Rank ?? 1, 1) >= Rank);

    /// <summary>Whether every engineer who could roll it is somebody the Commander has not unlocked.</summary>
    public bool WaitingOnAStranger(EngineerProgressState? progress) =>
        Engineers.Count > 0
        && !Engineers.Any(name => EngineerDirectory.ByName(name) is { } engineer
                                  && progress?.For(engineer.Id) is { IsUnlocked: true });

    public string Describe() =>
        Grade is { } grade ? $"{What} — grade {grade} {Wants}" : $"{What} — {Wants}";
}

/// <summary>
/// What the Commander's plans need an engineer for (list.md Phase 28).
/// <para>
/// <b>Read from the plans rather than from the checklist</b>, and that is the same call Phase 27
/// made for the gap: the plan owns <em>what</em> and the checklist owns <em>when</em>, so a
/// Commander who has not promoted anything yet still gets an answer about what they are building.
/// </para>
/// <para>
/// <b>Who can roll a thing is per grade, and the blueprint table says so per grade.</b> Six
/// engineers offer Increased FSD Range at grade 3 and three of them at grade 5, so deriving the
/// list from an engineer's top grade instead would send a Commander to Professor Palin for a
/// grade 5 drive he does not roll.
/// </para>
/// </summary>
public static class PlannedNeeds
{
    /// <summary>
    /// Every planned thing across both stores that wants an engineer, in the order the plans
    /// state them.
    /// <para>
    /// A slot naming an engineer outright is <b>that engineer and nobody else</b>: the Commander
    /// has an opinion, and answering it with a list of alternatives is answering a question they
    /// did not ask.
    /// </para>
    /// </summary>
    public static IReadOnlyList<PlannedWork> Of(
        IReadOnlyList<ShipBuild> ships,
        IReadOnlyList<OnFootBuild> onFoot)
    {
        var work = new List<PlannedWork>();

        foreach (var build in ships)
        {
            foreach (var slot in build.Slots.Where(slot => !slot.IsEmpty))
            {
                var what = $"{build.Describe()} · {slot.Slot}";

                if (slot.Blueprint is { Length: > 0 } blueprint)
                {
                    work.Add(new PlannedWork(
                        what,
                        blueprint,
                        slot.Grade,
                        Rollers(blueprint, slot.Module, slot.Grade, slot.Engineer)));
                }

                if (slot.Experimental is { Length: > 0 } experimental)
                {
                    work.Add(new PlannedWork(
                        what,
                        experimental,
                        null,
                        Rollers(experimental, slot.Module, null, slot.Engineer)));
                }
            }
        }

        foreach (var build in onFoot)
        {
            foreach (var slot in build.Slots.Where(slot => !slot.IsEmpty))
            {
                // A grade on foot is bought at Pioneer Supplies, not rolled by anybody — an
                // engineer's base does not sell one. So a grade-only slot wants nothing from this
                // directory, and counting it would put work on the list that no unlock can clear.
                if (slot.Modification is not { Length: > 0 } modification)
                {
                    continue;
                }

                work.Add(new PlannedWork(
                    $"{build.Describe()} · {slot.Slot}",
                    modification,
                    null,
                    Rollers(modification, build.Equipment, null, null)));
            }
        }

        return work;
    }

    /// <summary>
    /// Everybody who can roll one blueprint at one grade.
    /// <para>
    /// <b>Two sources, unioned, and the second one is not redundant.</b> The blueprint table
    /// carries the engineer list per grade and is the authority wherever it has a row — but the
    /// four Colonia on-foot engineers have no rows in it at all, and their modification lists live
    /// only in the directory. Reading the blueprint table alone tells a Colonia Commander that
    /// nobody within twenty-two thousand light years can do the thing three people next door do.
    /// </para>
    /// </summary>
    public static IReadOnlyList<string> Rollers(
        string wants,
        string? module,
        int? grade,
        string? named)
    {
        if (named is { Length: > 0 } chosen)
        {
            return EngineerDirectory.ByName(chosen) is { } engineer ? [engineer.Name] : [chosen];
        }

        var blueprints = BlueprintCatalogue.Named(wants, module);

        var matching = grade is { } wanted
            ? blueprints.Where(blueprint => blueprint.Grade == wanted || blueprint.Grade is null)
            : blueprints;

        var names = matching
            .SelectMany(blueprint => blueprint.Engineers)
            .Select(EngineerDirectory.ByName)
            .Where(engineer => engineer is not null)
            .Select(engineer => engineer!.Name);

        // The directory's own speciality list, which is how the Colonia four are reachable at all
        // and how a spoken module type — "thrusters" — finds the people who grade it.
        //
        // Matched here rather than through Catalogue.Match, and that is not a shortcut: the
        // matcher answers with **one** name, and the on-foot vocabulary spells the same
        // modification two ways across two sources — "Noise Suppressor" in the wiki list the four
        // Colonia engineers came from and "Noise suppressor" in EDEngineer's. Taking the matcher's
        // single answer keeps whichever spelling sorts first and silently drops everybody filed
        // under the other. The spoken path is still there, and is what a name nobody spells
        // exactly falls through to.
        var spelled = EngineerDirectory.All
            .SelectMany(engineer => engineer.Specialities.Select(speciality => (engineer, speciality)))
            .Where(match => string.Equals(match.speciality.Kind, wants, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var speciality = (spelled.Count > 0
                ? spelled
                : [.. EngineerDirectory.Grading(wants).Select(match => (engineer: match.Engineer, speciality: match.Speciality))])
            .Where(match => grade is not { } wanted
                            || !match.speciality.IsGraded
                            || match.speciality.MaxGrade >= wanted)
            .Select(match => match.engineer.Name);

        return [.. names.Concat(speciality).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
    }
}
