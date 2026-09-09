namespace D47.Core.Diagnostics.Donation;

/// <summary>
/// Where a donor reads who will hold their donation, on what basis, for what, for how long, and how to
/// have it deleted (#166).
/// </summary>
public static class DonationNotice
{
    /// <summary>The notice itself — who holds a donation, why, and how to have it deleted.</summary>
    public const string Url = "https://dseelinger.github.io/d47/donation-privacy.html";

    /// <summary>The sentence a report ends its provenance paragraph with.</summary>
    public const string Line =
        "**Who holds this, and how to have it deleted.** " + Url;
}
