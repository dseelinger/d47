using System.Globalization;
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
        var standing = Held > 0
            ? $", grade {Held.ToString(CultureInfo.InvariantCulture)} of "
              + Grade.ToString(CultureInfo.InvariantCulture)
            : $" at {Engineer.Where}";

        return standing + EngineerSay.Trip(LightYears, Jumps);
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
    /// <summary>The mark in front of it.</summary>
    public string Mark => Met switch
    {
        true => "✓",
        false => "·",
        _ => "?",
    };

    /// <summary>The line as a surface with no columns draws it.</summary>
    public string Describe() => $"{Mark} {Text}";
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
    /// The prerequisites for reaching one engineer, with the parts already done marked (remediation.md
    /// 13, item 12).
    /// </summary>
    public static IReadOnlyList<UnlockCriterion> CriteriaFor(
        Engineer engineer, EngineerProgressState? progress)
    {
        var standing = progress?.For(engineer.Id);
        var unlocked = standing?.IsUnlocked == true;
        var criteria = new List<UnlockCriterion>();

        foreach (var referrer in engineer.ReferredBy)
        {
            var wanted = engineer.ReferralGrade ?? EngineeringRules.ReferralGrade;
            var held = EngineerDirectory.ByName(referrer) is { } known
                ? progress?.For(known.Id)
                : null;

            var met = unlocked
                      || (held is { IsUnlocked: true } && (held.Rank ?? 0) >= wanted);

            criteria.Add(new UnlockCriterion(
                engineer.ReferredBy.Count > 1
                    ? $"Grade {wanted.ToString(CultureInfo.InvariantCulture)} with {referrer} "
                      + "(any one of the referrals will do)"
                    : $"Grade {wanted.ToString(CultureInfo.InvariantCulture)} with {referrer}",
                met));
        }

        if (engineer.Meeting is { Length: > 0 } meeting)
        {
            // Their invitation task, in Frontier's words.
            criteria.Add(new UnlockCriterion(
                meeting,
                unlocked || standing?.IsInvited == true ? true : null));
        }

        if (engineer.Unlock is { Length: > 0 } tribute)
        {
            criteria.Add(new UnlockCriterion(tribute, unlocked ? true : null));
        }

        return criteria;
    }

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

            wanted = at.ReferralGrade ?? EngineeringRules.ReferralGrade;
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
