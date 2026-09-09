using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace D47.Core.Diagnostics.Donation;

/// <summary>What is written on the outside of a donation, as opposed to what is in it (#175).</summary>
/// <param name="Format">The version of this agreement.</param>
/// <param name="Kind"><see cref="Excerpt"/> or <see cref="Corpus"/>.</param>
/// <param name="Donor">The per-installation token (#176).</param>
/// <param name="Build">The full build stamp the donation was cut from.</param>
/// <param name="TakenAt">When the Commander cut it.</param>
/// <param name="Bytes">How large the scrubbed payload is, counted rather than estimated.</param>
/// <param name="Sha256">SHA-256 of the scrubbed payload, lowercase hex.</param>
public sealed record DonationEnvelope(
    int Format,
    string Kind,
    string? Donor,
    string Build,
    DateTimeOffset TakenAt,
    long Bytes,
    string Sha256)
{
    /// <summary>The format this build speaks.</summary>
    public const int CurrentFormat = 1;

    /// <summary>An incident excerpt — evidence for one defect, and expiring.</summary>
    public const string Excerpt = "excerpt";

    /// <summary>A journal history — a replay case, and permanent.</summary>
    public const string Corpus = "corpus";

    /// <summary>The header names, which are a published interface the moment the first build ships.</summary>
    public const string FormatHeader = "d47-format";

    /// <inheritdoc cref="FormatHeader"/>
    public const string KindHeader = "d47-kind";

    /// <inheritdoc cref="FormatHeader"/>
    public const string DonorHeader = "d47-donor";

    /// <inheritdoc cref="FormatHeader"/>
    public const string BuildHeader = "d47-build";

    /// <inheritdoc cref="FormatHeader"/>
    public const string TakenAtHeader = "d47-taken-at";

    /// <inheritdoc cref="FormatHeader"/>
    public const string BytesHeader = "d47-bytes";

    /// <inheritdoc cref="FormatHeader"/>
    public const string Sha256Header = "d47-sha256";

    /// <summary>How long a value the endpoint will copy into object metadata.</summary>
    public const int MostHeaderCharacters = 200;

    /// <summary>UTF-8 with no byte order mark.</summary>
    public static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Whether this is a kind the store has a retention rule for.</summary>
    public static bool IsKnownKind(string kind) =>
        kind is Excerpt or Corpus;

    /// <summary>
    /// The envelope for a payload already held whole — which an excerpt always is, being kilobytes and
    /// being the thing the Commander just read.
    /// </summary>
    public static DonationEnvelope For(
        string kind,
        string? donor,
        ExcerptPaperwork paperwork,
        ReadOnlySpan<byte> payload) =>
        new(
            CurrentFormat,
            kind,
            donor,
            Trim(paperwork.Build),
            paperwork.TakenAt,
            payload.Length,
            Convert.ToHexStringLower(SHA256.HashData(payload)));

    /// <summary>
    /// The envelope for a payload too large to hold — which a corpus is, being hundreds of megabytes
    /// assembled one journal file at a time and never existing in one place.
    /// </summary>
    public static DonationEnvelope For(
        string kind,
        string? donor,
        ExcerptPaperwork paperwork,
        long bytes,
        string sha256) =>
        new(CurrentFormat, kind, donor, Trim(paperwork.Build), paperwork.TakenAt, bytes, sha256);

    /// <summary>The headers, in a fixed order, ready to be set on a request.</summary>
    public IEnumerable<KeyValuePair<string, string>> Headers()
    {
        yield return new(FormatHeader, Format.ToString(CultureInfo.InvariantCulture));
        yield return new(KindHeader, Kind);
        yield return new(BuildHeader, Trim(Build));
        yield return new(TakenAtHeader, Stamp(TakenAt));
        yield return new(BytesHeader, Bytes.ToString(CultureInfo.InvariantCulture));
        yield return new(Sha256Header, Sha256);

        if (Donor is { Length: > 0 } donor)
        {
            yield return new(DonorHeader, donor);
        }
    }

    /// <summary>
    /// Whether this envelope is one the endpoint would accept, checked before the request is made.
    /// </summary>
    public bool IsWellFormed() =>
        Format == CurrentFormat
        && IsKnownKind(Kind)
        && DonorToken.IsWellFormed(Donor)
        && Bytes > 0
        && Sha256.Length == 64
        && Sha256.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    /// <summary>
    /// The object name this donation will be stored under, worked out here so the receipt can name it
    /// before the endpoint answers — and so a donor who is told a key can check it is the one their own
    /// bytes produce.
    /// </summary>
    public string PredictedKey() =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{Prefix(Kind)}{Donor}/{Stamp(TakenAt)}-{Sha256[..16]}{Suffix(Kind)}");

    /// <summary>Where a kind's objects live, which is what its retention rule is written against.</summary>
    public static string Prefix(string kind) => kind switch
    {
        Corpus => "corpus/",
        _ => "excerpts/",
    };

    /// <summary>What the stored object is called.</summary>
    public static string Suffix(string kind) => kind switch
    {
        Corpus => ".jsonl.gz",
        _ => ".md.gz",
    };

    /// <summary>
    /// UTC, no punctuation a URL or an object name has to escape, and sorting in time order — which is
    /// the order anybody listing a donor's prefix wants to read them in.
    /// </summary>
    public static string Stamp(DateTimeOffset at) =>
        at.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    /// <summary>
    /// SHA-256 of some bytes, lowercase hex — the one spelling used by the envelope, the receipt and
    /// the endpoint, so a donor comparing two of them is comparing the same alphabet.
    /// </summary>
    public static string HashOf(ReadOnlySpan<byte> payload) =>
        Convert.ToHexStringLower(SHA256.HashData(payload));

    /// <inheritdoc cref="HashOf(System.ReadOnlySpan{byte})"/>
    public static string HashOf(string payload) =>
        HashOf(Utf8.GetBytes(payload));

    private static string Trim(string value)
    {
        var clean = new string([.. value.Where(character => character is >= ' ' and <= '~')]);
        return clean.Length <= MostHeaderCharacters ? clean : clean[..MostHeaderCharacters];
    }
}
