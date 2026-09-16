using System.Diagnostics.CodeAnalysis;
using D47.Core.Diagnostics.Donation;

namespace D47.Donations.Store;

/// <summary>
/// An object key as the Worker writes it: <c>excerpts/&lt;donor&gt;/&lt;stamp&gt;-&lt;sha16&gt;.md.gz</c>
/// or <c>corpus/&lt;donor&gt;/&lt;stamp&gt;-&lt;sha16&gt;.jsonl.gz</c>. <c>worker/src/index.js</c> defines
/// that format inline and shares no constant with this.
/// </summary>
/// <remarks>
/// Every component is checked against a fixed alphabet before it reaches a file name. The donor and
/// the stamp come from a key the store hands back, and a key carrying <c>..</c> or a separator would
/// otherwise name a path outside the download folder.
/// </remarks>
public sealed record DonationKey(string Key, string Kind, string Donor, string Stamp)
{
    private const string ExcerptPrefix = "excerpts/";
    private const string CorpusPrefix = "corpus/";

    /// <summary>The donor token as the window shows it.</summary>
    public string ShortDonor => Donor[..8];

    /// <summary>What this donation downloads to, named by the issue's contract.</summary>
    public string ZipName => $"{Stamp}-{Kind}-{ShortDonor}.zip";

    /// <summary>The single entry inside that zip, carrying the decompressed payload.</summary>
    public string EntryName =>
        $"{Stamp}-{Kind}-{ShortDonor}{(Kind == DonationEnvelope.Excerpt ? ".md" : ".jsonl")}";

    public static bool TryParse(string key, [NotNullWhen(true)] out DonationKey? parsed)
    {
        parsed = null;

        string kind;
        string suffix;

        if (key.StartsWith(ExcerptPrefix, StringComparison.Ordinal))
        {
            kind = DonationEnvelope.Excerpt;
            suffix = ".md.gz";
        }
        else if (key.StartsWith(CorpusPrefix, StringComparison.Ordinal))
        {
            kind = DonationEnvelope.Corpus;
            suffix = ".jsonl.gz";
        }
        else
        {
            return false;
        }

        var parts = key.Split('/');

        if (parts.Length != 3 || !parts[2].EndsWith(suffix, StringComparison.Ordinal))
        {
            return false;
        }

        var donor = parts[1];

        if (!IsDonorToken(donor))
        {
            return false;
        }

        var dash = parts[2].IndexOf('-', StringComparison.Ordinal);

        if (dash <= 0)
        {
            return false;
        }

        var stamp = parts[2][..dash];

        if (!IsStamp(stamp))
        {
            return false;
        }

        parsed = new DonationKey(key, kind, donor, stamp);
        return true;
    }

    /// <summary>Thirty-two lowercase hex characters, which is all the Worker will write.</summary>
    private static bool IsDonorToken(string donor) =>
        donor.Length == 32 && donor.All(character => character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

    /// <summary>A basic-format UTC instant: <c>20260914T100926Z</c>.</summary>
    private static bool IsStamp(string stamp) =>
        stamp.Length == 16
        && stamp[8] == 'T'
        && stamp[15] == 'Z'
        && stamp.Where((_, index) => index != 8 && index != 15).All(char.IsAsciiDigit);
}
