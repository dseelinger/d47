namespace D47.Core.Diagnostics.Donation;

/// <summary>
/// How far back a corpus donation reaches
/// (<a href="https://github.com/dseelinger/d47/issues/174">#174</a>).
/// <para>
/// <b>Deliberately not the same control as <see cref="ExcerptSpan"/>, because it answers a
/// different question.</b> A span is a window around an incident and stops at half a day, because
/// the consent there is <i>read this and say yes to it</i> and reading stops there. A scope is a
/// range of history with no incident in it, and the consent is a different one — see
/// <see cref="CorpusReport"/>. So this offers what the excerpt window must not: everything.
/// </para>
/// <para>
/// <b>There is no journal half and no log half here.</b> A corpus donation is Elite's journals and
/// nothing else: they reach back thirteen months while d47's own log keeps a fortnight
/// (<a href="https://github.com/dseelinger/d47/issues/168">#168</a>), and the log is speech rather
/// than a schema of game facts — it has no field list and its control is the show step, which is
/// exactly the control a corpus cannot use.
/// </para>
/// </summary>
/// <param name="Name">What the chooser shows.</param>
/// <param name="Back">
/// How far back from now, or null for the whole history on disk. Null rather than a very large
/// <see cref="TimeSpan"/> so that "everything" is a stated case rather than an arithmetic accident.
/// </param>
public sealed record CorpusScope(string Name, TimeSpan? Back)
{
    /// <summary>
    /// The offer, narrowest first
    /// (<a href="https://github.com/dseelinger/d47/issues/241">#241</a>).
    /// <para>
    /// It opened widest-first — "the whole point here is the biggest one" — until the Commander
    /// overturned that: the gentle scope is the opening offer, and the widest is one step away.
    /// And the widest is <b>"Everything", never "everything on disk"</b>: it means every Elite
    /// journal, and a Commander reads "on disk" literally, as though d47 were about to take the
    /// disk's contents rather than the game's history.
    /// </para>
    /// </summary>
    public static readonly IReadOnlyList<CorpusScope> All =
    [
        new("The last 30 days", TimeSpan.FromDays(30)),
        new("The last 3 months", TimeSpan.FromDays(90)),
        new("The last 12 months", TimeSpan.FromDays(365)),
        new("Everything", null),
    ];

    /// <summary>What the window opens on: the gentlest scope, not the biggest (#241).</summary>
    public static CorpusScope Default => All[0];

    /// <summary>The instant this scope starts at, given when the Commander asked.</summary>
    public DateTimeOffset From(DateTimeOffset now) =>
        Back is { } back ? now - back : DateTimeOffset.MinValue;

    public override string ToString() => Name;
}
