using System.Globalization;

namespace D47.Core.Logbook;

/// <summary>
/// What kind of thing a fact is about. Used to group the digest and to cap one category without
/// capping the others — a thousand mission completions must not crowd out the one death.
/// </summary>
public enum LogFactKind
{
    /// <summary>Where the window opened, and in what.</summary>
    Opening,

    Travel,

    Exploration,

    Trade,

    Combat,

    Mission,

    Engineering,

    /// <summary>Ships bought, sold, swapped, transferred.</summary>
    Fleet,

    /// <summary>Rank, reputation, anything that went up.</summary>
    Progress,

    /// <summary>Deaths, rebuys, hull, interdictions that were not the Commander's idea.</summary>
    Mishap,

    OnFoot,

    /// <summary>Where the window closed.</summary>
    Closing,
}

/// <summary>
/// One journal event a fact rests on. The event's own name and its instant, never its text —
/// see <see cref="LogDigestBuilder"/> for why nothing a person typed reaches this.
/// </summary>
public sealed record LogSource(string Event, DateTimeOffset At)
{
    public override string ToString() =>
        $"{Event} at {At.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}Z";
}

/// <summary>
/// One thing d47 computed from the journals, numbered so a sentence can point at it (list.md
/// Phase 33, "Every sentence traces to an event").
/// <para>
/// <b>This is the whole defence and it is worth being plain about the mechanism.</b> A model handed
/// a journal will write a better evening than the one that happened — it will promote a routine
/// docking into a narrow escape, because that reads better and nothing stopped it. A model handed
/// <em>docked at Jameson Memorial, 20:14</em> and told to cite what it used cannot do that without
/// inventing a numbered fact, which is a thing an audit can see.
/// </para>
/// <para>
/// <b>Aggregated, not transcribed.</b> Forty-two jumps are one fact citing forty-two events, not
/// forty-two facts. That is what keeps the largest prompt d47 assembles bounded, and it is also
/// what makes the prose readable: a list of every event that happened is a journal, and the
/// Commander already has one of those.
/// </para>
/// </summary>
public sealed record LogFact
{
    /// <summary>What a citation names. Assigned when the digest is sealed, never by a detector.</summary>
    public int Id { get; init; }

    public required LogFactKind Kind { get; init; }

    /// <summary>What d47 says happened, in the fewest words that keep it true.</summary>
    public required string Statement { get; init; }

    /// <summary>When, for ordering. The first of the supporting events on an aggregate.</summary>
    public DateTimeOffset At { get; init; }

    /// <summary>
    /// The events behind it, capped. An aggregate over four hundred bounties records the first
    /// few and the count rather than four hundred lines nobody will read — the file has to record
    /// what the fact drew on, not reproduce the journal it drew it from.
    /// </summary>
    public IReadOnlyList<LogSource> Sources { get; init; } = [];

    /// <summary>How many events there actually were, which may exceed <see cref="Sources"/>.</summary>
    public int SourceCount { get; init; }

    /// <summary>
    /// A figure over the whole window rather than a thing that happened at a moment. Rendered in
    /// its own group, because a total dropped into a chronology reads as an event and a model
    /// asked to narrate would place it in the story where it does not belong.
    /// </summary>
    public bool Aggregate { get; init; }

    public const int SourcesKept = 6;

    /// <summary>How the Sources section of the written file reads this fact.</summary>
    public string Provenance()
    {
        if (Sources.Count == 0)
        {
            return "no supporting event recorded";
        }

        var listed = string.Join("; ", Sources.Select(source => source.ToString()));

        return SourceCount > Sources.Count
            ? $"{listed}; and {SourceCount - Sources.Count} more"
            : listed;
    }
}
