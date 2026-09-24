using System.Globalization;
using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Engineers;

/// <summary>What the Commander can do about an engineer today (Phase 28, "Who can roll this").</summary>
public enum EngineerReach
{
    /// <summary>
    /// Nobody stands between the Commander and this invitation — either it has already been offered, or
    /// nobody has to recommend them.
    /// </summary>
    WithinReach,

    /// <summary>Already unlocked.</summary>
    Unlocked,

    /// <summary>Behind somebody the Commander has not unlocked yet.</summary>
    Locked,
}

/// <summary>
/// One stop on the way to an unlock — somebody who has to be reached before the next one (Phase 28,
/// "The fastest way in").
/// </summary>
/// <param name="Engineer">Who this stop is.</param>
/// <param name="LightYears">
/// From the stop before it, or from where the Commander is standing for the first one.
/// </param>
/// <param name="Jumps">The same distance in jumps of the ship actually being flown.</param>
/// <param name="Grade">
/// The grade wanted with this engineer: the referral grade the next stop needs, or — at the end of a
/// chain — the grade the Commander's own plans ask for. 1 means the unlock alone.
/// </param>
/// <param name="Held">The grade they hold with them now. 0 when they are not unlocked.</param>
/// <param name="Meeting">
/// What still has to happen before the invitation is offered, in the game's own words, or null once it
/// has been.
/// </param>
/// <param name="Tribute">
/// What the invitation asks to be handed over, where it is a delivery.
/// </param>
public sealed record UnlockStep(
    Engineer Engineer,
    double? LightYears,
    int? Jumps,
    int Grade,
    int Held,
    string? Meeting,
    string? Tribute)
{
    /// <summary>Whether the trip is a shopping run rather than a visit.</summary>
    public bool IsDelivery => Tribute is { Length: > 0 };

    /// <summary>Whether this stop costs something that is not a trip.</summary>
    public bool IsOpenEnded => Meeting is { Length: > 0 };

    /// <summary>
    /// The reputation climb this stop wants on top of the unlock, priced where Frontier priced it.
    /// </summary>
    public bool NeedsRanking => Grade > Math.Max(Held, 1);

    /// <summary>One stop, as a Commander hears it.</summary>
    public string Describe() => Engineer.Name + Rest();

    /// <summary>
    /// The same stop with the name taken off the front, so a surface can draw the name as something
    /// pressable and the rest as ordinary text (remediation.md 12, item 7).
    /// </summary>
    public string Rest()
    {
        if (SystemSplit() is { } split)
        {
            return split.Before + split.System + split.After;
        }

        var standing = Held > 0
            ? $", grade {Held.ToString(CultureInfo.InvariantCulture)} of "
              + Grade.ToString(CultureInfo.InvariantCulture)
            : $" at {Engineer.Where}";

        return standing + EngineerSay.Trip(LightYears, Jumps);
    }

    /// <summary>
    /// <see cref="Rest"/> split around the system name, so a surface can draw a copy control beside it —
    /// null on a grade line, or where the engineer's system is not on record (#256).
    /// </summary>
    public (string Before, string System, string After)? SystemSplit()
    {
        if (Held > 0 || Engineer.System is not { Length: > 0 } system)
        {
            return null;
        }

        var before = Engineer.Station is { Length: > 0 } station ? $" at {station} in " : " at ";

        return (before, system, EngineerSay.Trip(LightYears, Jumps));
    }
}

/// <summary>
/// Everything still standing between the Commander and one engineer, in the order it has to happen
/// (Phase 28, "The fastest way in").
/// </summary>
public sealed record UnlockChain(IReadOnlyList<UnlockStep> Steps)
{
    public static readonly UnlockChain Done = new([]);

    /// <summary>Whether there is nothing left to do at the grade that was asked about.</summary>
    public bool IsDone => Steps.Count == 0;

    /// <summary>The whole flight, in jumps.</summary>
    public int? Jumps =>
        Steps.Count > 0 && Steps.All(step => step.Jumps is not null)
            ? Steps.Sum(step => step.Jumps!.Value)
            : null;

    /// <summary>The same flight in light years, on the same all-or-nothing rule.</summary>
    public double? LightYears =>
        Steps.Count > 0 && Steps.All(step => step.LightYears is not null)
            ? Steps.Sum(step => step.LightYears!.Value)
            : null;

    /// <summary>How many stops cost something that is not a trip.</summary>
    public int OpenEnded => Steps.Count(step => step.IsOpenEnded);

    /// <summary>How many of them are a shopping run.</summary>
    public int Deliveries => Steps.Count(step => step.IsDelivery);

    /// <summary>Who the chain ends at.</summary>
    public Engineer? Target => Steps.Count == 0 ? null : Steps[^1].Engineer;
}

/// <summary>
/// One thing that has to be true before an engineer will work for the Commander, and whether it is
/// (remediation.md 13, item 12).
/// </summary>
/// <param name="Text">The criterion, in the game's own words where they exist.</param>
/// <param name="Met">
/// True where the journal settles it, false where the journal settles it the other way, and null where
/// nothing d47 can read decides it.
/// </param>
public sealed record UnlockCriterion(string Text, bool? Met)
{
    /// <summary>d47's own words about the reading behind <see cref="Met"/>, kept apart from Frontier's prose.</summary>
    public string? Reading { get; init; }

    /// <summary>The number and target behind this criterion, where the test is one that has them (#17).</summary>
    public UnlockMeasure? Measure { get; init; }
}

/// <summary>
/// The current figure and target behind a graded criterion, so a bar can draw under it (#17).
/// </summary>
/// <param name="Current">The figure read from the journal or the save.</param>
/// <param name="Target">The threshold the test asks for — <see cref="ReputationBands.Floor"/> or
/// <see cref="ReputationBands.Ceiling"/> for a reputation test, <c>AtLeast</c> otherwise.</param>
/// <param name="IsCeiling">Whether staying under <see cref="Target"/> is what is wanted, rather than
/// reaching it.</param>
public sealed record UnlockMeasure(double Current, double Target, bool IsCeiling)
{
    /// <summary>
    /// How full the bar reads, 0 to 1. A ceiling test's current and target both come off the same signed
    /// reputation scale, so the plain ratio already runs the right way — fuller the lower <see
    /// cref="Current"/> sits under a negative <see cref="Target"/>.
    /// </summary>
    public double Fill => Target == 0 || !double.IsFinite(Current / Target)
        ? 0
        : Math.Clamp(Current / Target, 0, 1);
}

/// <summary>
/// How far along the Commander is with one engineer, and what the way in looks like from where they are
/// standing (Phase 28, "Who can roll this").
/// </summary>
public sealed record EngineerEntry
{
    public required Engineer Engineer { get; init; }

    public required EngineerReach Reach { get; init; }

    /// <summary>Their own journal's word for it, or null where it has never mentioned them.</summary>
    public EngineerStanding? Standing { get; init; }

    /// <summary>Light years from where the Commander is, or null when either end is unknown.</summary>
    public double? LightYears { get; init; }

    /// <summary>The same in jumps of the ship being flown, or null when the range is unknown.</summary>
    public int? Jumps { get; init; }

    /// <summary>How many modules still to engineer this engineer could finish (#137).</summary>
    public int Wanted { get; init; }

    /// <summary>
    /// The direct dependants this engineer's referral gates that the Commander's plans want, not yet
    /// opened (#138).
    /// </summary>
    public IReadOnlyList<Engineer> Gate { get; init; } = [];

    /// <summary>The planned things this engineer can roll, whether or not they are unlocked yet (#109).</summary>
    public IReadOnlyList<PlannedWork> Planned { get; init; } = [];

    /// <summary>The line about it, or null where the gate draws nothing (#138).</summary>
    public string? GateLine =>
        Gate.Count == 0
            ? null
            : string.Join(
                "; ",
                Gate.GroupBy(dependant => (dependant.IsOnFoot, Grade: EngineerAccess.ReferralGradeFor(dependant)))
                    .Select(group => (group.Key.IsOnFoot
                                         ? "unlocking them opens "
                                         : $"grade {group.Key.Grade.ToString(CultureInfo.InvariantCulture)} opens ")
                                     + EngineerSay.List([.. group.Select(dependant => dependant.Name)])));

    /// <summary>What is still to be done to reach them.</summary>
    public UnlockChain Chain { get; init; } = UnlockChain.Done;

    /// <summary>
    /// Everything that has to be true before they will work, with the ones already true marked
    /// (remediation.md 13, item 12).
    /// </summary>
    public IReadOnlyList<UnlockCriterion> Criteria { get; init; } = [];

    /// <summary>The right-hand note: where they are, and how far that is.</summary>
    public string Aside =>
        Jumps is { } jumps
            ? $"{EngineerSay.Distance(LightYears)}, {EngineerSay.Jumps(jumps)}"
            : LightYears is { } light
                ? EngineerSay.Distance(light)
                : Engineer.System ?? "somewhere I do not have on record";

    /// <summary>Their standing in one clause, whether or not the journal has said anything.</summary>
    public string Status => Standing?.Describe() ?? "not met";

    /// <summary>What they grade, in the order the table states — best grade first, as one spoken clause.</summary>
    public string Specialities =>
        Engineer.Specialities.Count == 0
            ? "nothing I have a record of"
            : string.Join(", ", Engineer.Specialities.Select(speciality => speciality.IsGraded
                ? $"{speciality.Kind} to {speciality.MaxGrade.ToString(CultureInfo.InvariantCulture)}"
                : speciality.Kind));

    /// <summary>The same list, one entry at a time (remediation.md 16, item 6).</summary>
    public IReadOnlyList<string> SpecialityLines =>
    [
        .. Engineer.Specialities.Select(speciality => speciality.IsGraded
            ? $"{speciality.Kind} (G{speciality.MaxGrade.ToString(CultureInfo.InvariantCulture)})"
            : speciality.Kind),
    ];
}

/// <summary>Where each engineer stands with this Commander, and what the way in costs (Phase 28).</summary>
public static class EngineerAccess
{
    /// <summary>
    /// The role a <see cref="ChecklistIntentKind.EngineerPrerequisite"/> item keys on, rather than the
    /// criterion's wording — so a table change to the prose does not orphan the item (#257).
    /// </summary>
    public const string InvitationRole = "invitation";

    /// <summary>The tribute's role, on the same footing as <see cref="InvitationRole"/> (#257).</summary>
    public const string TributeRole = "tribute";

    /// <summary>
    /// One checklist item for every unmet line of <see cref="CriteriaFor"/> — a referral as the existing
    /// <see cref="ChecklistIntentKind.EngineerAccess"/> intent, and the invitation or tribute as <see
    /// cref="ChecklistIntentKind.EngineerPrerequisite"/> (#257).
    /// </summary>
    public static IReadOnlyList<ChecklistItem> UnmetPrerequisites(Engineer engineer, UnlockEvidence evidence)
    {
        var criteria = CriteriaFor(engineer, evidence);
        var wanted = ReferralGradeFor(engineer);
        var items = new List<ChecklistItem>();

        for (var index = 0; index < criteria.Count; index++)
        {
            if (criteria[index].Met == true)
            {
                continue;
            }

            items.Add(index < engineer.ReferredBy.Count
                ? ReferralItem(engineer.ReferredBy[index], wanted)
                : PrerequisiteItem(engineer, Role(engineer, index), criteria[index]));
        }

        return items;
    }

    /// <summary>Whether the criterion at this index is the invitation or the tribute (#257).</summary>
    private static string Role(Engineer engineer, int index) =>
        index == engineer.ReferredBy.Count && engineer.Meeting is { Length: > 0 }
            ? InvitationRole
            : TributeRole;

    private static ChecklistItem ReferralItem(string referrerName, int grade)
    {
        var referrer = EngineerDirectory.ByName(referrerName);
        var intent = new ChecklistIntent(ChecklistIntentKind.EngineerAccess, referrerName) { Grade = grade };

        return new ChecklistItem
        {
            Key = ChecklistKeys.For(intent),
            Scope = ChecklistScope.Universal,
            Kind = ChecklistItemKind.Derived,
            Source = ChecklistSource.EngineerPrerequisite,
            Text = grade > 1
                ? $"Rank {grade.ToString(CultureInfo.InvariantCulture)} with {referrerName}"
                : referrer is { Where: { Length: > 0 } where }
                    ? $"Unlock {referrerName} at {where}"
                    : $"Unlock {referrerName}",
            Intent = intent,
            Provenance = ChecklistProvenance.Asserted,
        };
    }

    private static ChecklistItem PrerequisiteItem(Engineer engineer, string role, UnlockCriterion criterion)
    {
        var intent = new ChecklistIntent(ChecklistIntentKind.EngineerPrerequisite, engineer.Name) { Detail = role };

        return new ChecklistItem
        {
            Key = ChecklistKeys.For(intent),
            Scope = ChecklistScope.Universal,
            Kind = ChecklistItemKind.Derived,
            Source = ChecklistSource.EngineerPrerequisite,
            Text = criterion.Text,
            Intent = intent,
            Provenance = ChecklistProvenance.Asserted,
        };
    }

    /// <summary>
    /// The prerequisites for reaching one engineer, with the parts already done marked (remediation.md
    /// 13, item 12), read where the game states a test for one and folded from the shipped prose
    /// otherwise (#183).
    /// </summary>
    public static IReadOnlyList<UnlockCriterion> CriteriaFor(Engineer engineer, UnlockEvidence evidence)
    {
        var standing = evidence.Progress?.For(engineer.Id);
        var unlocked = standing?.IsUnlocked == true;
        var criteria = new List<UnlockCriterion>();

        var anyReferralMet = engineer.ReferredBy
            .Any(referrer => EngineerDirectory.ByName(referrer) is { } known
                             && ReferralMet(engineer, evidence.Progress?.For(known.Id)));

        foreach (var referrer in engineer.ReferredBy)
        {
            var wanted = ReferralGradeFor(engineer);
            var held = EngineerDirectory.ByName(referrer) is { } known
                ? evidence.Progress?.For(known.Id)
                : null;

            var met = unlocked || ReferralMet(engineer, held) || (engineer.IsOnFoot && anyReferralMet);
            var ask = engineer.IsOnFoot
                ? $"Unlock {referrer}"
                : $"Grade {wanted.ToString(CultureInfo.InvariantCulture)} with {referrer}";

            criteria.Add(new UnlockCriterion(
                engineer.ReferredBy.Count > 1 ? $"{ask} (any one of the referrals will do)." : $"{ask}.",
                met));
        }

        if (engineer.Meeting is { Length: > 0 } meeting)
        {
            // Their invitation task, in Frontier's words. Evaluated even once already invited or unlocked, so
            // a criterion that has a measure still carries one and draws a full bar (#17).
            var (evalMet, reading, measure) = Evaluate(engineer.MeetingTest, engineer, evidence);
            var met = unlocked || standing?.IsInvited == true ? (bool?)true : evalMet;

            criteria.Add(new UnlockCriterion(meeting, met) { Reading = met == true ? null : reading, Measure = measure });
        }

        if (engineer.Unlock is { Length: > 0 } tribute)
        {
            var (evalMet, reading, measure) = Evaluate(engineer.UnlockTest, engineer, evidence);
            var met = unlocked ? (bool?)true : evalMet;

            criteria.Add(new UnlockCriterion(tribute, met) { Reading = met == true ? null : reading, Measure = measure });
        }

        return criteria;
    }

    /// <summary>One structured test read against the evidence, and d47's own words about the reading.</summary>
    private static (bool? Met, string? Reading, UnlockMeasure? Measure) Evaluate(
        UnlockTest? test, Engineer engineer, UnlockEvidence evidence) => test switch
    {
        UnlockTest.Rank rank => RankResult(rank, evidence),
        UnlockTest.Reputation reputation => ReputationResult(reputation, evidence),
        UnlockTest.Statistic statistic => StatisticResult(statistic, evidence),
        UnlockTest.Contribution contribution => ContributionResult(contribution, engineer, evidence),
        _ => (null, null, null),
    };

    private static (bool? Met, string? Reading, UnlockMeasure? Measure) RankResult(
        UnlockTest.Rank test, UnlockEvidence evidence)
    {
        var standing = evidence.Ranks?.For(test.Career);

        return standing is null
            ? (null, null, null)
            : (standing.Rank >= test.AtLeast, null, new UnlockMeasure(standing.Rank, test.AtLeast, false));
    }

    /// <summary>Whatever reading exists, however old, with its date shown once it predates the session.</summary>
    private static (bool? Met, string? Reading, UnlockMeasure? Measure) ReputationResult(
        UnlockTest.Reputation test, UnlockEvidence evidence)
    {
        var reading = evidence.Reputation?.Reading(test.Faction);

        if (reading is null)
        {
            return (null, null, null);
        }

        var target = test.AtMost ? ReputationBands.Ceiling(test.Band) : ReputationBands.Floor(test.Band);
        var met = test.AtMost ? reading.MyReputation < target : reading.MyReputation >= target;

        var stale = evidence.SessionStart is { } start && reading.SeenAt < start;

        return (
            met,
            stale ? $"as of {reading.SeenAt.ToString("d MMM yyyy", CultureInfo.InvariantCulture)}" : null,
            new UnlockMeasure(reading.MyReputation, target, test.AtMost));
    }

    /// <summary>Only ever a floor: a Statistics reading below it says nothing the game did not also let pass.</summary>
    private static (bool? Met, string? Reading, UnlockMeasure? Measure) StatisticResult(
        UnlockTest.Statistic test, UnlockEvidence evidence)
    {
        var value = evidence.Statistics?.Read(test.Path);

        return value switch
        {
            null => (null, null, null),
            var reached when reached >= test.AtLeast =>
                (true, null, new UnlockMeasure(reached.Value, test.AtLeast, false)),
            var short_ => (
                null,
                $"Last reported {short_.Value.ToString("N0", CultureInfo.InvariantCulture)}",
                new UnlockMeasure(short_.Value, test.AtLeast, false)),
        };
    }

    private static (bool? Met, string? Reading, UnlockMeasure? Measure) ContributionResult(
        UnlockTest.Contribution test, Engineer engineer, UnlockEvidence evidence)
    {
        var total = evidence.Contributions?.Total(engineer.Id, test.Type, test.Symbol);

        return total switch
        {
            null => (null, null, null),
            var reached when reached >= test.Quantity =>
                (true, null, new UnlockMeasure(reached.Value, test.Quantity, false)),
            var short_ => (
                false,
                $"{short_.Value.ToString(CultureInfo.InvariantCulture)} of "
                + $"{test.Quantity.ToString(CultureInfo.InvariantCulture)} handed over",
                new UnlockMeasure(short_.Value, test.Quantity, false)),
        };
    }

    /// <summary>
    /// The engineers this one refers into, whose plans name them and whose referral through this
    /// engineer is not yet satisfied — direct dependants only, ordered by name (#138).
    /// </summary>
    public static IReadOnlyList<Engineer> Gate(
        Engineer engineer, EngineerProgressState? progress, IReadOnlyList<PlannedWork> planned)
    {
        var standing = progress?.For(engineer.Id);

        return
        [
            .. EngineerDirectory.All
                .Where(dependant => dependant.ReferredBy
                    .Any(name => EngineerDirectory.ByName(name)?.Id == engineer.Id))
                .Where(dependant => planned.Any(work => EngineerDirectory.IsNamedIn(work.Engineers, dependant)))
                .Where(dependant => !ReferralMet(dependant, standing))
                .OrderBy(dependant => dependant.Name, StringComparer.Ordinal),
        ];
    }

    /// <summary>
    /// The grade a referrer must hold to introduce this engineer: 1 for an on-foot engineer, whose
    /// referrers have no rank and refer on being unlocked.
    /// </summary>
    public static int ReferralGradeFor(Engineer dependant) =>
        dependant.IsOnFoot ? 1 : dependant.ReferralGrade ?? EngineeringRules.ReferralGrade;

    /// <summary>Whether the Commander's standing with one referrer earns this engineer's referral.</summary>
    public static bool ReferralMet(Engineer dependant, EngineerStanding? referrer) =>
        referrer is { IsUnlocked: true }
        && (dependant.IsOnFoot || (referrer.Rank ?? 0) >= ReferralGradeFor(dependant));

    /// <summary>Whether the Commander can act on this engineer today, from their journal and the table.</summary>
    public static EngineerReach ReachOf(Engineer engineer, EngineerProgressState? progress)
    {
        var standing = progress?.For(engineer.Id);

        if (standing is { IsUnlocked: true })
        {
            return EngineerReach.Unlocked;
        }

        if (standing is { IsInvited: true } || !engineer.NeedsReferral)
        {
            return EngineerReach.WithinReach;
        }

        return Referrer(engineer, progress) is not null
            ? EngineerReach.WithinReach
            : EngineerReach.Locked;
    }

    /// <summary>The referrer who has already earned this engineer their introduction, or null.</summary>
    public static Engineer? Referrer(Engineer engineer, EngineerProgressState? progress)
    {
        var needed = engineer.ReferralGrade ?? 1;

        return engineer.ReferredBy
            .Select(EngineerDirectory.ByName)
            .Where(referrer => referrer is not null
                               && progress?.For(referrer.Id) is { IsUnlocked: true } standing
                               && Math.Max(standing.Rank ?? 1, 1) >= needed)
            .OrderByDescending(referrer => progress!.For(referrer!.Id)!.Rank ?? 0)
            .FirstOrDefault();
    }

    /// <summary>
    /// The stops between the Commander and one engineer at one grade, in flying order, or <see
    /// cref="UnlockChain.Done"/> when there is nothing left to do.
    /// </summary>
    public static UnlockChain ChainTo(
        Engineer engineer,
        int grade,
        EngineerProgressState? progress,
        StarPosition? from,
        double? jumpRange)
    {
        var walked = new List<(Engineer Engineer, int Grade)>();
        var at = engineer;
        var wanted = Math.Max(grade, 1);

        // Bounded by the directory rather than by a counter: every engineer appears at most once, so a table
        // that ever grew a cycle stops here instead of hanging the panel.
        var seen = new HashSet<int>();

        while (at is not null && seen.Add(at.Id))
        {
            var held = Held(at, progress);

            if (held >= wanted)
            {
                break;
            }

            walked.Add((at, wanted));

            if (held > 0 || !at.NeedsReferral || Referrer(at, progress) is not null)
            {
                // Either they are unlocked and only the rank is short, or the introduction has already been
                // earned.
                break;
            }

            // The referrer with the shortest way in, then the nearest of those — the same two questions the
            // ranking asks, asked one link at a time.
            var next = at.ReferredBy
                .Select(EngineerDirectory.ByName)
                .Where(referrer => referrer is not null)
                .OrderBy(referrer => Depth(referrer!, progress, 0))
                .ThenBy(referrer => referrer!.DistanceFrom(from) ?? double.MaxValue)
                .ThenBy(referrer => referrer!.Name, StringComparer.Ordinal)
                .FirstOrDefault();

            wanted = ReferralGradeFor(at);
            at = next;
        }

        if (walked.Count == 0)
        {
            return UnlockChain.Done;
        }

        walked.Reverse();

        var steps = new List<UnlockStep>();
        var here = from;

        foreach (var (stop, want) in walked)
        {
            var standing = progress?.For(stop.Id);
            var held = Held(stop, progress);
            var light = stop.DistanceFrom(here);

            steps.Add(new UnlockStep(
                stop,
                light,
                Jumps(light, jumpRange),
                want,
                held,

                // Invited, unlocked, or nothing published: three different reasons for there being no
                // open-ended requirement left, and all three genuinely are none.
                standing is { IsInvited: true } || held > 0 ? null : stop.Meeting,
                held > 0 ? null : stop.UnlockCost));

            here = stop.Position ?? here;
        }

        return new UnlockChain(steps);
    }

    /// <summary>
    /// A distance as the ship being flown would cross it, rounded up — a jump is indivisible, so 84
    /// light years at 30 is three jumps and not 2.8.
    /// </summary>
    public static int? Jumps(double? lightYears, double? jumpRange) =>
        lightYears is { } light && jumpRange is { } range && range > 0
            ? (int)Math.Ceiling(light / range)
            : null;

    /// <summary>The grade the Commander holds with an engineer, and 0 for one they have not unlocked.</summary>
    private static int Held(Engineer engineer, EngineerProgressState? progress) =>
        progress?.For(engineer.Id) is { IsUnlocked: true } standing
            ? Math.Max(standing.Rank ?? 1, 1)
            : 0;

    /// <summary>How many unlocks deep an engineer still is.</summary>
    private static int Depth(Engineer engineer, EngineerProgressState? progress, int guard)
    {
        if (guard > EngineerDirectory.All.Count)
        {
            return guard;
        }

        if (Held(engineer, progress) > 0)
        {
            return 0;
        }

        if (!engineer.NeedsReferral || Referrer(engineer, progress) is not null)
        {
            return 1;
        }

        var behind = engineer.ReferredBy
            .Select(EngineerDirectory.ByName)
            .Where(referrer => referrer is not null)
            .Select(referrer => Depth(referrer!, progress, guard + 1))
            .DefaultIfEmpty(guard)
            .Min();

        return behind + 1;
    }
}
