using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace D47.Core.Diagnostics.Donation;

/// <summary>
/// What is written on the outside of a donation, as opposed to what is in it
/// (<a href="https://github.com/dseelinger/d47/issues/175">#175</a>).
/// <para>
/// <b>Agreed before the lanes split, and carried from day one.</b> The consent side
/// (<a href="https://github.com/dseelinger/d47/issues/174">#174</a>) owns what a yes looks like
/// and the transport side owns where payloads land; this record is the whole of the interface
/// between them, which is what let both be built at once. Three fields carry that agreement: a
/// <see cref="Donor"/> slot, a <see cref="Format"/> so the endpoint can refuse what it does not
/// understand rather than guess at it, and a <see cref="Sha256"/> of the scrubbed payload so what
/// was consented to and what arrived are provably the same bytes.
/// </para>
/// <para>
/// <b>Nothing here appears in the payload.</b> The rendered report is the consent record, and an
/// identifier inside it would put an identity into the one artefact the donation path exists to
/// keep identities out of. The envelope travels as request headers beside the body, and the body
/// is exactly the bytes the Commander was shown a description of.
/// </para>
/// </summary>
/// <param name="Format">
/// The version of this agreement. <b>Bumped when a field changes meaning, never when one is
/// added</b> — an endpoint that refuses an unknown format is what stops a newer client's payload
/// being stored under an older client's assumptions, and a client that cannot be updated in the
/// field is a client whose donations must fail loudly rather than land wrong.
/// </param>
/// <param name="Kind">
/// <see cref="Excerpt"/> or <see cref="Corpus"/>. <b>Two retention classes rather than one</b>: a
/// donated corpus is permanent by design — that is the whole payoff, the permanent regression case
/// — and an excerpt is evidence for one defect that should go when the issue closes. Opposite
/// rules, so two prefixes at the store and two lifecycle rules over them.
/// </param>
/// <param name="Donor">
/// The per-installation token (<a href="https://github.com/dseelinger/d47/issues/176">#176</a>).
/// <b>Nullable only for a build that predates that issue landing, never after</b> — the slot was
/// agreed on day one precisely so this could be filled without a format bump, and a null here now
/// means the token file could not be written, which is a fault rather than a mode.
/// </param>
/// <param name="Build">The full build stamp the donation was cut from. What a fix is proven against.</param>
/// <param name="TakenAt">When the Commander cut it.</param>
/// <param name="Bytes">How large the scrubbed payload is, counted rather than estimated.</param>
/// <param name="Sha256">
/// SHA-256 of the scrubbed payload, lowercase hex.
/// <para>
/// <b>Of the payload, and deliberately not of what goes on the wire.</b> The transport may
/// compress; the consent did not. A hash over compressed bytes is not reproducible by anybody
/// holding the payload — compression levels and implementations differ — so it would prove the
/// transfer and nothing a donor cares about. This hash is checkable by the donor against their own
/// receipt, and by anyone later holding the object, with an ordinary <c>sha256sum</c>.
/// </para>
/// </param>
public sealed record DonationEnvelope(
    int Format,
    string Kind,
    string? Donor,
    string Build,
    DateTimeOffset TakenAt,
    long Bytes,
    string Sha256)
{
    /// <summary>The format this build speaks. See <see cref="Format"/> for when it moves.</summary>
    public const int CurrentFormat = 1;

    /// <summary>An incident excerpt — evidence for one defect, and expiring.</summary>
    public const string Excerpt = "excerpt";

    /// <summary>A journal history — a replay case, and permanent.</summary>
    public const string Corpus = "corpus";

    /// <summary>
    /// The header names, which are a published interface the moment the first build ships.
    /// <para>
    /// <b>Prefixed and lowercase, and no <c>X-</c>.</b> HTTP header names are case-insensitive on
    /// the wire but the Worker reads them by literal string, and every runtime in between
    /// lowercases them — so writing them lowercase here is writing what will actually be compared.
    /// The <c>X-</c> convention was deprecated in RFC 6648 and buys nothing.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// How long a value the endpoint will copy into object metadata. <b>Said on both sides.</b>
    /// R2 caps the whole custom-metadata block, so one long header would fail the write rather
    /// than be truncated — and the only header here whose length d47 does not choose is the build
    /// stamp, which a local build can make arbitrarily long.
    /// </summary>
    public const int MostHeaderCharacters = 200;

    /// <summary>UTF-8 with no byte order mark. A BOM in front of a payload is a payload that fails at line one.</summary>
    public static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Whether this is a kind the store has a retention rule for.</summary>
    public static bool IsKnownKind(string kind) =>
        kind is Excerpt or Corpus;

    /// <summary>
    /// The envelope for a payload already held whole — which an excerpt always is, being
    /// kilobytes and being the thing the Commander just read.
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
    /// The envelope for a payload too large to hold — which a corpus is, being hundreds of
    /// megabytes assembled one journal file at a time and never existing in one place.
    /// <para>
    /// The caller hashes and counts as it writes; this only assembles what it is told. Splitting
    /// it that way is what keeps <see cref="CorpusDonation"/>'s "nothing is ever held whole"
    /// property true through the transport.
    /// </para>
    /// </summary>
    public static DonationEnvelope For(
        string kind,
        string? donor,
        ExcerptPaperwork paperwork,
        long bytes,
        string sha256) =>
        new(CurrentFormat, kind, donor, Trim(paperwork.Build), paperwork.TakenAt, bytes, sha256);

    /// <summary>
    /// The headers, in a fixed order, ready to be set on a request.
    /// <para>
    /// <b>Every value is bounded here rather than at the endpoint.</b> The endpoint bounds them
    /// too — it must, since it trusts nothing that arrives — but a client that sends something it
    /// knows will be refused has spent a request against a daily ceiling to learn what it already
    /// knew.
    /// </para>
    /// </summary>
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
    /// The object name this donation will be stored under, worked out here so the receipt can name
    /// it before the endpoint answers — and so a donor who is told a key can check it is the one
    /// their own bytes produce.
    /// <para>
    /// <b>The endpoint derives the same key rather than accepting this one.</b> A client-supplied
    /// object name is a path a stranger chooses inside somebody else's bucket, and no amount of
    /// escaping makes that a good idea. This is a prediction, not an instruction.
    /// </para>
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

    /// <summary>
    /// What the stored object is called. Compressed on the wire and stored that way, so the name
    /// says so — a reader who fetches one should not have to guess why it will not parse.
    /// </summary>
    public static string Suffix(string kind) => kind switch
    {
        Corpus => ".jsonl.gz",
        _ => ".md.gz",
    };

    /// <summary>
    /// UTC, no punctuation a URL or an object name has to escape, and sorting in time order —
    /// which is the order anybody listing a donor's prefix wants to read them in.
    /// </summary>
    public static string Stamp(DateTimeOffset at) =>
        at.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    /// <summary>
    /// SHA-256 of some bytes, lowercase hex — the one spelling used by the envelope, the receipt
    /// and the endpoint, so a donor comparing two of them is comparing the same alphabet.
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
