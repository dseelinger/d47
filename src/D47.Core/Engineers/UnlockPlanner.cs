using System.Globalization;
using System.Text;
using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Loadout;
using D47.Core.Ships;

namespace D47.Core.Engineers;

/// <summary>
/// One engineer worth unlocking next, what it would take, and what it would buy (Phase 28, "The fastest
/// way in").
/// </summary>
public sealed record UnlockCandidate
{
    public required Engineer Engineer { get; init; }

    public required UnlockChain Chain { get; init; }

    /// <summary>The planned things this unlock would make rollable that nobody rollable can do now.</summary>
    public IReadOnlyList<PlannedWork> Covers { get; init; } = [];

    /// <summary>
    /// The trip, in the one unit everything else converted into: jumps per planned thing covered, or
    /// light years where no jump range is known.
    /// </summary>
    public double Score { get; init; }

    /// <summary>Whether the score is jumps or light years, so the page never labels one as the other.</summary>
    public bool ScoredInJumps { get; init; }

    /// <summary>
    /// The ranking in one sentence — "One step, 84 ly, about 3 jumps, and 4 other planned things
    /// covered."
    /// </summary>
    public string Summary()
    {
        var said = new StringBuilder();

        said.Append(EngineerSay.Count(Chain.Steps.Count, "step", "steps"));

        if (Chain.LightYears is { } light)
        {
            said.Append(CultureInfo.InvariantCulture, $", {EngineerSay.Distance(light)}");
        }

        if (Chain.Jumps is { } jumps)
        {
            said.Append(CultureInfo.InvariantCulture, $", {EngineerSay.Jumps(jumps)}");
        }

        said.Append(Covers.Count switch
        {
            0 => ", covering nothing you have planned",
            1 => ", and 1 planned thing covered",
            _ => $", and {Covers.Count.ToString(CultureInfo.InvariantCulture)} planned things covered",
        });

        return said.Append('.').ToString();
    }

    /// <summary>The work behind the ranking, one line per stop, plus what cannot be counted in jumps.</summary>
    public IReadOnlyList<string> Working()
    {
        var lines = new List<string>();

        foreach (var step in Chain.Steps)
        {
            lines.Add(step.Describe());

            if (step.Meeting is { Length: > 0 } meeting)
            {
                lines.Add($"    first: {meeting}");
            }

            if (step.Tribute is { Length: > 0 } tribute)
            {
                lines.Add($"    hand over: {tribute}");
            }

            if (step.NeedsRanking)
            {
                lines.Add(
                    $"    grade {step.Grade.ToString(CultureInfo.InvariantCulture)} with them is "
                    + "rank you still have to earn");
            }
        }

        foreach (var covered in Covers)
        {
            lines.Add($"    covers: {covered.Describe()}");
        }

        return lines;
    }
}

/// <summary>Where every engineer stands, and which one to go and get next (Phase 28).</summary>
public sealed record EngineerReport
{
    /// <summary>Everybody, ordered by what the Commander can act on today.</summary>
    public IReadOnlyList<EngineerEntry> Directory { get; init; } = [];

    /// <summary>
    /// The unlocks worth making next, best first — and the rank climbs, which are the same question.
    /// </summary>
    public IReadOnlyList<UnlockCandidate> Route { get; init; } = [];

    /// <summary>Everything the plans want rolling.</summary>
    public IReadOnlyList<PlannedWork> Planned { get; init; } = [];

    /// <summary>How many planned things are waiting on somebody the Commander has not unlocked.</summary>
    public int Waiting { get; init; }

    /// <summary>Whether the journal has said anything about engineer progress yet.</summary>
    public bool ProgressKnown { get; init; }

    /// <summary>Where the distances are measured from, and null when the journal has not said.</summary>
    public string? From { get; init; }

    /// <summary>The jump range the ranking used, and null when nothing has reported one.</summary>
    public double? JumpRange { get; init; }

    public int Unlocked => Directory.Count(entry => entry.Reach == EngineerReach.Unlocked);

    public int WithinReach => Directory.Count(entry => entry.Reach == EngineerReach.WithinReach);

    /// <summary>The line at the top of the page: where they stand in the game's own three states.</summary>
    public string Summary()
    {
        if (!ProgressKnown)
        {
            return "No engineer progress yet — Elite writes it when you enter the game.";
        }

        var inProgress = Directory.Count(entry => entry.Standing is { IsUnlocked: false });
        var notStarted = Directory.Count(entry => entry.Standing is null);

        return $"{Unlocked.ToString(CultureInfo.InvariantCulture)} of "
               + $"{Directory.Count.ToString(CultureInfo.InvariantCulture)} unlocked. "
               + $"{inProgress.ToString(CultureInfo.InvariantCulture)} in progress. "
               + $"{notStarted.ToString(CultureInfo.InvariantCulture)} not started.";
    }
}

/// <summary>The engineer solver (Phase 28, "The fastest way in").</summary>
public static class UnlockPlanner
{
    /// <summary>
    /// The whole page: every engineer in the order the Commander can act on them, and the unlocks worth
    /// making next.
    /// </summary>
    public static EngineerReport Of(
        IReadOnlyList<ShipBuild> ships,
        IReadOnlyList<OnFootBuild> onFoot,
        CommanderGameState? state)
    {
        var progress = state?.Engineers;
        var from = state?.Location.StarPos;
        var range = state?.Ship.MaxJumpRange;
        var planned = PlannedNeeds.Of(ships, onFoot);
        var workload = EngineerWorkload.Outstanding(ships, onFoot, state);

        // What nobody the Commander can reach today can roll.
        var outstanding = planned.Where(work => !work.CanBeRolled(progress)).ToList();

        var directory = EngineerDirectory.All
            .Select(engineer => Entry(engineer, progress, from, range, workload))
            .OrderBy(entry => entry.Reach)
            .ThenByDescending(entry => entry.Wanted)
            .ThenBy(entry => entry.LightYears ?? double.MaxValue)
            .ThenBy(entry => entry.Engineer.Name, StringComparer.Ordinal)
            .ToList();

        return new EngineerReport
        {
            Directory = directory,
            Route = Rank(directory, outstanding, progress, from, range),
            Planned = planned,
            Waiting = planned.Count(work => work.WaitingOnAStranger(progress)),
            ProgressKnown = progress is { IsKnown: true },
            From = state?.Location.StarSystem,
            JumpRange = range,
        };
    }

    /// <summary>Everything about the Commander that moves this ranking, in one short string.</summary>
    public static string Stamp(CommanderGameState? state) =>
        state is null
            ? string.Empty
            : string.Join(
                '|',
                state.Location.StarSystem ?? string.Empty,
                state.Location.StarPos?.ToString() ?? string.Empty,
                state.Ship.MaxJumpRange?.ToString("R", CultureInfo.InvariantCulture) ?? string.Empty,
                state.Engineers.TakenAt?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty);

    /// <summary>
    /// The chain to one engineer as checklist items, in flying order (Phase 28, "The route promotes to
    /// the checklist as a chain rather than a line").
    /// </summary>
    public static IReadOnlyList<ChecklistItem> Items(UnlockChain chain, ChecklistScope scope)
    {
        var items = new List<ChecklistItem>();

        foreach (var step in chain.Steps)
        {
            var intent = new ChecklistIntent(ChecklistIntentKind.EngineerAccess, step.Engineer.Name)
            {
                Grade = step.Grade,
            };

            items.Add(new ChecklistItem
            {
                Key = ChecklistKeys.For(intent),
                Scope = scope,
                Kind = ChecklistItemKind.Derived,
                Source = ChecklistSource.EngineeringPlan,
                Text = step.Grade > 1
                    ? $"Rank {step.Grade.ToString(CultureInfo.InvariantCulture)} with {step.Engineer.Name}"
                    : $"Unlock {step.Engineer.Name} at {step.Engineer.Where}",
                Intent = intent,

                // Asserted: the chain and the grade come from the shipped table, and the standing that says
                // which stops are already behind them comes from their own journal.
                Provenance = ChecklistProvenance.Asserted,
            });
        }

        return items;
    }

    /// <summary>The best unlocks to make next.</summary>
    private static IReadOnlyList<UnlockCandidate> Rank(
        IReadOnlyList<EngineerEntry> directory,
        IReadOnlyList<PlannedWork> outstanding,
        EngineerProgressState? progress,
        StarPosition? from,
        double? range)
    {
        var candidates = new List<UnlockCandidate>();

        // Everybody, not only the ones still locked.
        foreach (var entry in directory)
        {
            var covers = outstanding
                .Where(work => EngineerDirectory.IsNamedIn(work.Engineers, entry.Engineer))
                .ToList();

            // The grade to aim for is the highest any covered plan asks of them.
            var grade = covers.Count == 0 ? 1 : covers.Max(work => work.Rank);
            var chain = EngineerAccess.ChainTo(entry.Engineer, grade, progress, from, range);

            if (chain.IsDone)
            {
                continue;
            }

            var cost = chain.Jumps is { } jumps ? jumps : chain.LightYears;

            candidates.Add(new UnlockCandidate
            {
                Engineer = entry.Engineer,
                Chain = chain,
                Covers = covers,
                Score = cost is { } known ? known / Math.Max(covers.Count, 1) : double.MaxValue,
                ScoredInJumps = chain.Jumps is not null,
            });
        }

        return
        [
            .. candidates
                .OrderByDescending(candidate => candidate.Covers.Count > 0)
                .ThenBy(candidate => candidate.Score)
                .ThenBy(candidate => candidate.Chain.OpenEnded)
                .ThenBy(candidate => candidate.Chain.Steps.Count)

                // Jumps round up, so two candidates a real distance apart tie on them all the time — 62 light
                // years and 73 are both three jumps at 30.
                .ThenBy(candidate => candidate.Chain.LightYears ?? double.MaxValue)
                .ThenBy(candidate => candidate.Engineer.Name, StringComparer.Ordinal),
        ];
    }

    private static EngineerEntry Entry(
        Engineer engineer,
        EngineerProgressState? progress,
        StarPosition? from,
        double? range,
        IReadOnlyDictionary<int, int> workload)
    {
        var light = engineer.DistanceFrom(from);

        return new EngineerEntry
        {
            Engineer = engineer,
            Reach = EngineerAccess.ReachOf(engineer, progress),
            Standing = progress?.For(engineer.Id),
            LightYears = light,
            Jumps = EngineerAccess.Jumps(light, range),
            Wanted = workload.GetValueOrDefault(engineer.Id),
            Chain = EngineerAccess.ChainTo(engineer, 1, progress, from, range),
            Criteria = EngineerAccess.CriteriaFor(engineer, progress),
        };
    }
}
