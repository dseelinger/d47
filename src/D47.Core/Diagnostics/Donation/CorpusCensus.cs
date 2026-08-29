namespace D47.Core.Diagnostics.Donation;

/// <summary>
/// One event kind, what the scrub did to it, and one real instance of the result
/// (<a href="https://github.com/dseelinger/d47/issues/174">#174</a>).
/// </summary>
/// <param name="Kind">Elite's own event name.</param>
/// <param name="Events">How many of them are in the donation.</param>
/// <param name="Changed">How many the scrub altered — a name replaced, a field dropped, a body withheld.</param>
/// <param name="Withheld">How many were dropped whole, unreadable to the scrubber.</param>
/// <param name="Sample">
/// One real line from the payload, after scrubbing. Null only where every instance was withheld,
/// which leaves nothing to show and is itself the thing worth reporting.
/// </param>
public sealed record KindCensus(string Kind, int Events, int Changed, int Withheld, string? Sample)
{
    /// <summary>Whether the scrub did anything to this kind at all.</summary>
    public bool Touched => Changed > 0 || Withheld > 0;
}

/// <summary>
/// What kinds of thing are in a corpus donation, counted, with one scrubbed instance of each kept
/// (<a href="https://github.com/dseelinger/d47/issues/174">#174</a>).
/// <para>
/// <b>This is what makes a corpus consentable, and the reason is a size argument rather than a
/// design preference.</b> A report built from this is <i>O(distinct event kinds)</i> — a few
/// hundred lines — while the payload it describes is <i>O(events)</i>, which for the corpus behind
/// #174 is roughly 712,000. Staging the donation into sessions does not help, because 935 reviews
/// is as unreadable as one 383 MB file; counting by kind does, because the number of kinds does not
/// grow with the number of sittings.
/// </para>
/// <para>
/// <b>The sample is the longest instance, on purpose.</b> Not the first, which would be whatever
/// the earliest session happened to contain, and not a random one, which could not be reproduced.
/// The longest post-scrub line for a kind is its maximal-exposure instance — the one with the most
/// fields that survived — so consenting to it is a stronger act than consenting to a typical one. A
/// changed instance always wins over an unchanged one of the same kind, because a reader checking
/// this report is checking the scrub.
/// </para>
/// </summary>
public sealed class CorpusCensus
{
    private sealed class Counter
    {
        public int Events;
        public int Changed;
        public int Withheld;
        public string? Sample;
        public bool SampleChanged;
    }

    private readonly Dictionary<string, Counter> _kinds = new(StringComparer.Ordinal);

    /// <summary>How many distinct kinds have been seen.</summary>
    public int Count => _kinds.Count;

    /// <summary>
    /// Records one event and what the scrub made of it.
    /// </summary>
    /// <param name="kind">Elite's event name.</param>
    /// <param name="before">The line as the file held it.</param>
    /// <param name="after">The line as it will travel, or null where it was withheld whole.</param>
    public void Saw(string kind, string before, string? after)
    {
        if (!_kinds.TryGetValue(kind, out var counter))
        {
            counter = new Counter();
            _kinds[kind] = counter;
        }

        counter.Events++;

        if (after is null)
        {
            counter.Withheld++;
            return;
        }

        var changed = !string.Equals(before, after, StringComparison.Ordinal);

        if (changed)
        {
            counter.Changed++;
        }

        // A changed instance displaces an unchanged one whatever their lengths, because the report
        // exists to let a reader check the scrub and an untouched line shows them nothing about it.
        // Within the same class, longest wins.
        if (counter.Sample is null
            || (changed && !counter.SampleChanged)
            || (changed == counter.SampleChanged && after.Length > counter.Sample.Length))
        {
            counter.Sample = after;
            counter.SampleChanged = changed;
        }
    }

    /// <summary>
    /// What was seen, ordered by kind. <b>Ordering by name rather than by count</b> because this is
    /// read as an inventory — a reader checking whether something they expected is present scans
    /// alphabetically. <see cref="CorpusReport"/> does the grouping that matters.
    /// </summary>
    public IReadOnlyList<KindCensus> Kinds =>
    [
        .. _kinds
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new KindCensus(
                pair.Key,
                pair.Value.Events,
                pair.Value.Changed,
                pair.Value.Withheld,
                pair.Value.Sample)),
    ];
}
