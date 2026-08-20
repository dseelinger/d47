namespace D47.Core.Habits;

/// <summary>
/// The circumstance a claim is about, and therefore the moment the callout is allowed to say it
/// (list.md Phase 32, "Callouts that are yours rather than everyone's").
/// <para>
/// A personal callout fires <b>when the circumstance arrives</b> rather than at the start of a
/// session. <see cref="Callouts.ContinuityCallout"/> already owns the opening line, and a second
/// line competing with it is exactly the "second voice competing with the first" the item forbids.
/// </para>
/// <para>
/// A closed enum rather than a predicate on the claim, because a claim is stored on disk and read
/// back — a stored lambda is not a thing, and a stored occasion is.
/// </para>
/// </summary>
public enum HabitOccasion
{
    /// <summary>
    /// Nothing in flight brings this one up. The claim is readable and is never spoken unprompted,
    /// which is what a detector answers when the pattern has no live moment to attach to.
    /// </summary>
    Never,

    /// <summary>Dropping out of supercruise at a station — the moment before the approach.</summary>
    ArrivingAtAStation,

    /// <summary>Entering orbital cruise over a planet, which is where a glide begins.</summary>
    ApproachingAPlanet,

    /// <summary>The interdiction tug-of-war, at the instant it starts.</summary>
    BeingInterdicted,

    /// <summary>Walking up to a settlement on foot.</summary>
    ArrivingOnFoot,

    /// <summary>Touching down on a surface.</summary>
    TouchingDown,
}

/// <summary>
/// The numbers behind a claim (list.md Phase 32, "A habit is a claim with a count behind it").
/// <para>
/// <b>Every field here is the item's requirement rather than a convenience.</b> It refuses
/// <em>you always forget the gear</em> and asks for <em>eleven times this month, twice last
/// night</em> — a frequency, a window and a sample size, "or it is an insult rather than an
/// observation". So a claim cannot be constructed without them, and
/// <see cref="HabitClaim.Sentence"/> is built from them rather than beside them.
/// </para>
/// </summary>
public sealed record HabitEvidence
{
    /// <summary>How many times it happened.</summary>
    public required int Occurrences { get; init; }

    /// <summary>
    /// Out of how many chances. <b>The denominator is the difference between a habit and a
    /// Tuesday</b>: fifty submissions out of fifty-two is a thing about a person, and two out of
    /// two is a fortnight with two interdictions in it.
    /// </summary>
    public required int Opportunities { get; init; }

    /// <summary>The first event the window covers, and the last. Both from the journal's own stamps.</summary>
    public required DateTimeOffset From { get; init; }

    public required DateTimeOffset To { get; init; }

    /// <summary>
    /// How many of the occurrences fall inside <see cref="HabitFloor.RecentWindow"/> of the mining
    /// run. The half of the item's example sentence a total cannot carry — a habit somebody grew
    /// out of eight months ago and one they have twice this week are the same total.
    /// </summary>
    public int Recent { get; init; }

    /// <summary>The most recent occurrence, or null where there were none.</summary>
    public DateTimeOffset? Latest { get; init; }

    /// <summary>How many journal files the window was read from. Part of "a small corpus says so".</summary>
    public required int Journals { get; init; }

    /// <summary>How often it happened, as a proportion. Zero opportunities is zero rather than a throw.</summary>
    public double Rate => Opportunities <= 0 ? 0 : (double)Occurrences / Opportunities;

    /// <summary>
    /// Whether this clears every floor in <see cref="HabitFloor"/>. The one place the three numbers
    /// are compared, so a detector cannot ship a claim by forgetting to ask.
    /// </summary>
    public bool ClearsTheFloor =>
        Journals >= HabitFloor.Journals &&
        Occurrences >= HabitFloor.Occurrences &&
        Opportunities >= HabitFloor.Opportunities;

    /// <summary>
    /// Whether this is common enough to be said out loud unprompted (remediation.md 17, item 12).
    /// <para>
    /// <b>The denominator was recorded, printed and never tested.</b> Reported as *"You have
    /// dropped short of where you were going and had to come back — 14 of 490 approaches"*, said
    /// on a perfect approach: 14 of 490 clears all three counts above while being something the
    /// Commander does once in thirty-five approaches. <see cref="Opportunities"/> documents itself
    /// as the difference between a habit and a Tuesday, and then only the numerator was asked.
    /// </para>
    /// <para>
    /// <b>Separate from <see cref="ClearsTheFloor"/> rather than folded into it</b>, because the
    /// two answer different questions. Clearing the counts is what makes the arithmetic sound
    /// enough to record and to show; clearing the rate is what earns interrupting somebody with
    /// it. A claim below the rate stays readable on the Habits page and is simply never spoken.
    /// </para>
    /// <para>
    /// Measured across the three Commanders in the 916-journal corpus: overshoot rates 2.3%, 1.0%
    /// and 2.8% and never speaks again; arrival collisions 0.4%; while *you submit rather than
    /// run* is 19 of 21 and 24 of 24, and settlement security killing you on foot is 6 of 12.
    /// The floor cleanly separates the two kinds.
    /// </para>
    /// </summary>
    public bool WorthSaying => ClearsTheFloor && Rate >= HabitFloor.Rate;
}

/// <summary>
/// How much of the Commander there has to be before d47 says anything about them (list.md Phase 32,
/// item 2: "a small corpus says so rather than inventing a pattern from three journals").
/// <para>
/// Constants rather than settings, deliberately. A Commander who could lower the floor would be
/// able to ask d47 to tell them something it does not know, and the first thing they would do with
/// that is confirm something they already believed.
/// </para>
/// </summary>
public static class HabitFloor
{
    /// <summary>
    /// Journals in the corpus before any detector is consulted at all. Item 2 names a fortnight as
    /// the thing not to generalise from; twenty files is comfortably more than one.
    /// </summary>
    public const int Journals = 20;

    /// <summary>Times it has to have happened. Below five, a pattern is a coincidence with a count.</summary>
    public const int Occurrences = 5;

    /// <summary>Chances it had to happen. A rate with no denominator is a number pretending.</summary>
    public const int Opportunities = 10;

    /// <summary>
    /// How often it has to happen before d47 will say it unprompted — one time in five
    /// (remediation.md 17, item 12).
    /// <para>
    /// The counts above say the arithmetic is sound. This says the thing is <em>characteristic</em>,
    /// which is what the word habit means and what the counts alone could not test: at 2.9% almost
    /// every approach is a good one, so a line of the form <em>you do this</em> arrives while the
    /// Commander demonstrably is not doing it, 97 times in 100. No wording fixes that; only a
    /// denominator does.
    /// </para>
    /// </summary>
    public const double Rate = 0.20;

    /// <summary>What "recently" means in a claim's sentence.</summary>
    public static readonly TimeSpan RecentWindow = TimeSpan.FromDays(30);
}

/// <summary>
/// One thing d47 has noticed the Commander keeps doing, with the arithmetic that says so
/// (list.md Phase 32).
/// <para>
/// <b>Derived, never told.</b> A claim is the output of arithmetic over journal events on this
/// machine. No model wrote one, no model reads one, and nothing the Commander says can create one —
/// which is what keeps the whole phase out of the untrusted-input path that
/// <see cref="Memory.MemoryEntry"/> has to hedge against entry by entry.
/// </para>
/// </summary>
/// <param name="Key">
/// The detector that produced it, stable across runs. What a dismissal names, and therefore what
/// has to survive a re-mine — see <see cref="HabitStore"/>.
/// </param>
/// <param name="Subject">What the Commander would call it, for the panel and for a sentence.</param>
public sealed record HabitClaim(string Key, string Subject)
{
    public required HabitEvidence Evidence { get; init; }

    /// <summary>The claim in words, without its numbers. The numbers are added by <see cref="Spoken"/>.</summary>
    public required string Observation { get; init; }

    /// <summary>
    /// What the opportunities <em>are</em> — arrivals, approaches, interdictions, deaths. Required,
    /// because a bare rate is the thing item 2 refuses: <em>nine of two thousand three hundred and
    /// seventy</em> is not a sentence, and a Commander cannot check a denominator they have not
    /// been told the meaning of.
    /// </summary>
    public required string Denominator { get; init; }

    /// <summary>When it is allowed to be said unprompted.</summary>
    public HabitOccasion Occasion { get; init; } = HabitOccasion.Never;

    /// <summary>
    /// The sentence, with its count and its window in it — item 2's whole requirement, enforced by
    /// there being no other way to render a claim.
    /// <para>
    /// Never <em>you always</em>. The observation states what happened and this states how often and
    /// over what, so a claim cannot be spoken shorn of the thing that makes it fair.
    /// </para>
    /// </summary>
    public string Spoken()
    {
        var evidence = Evidence;
        var recent = evidence.Recent > 0
            ? $", {evidence.Recent} of them in the last month"
            : string.Empty;

        return $"{Observation} — {evidence.Occurrences} of {evidence.Opportunities} {Denominator}{recent}.";
    }

    /// <summary>
    /// The claim with the whole of its working shown — what <c>explain_habit</c> answers, and item
    /// 3's "it says why it fired if asked".
    /// </summary>
    public string Explained()
    {
        var evidence = Evidence;
        var latest = evidence.Latest is { } at ? $" The last one was {at:yyyy-MM-dd}." : string.Empty;

        return $"{Spoken()} Counted over {evidence.Journals} of your journals, "
            + $"{evidence.From:yyyy-MM-dd} to {evidence.To:yyyy-MM-dd}.{latest}";
    }

    /// <summary>
    /// A count in the words a person would use. Shared with <see cref="HabitDetector.Declined"/>,
    /// because "only 1 times in 662" was what the first run over the real corpus actually printed.
    /// </summary>
    public static string Times(int count) => count == 1 ? "once" : $"{count} times";
}

/// <summary>
/// A detector that ran and had nothing to claim, and why (list.md Phase 32, item 2).
/// <para>
/// <b>This exists because "nothing to report" and "not enough of you to say" are different
/// answers</b>, and a Commander given the first when the second is true has been told something
/// untrue about themselves — which is the exact failure the item says makes them stop believing the
/// true ones. A detector that declined is reported as having declined.
/// </para>
/// </summary>
public sealed record HabitSilence(string Key, string Subject, string Why);
