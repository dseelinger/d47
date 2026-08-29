namespace D47.Core.Diagnostics.Donation;

/// <summary>
/// How far back a donation reaches, in the units a Commander thinks in
/// (<a href="https://github.com/dseelinger/d47/issues/173">#173</a>).
/// <para>
/// <b>Named spans rather than a pair of minute steppers</b>, and the reason is not taste. The
/// steppers implied a reach the sources did not have: the journal half stopped at the current Elite
/// session and the log half could not cross midnight, so a Commander who restarted d47 and asked
/// for sixty minutes got twenty and was told nothing. Now that both halves read from disk, the
/// control can offer what is actually there — and a span a person can name is one they can also
/// judge the size of before they consent to it.
/// </para>
/// <para>
/// <b>The list stops at half a day, and that number came from measuring rather than taste.</b> The
/// consent this window asks for is <i>read this and say yes to it</i>, so the offer has to stop
/// where reading does. Driven against a real Commander's journals and logs: six hours came to
/// 48,000 characters, while two days came to 1.8 million and a week to 5.3 million. A span nobody can read is not a wider version of this feature, it is a different
/// one, and it is <a href="https://github.com/dseelinger/d47/issues/174">#174</a>.
/// </para>
/// <para>
/// <b>What #173 bought is not the top of this list.</b> It is that every entry on it now returns
/// what it says: before the sources moved to disk, a Commander who had restarted d47 twenty minutes
/// ago got twenty minutes whichever of these they picked, and was told nothing.
/// </para>
/// </summary>
/// <param name="Name">What the chooser shows.</param>
/// <param name="Before">How far back from the mark.</param>
/// <param name="After">
/// How far past it — nothing for the wider spans, which already run to the mark, and a minute for
/// the tightest, because a Commander says "note that" while a thing is going wrong at least as
/// often as afterwards.
/// </param>
public sealed record ExcerptSpan(string Name, TimeSpan Before, TimeSpan After)
{
    /// <summary>
    /// The offer, tightest first. The first two are an incident; the last two are a sitting, and a
    /// defect that only shows itself across a day.
    /// </summary>
    public static readonly IReadOnlyList<ExcerptSpan> All =
    [
        new("The last 10 minutes", TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(1)),
        new("The last hour", TimeSpan.FromHours(1), TimeSpan.FromMinutes(1)),
        new("The last 6 hours", TimeSpan.FromHours(6), TimeSpan.Zero),
        new("The last 12 hours", TimeSpan.FromHours(12), TimeSpan.Zero),
    ];

    /// <summary>
    /// What a window opens on. The old default was five minutes either side of the mark, and the
    /// nearest thing to it that says what it means is the ten-minute span.
    /// </summary>
    public static ExcerptSpan Default => All[0];

    /// <summary>The window this span puts around a mark.</summary>
    public ExcerptRequest Around(DateTimeOffset markedAt, bool includeMySpeech) =>
        new(markedAt, Before, After, includeMySpeech);

    public override string ToString() => Name;
}
