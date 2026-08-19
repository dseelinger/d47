using System.Globalization;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Engineers;

/// <summary>
/// What the Commander can do about an engineer today (list.md Phase 28, "Who can roll this").
/// <para>
/// The order of these values <b>is</b> the order the directory sorts in, so it is declared once
/// here rather than in a comparer somewhere else: the question is nearly always <em>who can I go
/// and get</em>, and alphabetical answers that for nobody.
/// </para>
/// </summary>
public enum EngineerReach
{
    /// <summary>
    /// Nobody stands between the Commander and this invitation — either it has already been
    /// offered, or nobody has to recommend them. What is left may still be a rank or a delivery,
    /// but it is theirs to go and do.
    /// </summary>
    WithinReach,

    /// <summary>Already unlocked. Useful, and not a thing to go and get.</summary>
    Unlocked,

    /// <summary>Behind somebody the Commander has not unlocked yet.</summary>
    Locked,
}

/// <summary>
/// One stop on the way to an unlock — somebody who has to be reached before the next one
/// (list.md Phase 28, "The fastest way in").
/// </summary>
/// <param name="Engineer">Who this stop is.</param>
/// <param name="LightYears">
/// From the stop before it, or from where the Commander is standing for the first one. Null where
/// either end has no coordinate, which is a distance nobody knows rather than a short one.
/// </param>
/// <param name="Jumps">
/// The same distance in jumps of the ship actually being flown. <b>The unit the ranking is
/// expressed in</b>, and null when no jump range is known — never guessed, because a guessed range
/// produces a wrong order that looks exactly like a right one.
/// </param>
/// <param name="Grade">
/// The grade wanted with this engineer: the referral grade the next stop needs, or — at the end of
/// a chain — the grade the Commander's own plans ask for. 1 means the unlock alone.
/// </param>
/// <param name="Held">The grade they hold with them now. 0 when they are not unlocked.</param>
/// <param name="Meeting">
/// What still has to happen before the invitation is offered, in the game's own words, or null
/// once it has been. <b>This is the open-ended part</b>, and it is carried as prose precisely so
/// that nothing can add it to a jump count.
/// </param>
/// <param name="Tribute">What the invitation asks to be handed over, where it is a delivery.</param>
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

    /// <summary>
    /// Whether this stop costs something that is not a trip.
    /// <para>
    /// <b>A combat rank is not a trip at all</b>, and turning it into one would state a number
    /// the game does not publish. So it is flagged rather than priced, and the sentence beside it
    /// is Frontier's own.
    /// </para>
    /// </summary>
    public bool IsOpenEnded => Meeting is { Length: > 0 };

    /// <summary>
    /// The reputation climb this stop wants on top of the unlock, priced where Frontier priced it.
    /// Null where the unlock alone is enough.
    /// </summary>
    public long? ReputationCost =>
        Grade > Math.Max(Held, 1) ? EngineeringRules.ReputationCost(Grade) : null;

    /// <summary>One stop, as a Commander hears it.</summary>
    public string Describe() => Engineer.Name + Rest();

    /// <summary>
    /// The same stop with the name taken off the front, so a surface can draw the name as
    /// something pressable and the rest as ordinary text (remediation.md 12, item 7).
    /// <para>
    /// Split here rather than in the page, so the two halves cannot drift: <see cref="Describe"/>
    /// is this with the name in front of it, and there is one sentence rather than two.
    /// </para>
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
/// Everything still standing between the Commander and one engineer, in the order it has to
/// happen (list.md Phase 28, "The fastest way in").
/// <para>
/// <b>A chain rather than a line.</b> Broo Tarquin comes through Hera Tani, who comes through Liz
/// Ryder — three unlocks and two rank climbs, and a checklist entry reading "unlock Broo Tarquin"
/// hides every one of them.
/// </para>
/// </summary>
public sealed record UnlockChain(IReadOnlyList<UnlockStep> Steps)
{
    public static readonly UnlockChain Done = new([]);

    /// <summary>Whether there is nothing left to do at the grade that was asked about.</summary>
    public bool IsDone => Steps.Count == 0;

    /// <summary>
    /// The whole flight, in jumps. Null when any leg's distance is unknown, because a partial sum
    /// presented as a total is the one arithmetic mistake nobody can see.
    /// </summary>
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
/// One thing that has to be true before an engineer will work for the Commander, and whether it
/// is (remediation.md 13, item 12).
/// <para>
/// <b>Three states, not two.</b> Met and unmet are what a checkmark says; the third is the one
/// that matters here — d47 cannot see whether a Commander has scanned thirty jump wakes or sold
/// enough exploration data, because Elite does not publish either. Marking those unmet would be a
/// claim, and marking them met would be a worse one, so they carry neither mark and say so.
/// </para>
/// </summary>
/// <param name="Text">The criterion, in the game's own words where they exist.</param>
/// <param name="Met">
/// True where the journal settles it, false where the journal settles it the other way, and null
/// where nothing d47 can read decides it.
/// </param>
public sealed record UnlockCriterion(string Text, bool? Met)
{
    /// <summary>The mark in front of it. A space where there is nothing honest to draw.</summary>
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
/// How far along the Commander is with one engineer, and what the way in looks like from where
/// they are standing (list.md Phase 28, "Who can roll this").
/// <para>
/// <b>One pane rather than two.</b> A row already holds the name, the specialities, the location
/// and the standing, so a second column beside it would only repeat them.
/// </para>
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

    /// <summary>How many planned things this engineer could roll.</summary>
    public int Wanted { get; init; }

    /// <summary>What is still to be done to reach them. Empty once they are unlocked.</summary>
    public UnlockChain Chain { get; init; } = UnlockChain.Done;

    /// <summary>
    /// Everything that has to be true before they will work, with the ones already true marked
    /// (remediation.md 13, item 12).
    /// <para>
    /// <b>All of them, not only the ones outstanding.</b> The chain above lists what is left,
    /// which is what to go and do; this lists what it takes, which is what a Commander is asking
    /// when they open somebody they have not met — and a referral they earned a month ago
    /// disappearing from the list reads as a requirement that was never there.
    /// </para>
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

    /// <summary>
    /// What they grade, in the order the table states — best grade first, as one spoken clause.
    /// <para>
    /// This is the form for <em>saying</em>, and it stays a sentence: "Beam Laser to 5, Burst
    /// Laser to 5" is how somebody reads it aloud. What a panel wants is
    /// <see cref="SpecialityLines"/> — see there for why the two exist.
    /// </para>
    /// </summary>
    public string Specialities =>
        Engineer.Specialities.Count == 0
            ? "nothing I have a record of"
            : string.Join(", ", Engineer.Specialities.Select(speciality => speciality.IsGraded
                ? $"{speciality.Kind} to {speciality.MaxGrade.ToString(CultureInfo.InvariantCulture)}"
                : speciality.Kind));

    /// <summary>
    /// The same list, one entry at a time (remediation.md 16, item 6).
    /// <para>
    /// Reported against the Engineers tab: <em>"This should not be laid out on a single line. Hard
    /// to get what I need from that."</em> Nine specialities set as running prose wrap into a
    /// paragraph a Commander has to read to find out whether the one they came for is in it, and
    /// the commas that separate the entries look exactly like the commas inside them.
    /// </para>
    /// <para>
    /// <b>Not a formatting of <see cref="Specialities"/>.</b> Splitting that string on its commas
    /// would be a second parser of d47's own sentence, wrong the day a speciality has a comma in
    /// its name. Both are projections of the same table, which is why the grade reads
    /// <c>(G5)</c> here and <c>to 5</c> there: a list is scanned and a sentence is heard, and they
    /// are allowed to differ.
    /// </para>
    /// </summary>
    public IReadOnlyList<string> SpecialityLines =>
    [
        .. Engineer.Specialities.Select(speciality => speciality.IsGraded
            ? $"{speciality.Kind} (G{speciality.MaxGrade.ToString(CultureInfo.InvariantCulture)})"
            : speciality.Kind),
    ];
}

/// <summary>
/// Where each engineer stands with this Commander, and what the way in costs (list.md Phase 28).
/// <para>
/// <b>Core-pure and local.</b> Every distance here is arithmetic over coordinates that shipped in
/// <c>Engineers.tsv</c> and a position the journal already stated, so the Engineers tab works in
/// flight, works offline, and re-ranks for nothing when a plan moves.
/// <see cref="IGalaxyService.DistanceAsync"/> answers the same question correctly and is an async
/// service call, which is the wrong shape for something evaluated thirty-eight times a redraw.
/// </para>
/// <para>
/// <b>Several referrers mean any of them will do</b>, which is the reading
/// <see cref="EngineerDirectory"/> has carried since Phase 14 and the one this keeps. One row
/// argues with it, and it is worth knowing which: Yi Shen's own meeting text reads "Complete
/// referral tasks for Baltanos, Eleanor Bresa and Rosa Dayette" — three requirements rather than a
/// choice of three. So the chain names the referrer it picked and the page prints the meeting text
/// underneath it, and a Commander reads Frontier's sentence rather than only d47's count of it.
/// That is the whole reason a ranking has to show its work.
/// </para>
/// </summary>
public static class EngineerAccess
{
    /// <summary>
    /// What it takes to reach one engineer, with the parts already done marked
    /// (remediation.md 13, item 12).
    /// <para>
    /// <b>Only the journal decides a checkmark.</b> A referral is settled by the grade the
    /// Commander holds with the referrer, and being invited or unlocked is settled by
    /// <c>EngineerProgress</c> — those three are read. Everything else Frontier states as prose
    /// about scanning wakes or selling data, and nothing in the journal answers it, so those lines
    /// carry a question mark rather than a claim in either direction.
    /// </para>
    /// <para>
    /// <b>Unlocked means all of it is met</b>, including the prose, because the game has already
    /// said so — which is the one case where an unreadable criterion becomes readable.
    /// </para>
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
            // Their invitation task, in Frontier's words. Met once the invitation has been
            // offered, which the journal does say; unreadable before that, which it does not.
            criteria.Add(new UnlockCriterion(
                meeting,
                unlocked || standing?.IsInvited == true ? true : null));
        }

        if (engineer.Unlock is { Length: > 0 } tribute)
        {
            criteria.Add(new UnlockCriterion(tribute, unlocked ? true : null));
        }

        // Last, and it is the one line that is always decidable: the others are how you get here
        // and this is whether you have.
        criteria.Add(new UnlockCriterion(
            $"{engineer.Name} works for you",
            unlocked));

        return criteria;
    }

    /// <summary>
    /// Whether the Commander can act on this engineer today, from their journal and the table.
    /// <para>
    /// <b>The journal wins where the two meet.</b> An invitation already extended is a referral
    /// that has happened, and no shipped table can be more right about that than their own
    /// progress file.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// The referrer who has already earned this engineer their introduction, or null.
    /// <para>
    /// The best standing among them decides it, because any one of them will do — reading three
    /// names as three requirements describes a wall where the table says there is a door.
    /// </para>
    /// </summary>
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
    /// The stops between the Commander and one engineer at one grade, in flying order, or
    /// <see cref="UnlockChain.Done"/> when there is nothing left to do.
    /// <para>
    /// <b>Walked from the target backwards and then reversed</b>, because the chain is a parent
    /// link: an engineer names who recommends them and never who they recommend. A stop already
    /// unlocked at the grade the next one wants is not a stop.
    /// </para>
    /// <para>
    /// <paramref name="from"/> is where the Commander is standing, and each leg is measured from
    /// the stop before it — so the answer is the flight actually flown rather than the sum of the
    /// distances from home to each of them.
    /// </para>
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

        // Bounded by the directory rather than by a counter: every engineer appears at most once,
        // so a table that ever grew a cycle stops here instead of hanging the panel.
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
                // Either they are unlocked and only the rank is short, or the introduction has
                // already been earned. Nothing further back is still to be done.
                break;
            }

            // The referrer with the shortest way in, then the nearest of those — the same two
            // questions the ranking asks, asked one link at a time.
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

                // Invited, unlocked, or nothing published: three different reasons for there being
                // no open-ended requirement left, and all three are honestly none.
                standing is { IsInvited: true } || held > 0 ? null : stop.Meeting,
                held > 0 ? null : stop.UnlockCost));

            here = stop.Position ?? here;
        }

        return new UnlockChain(steps);
    }

    /// <summary>
    /// A distance as the ship being flown would cross it, rounded up — <b>a jump is
    /// indivisible</b>, so 84 light years at 30 is three jumps and not 2.8. Null when either
    /// figure is unknown.
    /// </summary>
    public static int? Jumps(double? lightYears, double? jumpRange) =>
        lightYears is { } light && jumpRange is { } range && range > 0
            ? (int)Math.Ceiling(light / range)
            : null;

    /// <summary>
    /// The grade the Commander holds with an engineer, and 0 for one they have not unlocked.
    /// <para>
    /// An unlocked engineer with no rank in the journal is read as grade 1 rather than as 0,
    /// because unlocking one <em>is</em> grade 1 — reading it as zero would make every unlocked
    /// engineer look like a stop still to make.
    /// </para>
    /// </summary>
    private static int Held(Engineer engineer, EngineerProgressState? progress) =>
        progress?.For(engineer.Id) is { IsUnlocked: true } standing
            ? Math.Max(standing.Rank ?? 1, 1)
            : 0;

    /// <summary>
    /// How many unlocks deep an engineer still is. Used only to choose between referrers, so it
    /// counts stops rather than measuring anything.
    /// </summary>
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
