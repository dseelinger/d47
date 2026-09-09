namespace D47.Core.Diagnostics.Donation;

/// <summary>One event kind, what the scrub did to it, and one real instance of the result (#174).</summary>
/// <param name="Kind">Elite's own event name.</param>
/// <param name="Events">How many of them are in the donation.</param>
/// <param name="Changed">
/// How many the scrub altered — a name replaced, a field dropped, a body withheld.
/// </param>
/// <param name="Withheld">How many were dropped whole, unreadable to the scrubber.</param>
/// <param name="Sample">One real line from the payload, after scrubbing.</param>
public sealed record KindCensus(string Kind, int Events, int Changed, int Withheld, string? Sample)
{
    /// <summary>Whether the scrub did anything to this kind at all.</summary>
    public bool Touched => Changed > 0 || Withheld > 0;
}

/// <summary>
/// What kinds of thing are in a corpus donation, counted, with one scrubbed instance of each kept
/// (#174).
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

    /// <summary>Records one event and what the scrub made of it.</summary>
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

        // A changed instance displaces an unchanged one whatever their lengths, because the report exists to
        // let a reader check the scrub and an untouched line shows them nothing about it.
        if (counter.Sample is null
            || (changed && !counter.SampleChanged)
            || (changed == counter.SampleChanged && after.Length > counter.Sample.Length))
        {
            counter.Sample = after;
            counter.SampleChanged = changed;
        }
    }

    /// <summary>What was seen, ordered by kind.</summary>
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
