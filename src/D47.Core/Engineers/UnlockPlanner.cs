using System.Globalization;
using System.Text;
using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Loadout;
using D47.Core.Ships;

namespace D47.Core.Engineers;

/// <summary>
/// One engineer worth unlocking next, what it would take, and what it would buy (Phase 28,
/// "The fastest way in").
/// </summary>
public sealed record UnlockCandidate
{
    public required Engineer Engineer { get; init; }

    public required UnlockChain Chain { get; init; }

    /// <summary>The planned things this unlock would make rollable that nobody rollable can do now.</summary>
    public IReadOnlyList<PlannedWork> Covers { get; init; } = [];

    /// <summary>
    /// The trip, in the one unit everything else converted into: jumps per planned thing covered,
    /// or light years where no jump range is known. Lower is better; <see cref="double.MaxValue"/>
    /// where the distance itself is unknown.
    /// </summary>
    public double Score { get; init; }

    /// <summary>Whether the score is jumps or light years, so the page never labels one as the other.</summary>
    public bool ScoredInJumps { get; init; }

    /// <summary>
    /// The ranking in one sentence — "One step, 84 ly, about 3 jumps, and 4 other planned things
    /// covered."
    /// <para>
    /// <b>This is the item's whole point.</b> A ranking nobody can inspect is an oracle, and when
    /// it is wrong — or when the Commander would rather go somewhere else anyway — they cannot
    /// tell a bad answer from a bug.
    /// </para>
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

    /// <summary>
    /// The work behind the ranking, one line per stop, plus what cannot be counted in jumps.
    /// <para>
    /// The open-ended requirements are listed <b>as the game words them</b> and never folded into
    /// the score: a combat rank is not a trip, and the Commander is the one who can weigh it
    /// against a flight.
    /// </para>
    /// </summary>
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

/// <summary>
/// Where every engineer stands, and which one to go and get next (Phase 28).
/// </summary>
public sealed record EngineerReport
{
    /// <summary>Everybody, ordered by what the Commander can act on today.</summary>
    public IReadOnlyList<EngineerEntry> Directory { get; init; } = [];

    /// <summary>
    /// The unlocks worth making next, best first — and the rank climbs, which are the same
    /// question. An engineer held at grade 2 with a grade 5 blueprint planned on them is as much
    /// in the way as one nobody has met.
    /// </summary>
    public IReadOnlyList<UnlockCandidate> Route { get; init; } = [];

    /// <summary>Everything the plans want rolling.</summary>
    public IReadOnlyList<PlannedWork> Planned { get; init; } = [];

    /// <summary>
    /// How many planned things are waiting on somebody the Commander has not unlocked.
    /// <para>
    /// <b>The count that belongs to the other tab.</b> It is the one number this page owes the
    /// Loadout one, and it is the reason the directory exists at all — a plan blocked on a person
    /// is not a plan blocked on materials, and the gap analysis cannot say so.
    /// </para>
    /// </summary>
    public int Waiting { get; init; }

    /// <summary>Whether the journal has said anything about engineer progress yet.</summary>
    public bool ProgressKnown { get; init; }

    /// <summary>Where the distances are measured from, and null when the journal has not said.</summary>
    public string? From { get; init; }

    /// <summary>The jump range the ranking used, and null when nothing has reported one.</summary>
    public double? JumpRange { get; init; }

    public int Unlocked => Directory.Count(entry => entry.Reach == EngineerReach.Unlocked);

    public int WithinReach => Directory.Count(entry => entry.Reach == EngineerReach.WithinReach);

    /// <summary>The line at the top of the page: where they stand, and what is blocked on a person.</summary>
    public string Summary()
    {
        if (!ProgressKnown)
        {
            return "No engineer progress yet — Elite writes it when you enter the game.";
        }

        var said = new StringBuilder(
            $"{Unlocked.ToString(CultureInfo.InvariantCulture)} of "
            + $"{Directory.Count.ToString(CultureInfo.InvariantCulture)} unlocked, "
            + $"{EngineerSay.Count(WithinReach, "within reach", "within reach")}.");

        if (Waiting > 0)
        {
            var waiting = EngineerSay.Count(Waiting, "planned thing is", "planned things are");

            said.Append(CultureInfo.InvariantCulture,
                $" {waiting} waiting on somebody you have not unlocked.");
        }
        else if (Planned.Count > 0)
        {
            said.Append(" Nothing you have planned is waiting on an engineer you have not unlocked.");
        }

        return said.ToString();
    }
}

/// <summary>
/// The engineer solver (Phase 28, "The fastest way in").
/// <para>
/// <b>A solver rather than a display, and it has to show its work.</b> Walking one engineer's
/// referral chain is exact and cheap — <c>referred_by</c> is a single parent — and it answers the
/// wrong question. The real one is <em>how do I get everything my plans need</em>, and two things
/// make that harder than a walk: a blueprint usually lists several engineers, and one unlock
/// covers many plans. Marsha Hicks rolls multi-cannons, cannons, fragment cannons, fuel scoops,
/// refineries and four limpet controllers, all at grade 5. So the best next unlock is the one that
/// satisfies the most of what is planned, not the shortest chain.
/// </para>
/// <para>
/// <b>One unit, and it is jumps.</b> Distance converts to jumps at the range of the ship actually
/// being flown, and each stop on a chain carries its own leg — so a long chain of short hops and a
/// single long haul are compared on the same scale rather than balanced by a tuning constant.
/// <b>Colonia then needs no rule of its own</b>: 22,000 light years is hundreds of jumps and
/// swamps any step count, and because the distance is measured from where the Commander is
/// standing, being in Colonia flips it automatically.
/// </para>
/// <para>
/// <b>What is not a trip is not converted into one.</b> A tribute of fifty units is a shopping run
/// to a known system and is already inside the leg that reaches it; a combat rank is not a trip at
/// all, so it is carried as the game's own sentence, breaks ties, and never becomes a number.
/// Distance stays the primary key deliberately — making open-endedness a class above it would put
/// an already-invited Colonia engineer ahead of a Bubble one and undo the thing Colonia needed no
/// rule for.
/// </para>
/// <para>
/// Core-pure: no clock, no thread, no file, no network. Everything is handed in, so the replay
/// harness can drive it.
/// </para>
/// </summary>
public static class UnlockPlanner
{
    /// <summary>
    /// The whole page: every engineer in the order the Commander can act on them, and the unlocks
    /// worth making next.
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

        // What nobody the Commander can reach today can roll. Everything else is already answered
        // by somebody they have, so an unlock that covers it buys them nothing.
        var outstanding = planned.Where(work => !work.CanBeRolled(progress)).ToList();

        var directory = EngineerDirectory.All
            .Select(engineer => Entry(engineer, progress, from, range, planned))
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

    /// <summary>
    /// Everything about the Commander that moves this ranking, in one short string.
    /// <para>
    /// <b>So a page can ask whether to redraw without computing the answer first.</b> Where they
    /// are, what they are flying and how they stand with everybody are the whole of the game-state
    /// half; the plans are the other half and both stores already raise an event when they change.
    /// A page that redrew on every tick instead would rebuild thirty-eight rows a second, and a
    /// rebuilt control has no bounds until the next layout pass — so a ray aimed at a row would
    /// land on a different one.
    /// </para>
    /// </summary>
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
    /// The chain to one engineer as checklist items, in flying order (Phase 28, "The route
    /// promotes to the checklist as a chain rather than a line").
    /// <para>
    /// <b>One item per stop</b>, which is what <see cref="EngineeringPlan"/> already does when it
    /// emits an access step beside a modification — and the reason is the same: each stop is its
    /// own next action, and a single line reading "unlock Broo Tarquin" hides two engineers and
    /// two rank climbs behind a tick nobody can make progress on.
    /// </para>
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

                // Asserted: the chain and the grade come from the shipped table, and the standing
                // that says which stops are already behind them comes from their own journal.
                Provenance = ChecklistProvenance.Asserted,
            });
        }

        return items;
    }

    /// <summary>
    /// The best unlocks to make next.
    /// <para>
    /// Ordered by whether it covers anything planned, then by the trip per thing covered, then by
    /// how much of it cannot be counted in jumps at all. With nothing planned the first key is
    /// dead and the answer collapses to "who is nearest", which is the right answer to a question
    /// with no plans behind it.
    /// </para>
    /// </summary>
    private static IReadOnlyList<UnlockCandidate> Rank(
        IReadOnlyList<EngineerEntry> directory,
        IReadOnlyList<PlannedWork> outstanding,
        EngineerProgressState? progress,
        StarPosition? from,
        double? range)
    {
        var candidates = new List<UnlockCandidate>();

        // Everybody, not only the ones still locked. An engineer already unlocked at grade 2 with a
        // grade 5 blueprint planned on them is a real thing standing between the Commander and
        // their plans, and it drops out on its own below: a chain to a grade they already hold is
        // an empty chain.
        foreach (var entry in directory)
        {
            var covers = outstanding
                .Where(work => EngineerDirectory.IsNamedIn(work.Engineers, entry.Engineer))
                .ToList();

            // The grade to aim for is the highest any covered plan asks of them. Unlocking buys
            // grade 1, so a chain that stops there when a grade 5 blueprint is planned would
            // report an unlock as finishing something it has only started.
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

                // Jumps round up, so two candidates a real distance apart tie on them all the
                // time — 62 light years and 73 are both three jumps at 30. The light years break
                // it, and only then the name, which is there to keep the order deterministic
                // rather than to mean anything.
                .ThenBy(candidate => candidate.Chain.LightYears ?? double.MaxValue)
                .ThenBy(candidate => candidate.Engineer.Name, StringComparer.Ordinal),
        ];
    }

    private static EngineerEntry Entry(
        Engineer engineer,
        EngineerProgressState? progress,
        StarPosition? from,
        double? range,
        IReadOnlyList<PlannedWork> planned)
    {
        var light = engineer.DistanceFrom(from);

        return new EngineerEntry
        {
            Engineer = engineer,
            Reach = EngineerAccess.ReachOf(engineer, progress),
            Standing = progress?.For(engineer.Id),
            LightYears = light,
            Jumps = EngineerAccess.Jumps(light, range),
            Wanted = planned.Count(work => EngineerDirectory.IsNamedIn(work.Engineers, engineer)),
            Chain = EngineerAccess.ChainTo(engineer, 1, progress, from, range),
            Criteria = EngineerAccess.CriteriaFor(engineer, progress),
        };
    }
}
