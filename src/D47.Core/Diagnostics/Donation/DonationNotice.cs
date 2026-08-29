namespace D47.Core.Diagnostics.Donation;

/// <summary>
/// Where a donor reads who will hold their donation, on what basis, for what, for how long, and
/// how to have it deleted (<a href="https://github.com/dseelinger/d47/issues/166">#166</a>).
/// <para>
/// <b>Said once, because two reports need it.</b> <see cref="ExcerptReport"/> and
/// <see cref="CorpusReport"/> both put it in front of the Commander at the moment of consent, and
/// a URL written twice is a URL that gets corrected once.
/// </para>
/// <para>
/// <b>Not the in-app Privacy page, and that is the whole point of it being a URL.</b> That page is
/// computed from live egress state and is right for what it does — but it answers "what is this
/// build reaching" rather than "who ends up holding what I am about to send", and somebody
/// deciding whether to donate may be reading a thread rather than looking at the panel. A
/// published address is readable before, during and long after, by somebody who has never
/// installed d47.
/// </para>
/// </summary>
public static class DonationNotice
{
    /// <summary>The notice itself — who holds a donation, why, and how to have it deleted.</summary>
    public const string Url = "https://dseelinger.github.io/d47/donation-privacy.html";

    /// <summary>
    /// The sentence a report ends its provenance paragraph with. <b>A whole sentence rather than a
    /// bare link</b>, because a URL on its own tells a Commander nothing about whether it is worth
    /// following, and this one is the only place the answer to "who has this now" is written down.
    /// </summary>
    public const string Line =
        "**Who holds this, and how to have it deleted.** " + Url;
}
