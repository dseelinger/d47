using D47.Core.Journal;

namespace D47.Core.Habits;

/// <summary>The corpus a detector is concluding over. Everything a claim's window needs.</summary>
/// <param name="Journals">How many files were read. Compared against <see cref="HabitFloor.Journals"/>.</param>
/// <param name="From">The first event's stamp, and the last — the journal's own clock, not d47's.</param>
/// <param name="Now">
/// When the mining ran, injected like every other instant in Core. What "in the last month" is
/// measured back from.
/// </param>
public readonly record struct HabitWindow(
    int Journals,
    DateTimeOffset From,
    DateTimeOffset To,
    DateTimeOffset Now);

/// <summary>What a detector concluded: a claim, or a reason it has nothing to claim. Never both.</summary>
public sealed record HabitOutcome(HabitClaim? Claim, HabitSilence? Silence);

/// <summary>
/// One pattern d47 looks for in the Commander's own journals (Phase 32, "Mine the
/// Commander's own journals").
/// <para>
/// <b>Arithmetic over events, and nothing else.</b> No journal text reaches a model and no model
/// reaches a detector — which is not only item 1's requirement but is what keeps the whole phase
/// outside the untrusted-input path. A detector cannot be steered, because there is nothing to
/// steer it with.
/// </para>
/// <para>
/// The shape is <see cref="Callouts.ICallout"/>'s: fold what arrives, keep your own state, answer
/// when asked. It owns no thread and reads no clock, which is what makes 697,787 events in seven
/// seconds possible at all.
/// </para>
/// </summary>
public interface IHabitDetector
{
    /// <summary>Stable across runs, because a dismissal names it and has to outlive a re-mine.</summary>
    string Key { get; }

    /// <summary>What the Commander would call it. Panel label and readback heading.</summary>
    string Subject { get; }

    /// <summary>One event, in file order. Called several hundred thousand times, so it stays cheap.</summary>
    void Observe(JournalEvent journalEvent);

    HabitOutcome Conclude(HabitWindow window);
}

/// <summary>
/// The floor test and the sentence assembly, written once (Phase 32, item 2).
/// <para>
/// Every detector counts two things — how often it happened and how many chances it had — and the
/// rest is identical between them. Putting the comparison here rather than in each detector is what
/// makes "a claim with a count behind it" a property of the type instead of a habit of whoever
/// wrote the last one: <b>a detector cannot ship a claim by forgetting to check</b>.
/// </para>
/// </summary>
public abstract class HabitDetector : IHabitDetector
{
    /// <summary>Every occurrence's stamp, kept rather than counted so "recently" is answerable.</summary>
    private readonly List<DateTimeOffset> _occurrences = [];

    public abstract string Key { get; }

    public abstract string Subject { get; }

    /// <summary>The claim without its numbers — <see cref="HabitClaim.Spoken"/> adds those.</summary>
    protected abstract string Observation { get; }

    /// <summary>When the callout may say it, if ever.</summary>
    protected abstract HabitOccasion Occasion { get; }

    /// <summary>What the opportunities are, as a plural noun. See <see cref="HabitClaim.Denominator"/>.</summary>
    protected abstract string Denominator { get; }

    /// <summary>Chances the pattern had to show itself. The denominator, and its own decision per detector.</summary>
    protected int Opportunities { get; set; }

    protected IReadOnlyList<DateTimeOffset> Occurrences => _occurrences;

    public abstract void Observe(JournalEvent journalEvent);

    public HabitOutcome Conclude(HabitWindow window)
    {
        var occurrences = Resolve();

        var evidence = new HabitEvidence
        {
            Occurrences = occurrences.Count,
            Opportunities = Opportunities,
            From = window.From,
            To = window.To,
            Journals = window.Journals,
            Recent = occurrences.Count(at => window.Now - at <= HabitFloor.RecentWindow),
            Latest = occurrences.Count == 0 ? null : occurrences.Max(),
        };

        if (!evidence.ClearsTheFloor)
        {
            return new HabitOutcome(null, new HabitSilence(Key, Subject, Declined(evidence)));
        }

        return new HabitOutcome(
            new HabitClaim(Key, Subject)
            {
                Evidence = evidence,
                Observation = Observation,
                Denominator = Denominator,
                Occasion = Occasion,
            },
            null);
    }

    /// <summary>Records one occurrence at the journal's own stamp.</summary>
    protected void Occurred(DateTimeOffset at) => _occurrences.Add(at);

    /// <summary>
    /// A last chance to decide what counted, once the whole corpus has been read. Default is what
    /// <see cref="Occurred"/> collected.
    /// <para>
    /// Exists because one detector genuinely needs it: a body's gravity arrives in a
    /// <c>Scan</c> that may be anywhere in the corpus relative to the landing on it, so
    /// <see cref="HighGravityLandingDetector"/> collects landings and joins the gravity afterwards
    /// rather than requiring the journal to have been written in a convenient order.
    /// </para>
    /// </summary>
    protected virtual IReadOnlyList<DateTimeOffset> Resolve() => _occurrences;

    /// <summary>
    /// Why this detector is saying nothing. Overridden where a detector can say something more
    /// useful than the floor arithmetic — which is the difference item 2 insists on between
    /// "nothing to report" and "not enough of you yet".
    /// </summary>
    protected virtual string Declined(HabitEvidence evidence)
    {
        if (evidence.Journals < HabitFloor.Journals)
        {
            return $"only {evidence.Journals} of your journals to read, and I do not draw conclusions "
                + $"from fewer than {HabitFloor.Journals}";
        }

        if (evidence.Opportunities < HabitFloor.Opportunities)
        {
            return $"there have only been {evidence.Opportunities} {Denominator} to go on, "
                + "which is too few for a rate to mean anything";
        }

        return evidence.Occurrences == 0
            ? $"it has not happened once in {evidence.Opportunities} {Denominator}"
            : $"only {HabitClaim.Times(evidence.Occurrences)} in {evidence.Opportunities} "
                + $"{Denominator}, which is not a habit";
    }
}
