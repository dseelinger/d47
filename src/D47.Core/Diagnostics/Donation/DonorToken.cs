using System.Globalization;
using System.Security.Cryptography;
using D47.Core.Storage;

namespace D47.Core.Diagnostics.Donation;

/// <summary>
/// The one thing on a donation envelope that says two donations came from the same install
/// (<a href="https://github.com/dseelinger/d47/issues/176">#176</a>).
/// <para>
/// <b>This reverses a deliberate privacy decision, and the reversal is the point rather than a
/// side effect.</b> <see cref="Pseudonyms"/> says in as many words that two donations from the
/// same Commander must not be joinable, and
/// <a href="https://github.com/dseelinger/d47/issues/166">#166</a> lists that as a feature. It is
/// a feature. It is also incompatible with the thing
/// <a href="https://github.com/dseelinger/d47/issues/174">#174</a> was built to do: a corpus that
/// grows — one large first donation and then a day at a time — requires knowing that donation
/// seven came from the same install as donation one. Without that there is no corpus, only a pile
/// of blobs that cannot be appended to or deduplicated. There is no clever middle: once the
/// envelope carries a stable token, whoever holds the store can group them, whatever the body
/// says. So it is decided here, in writing, rather than discovered later by whoever notices that
/// the bucket groups neatly.
/// </para>
/// <para>
/// <b>Content pseudonyms are untouched.</b> They stay per-donation, by field list, exactly as they
/// are. What changed is what is written on the envelope, not what is written in the payload — and
/// the token never appears in the rendered report, because that report is the consent record and
/// putting an identifier inside it would put an identity into the one artefact the whole path
/// exists to keep identities out of.
/// </para>
/// <para>
/// <b>Random, and structurally incapable of being anything else.</b> <see cref="NewToken"/> takes
/// no arguments. A derived token — a hash of a Commander name, a Frontier id, a machine id — is a
/// re-identifier wearing a UUID's clothes, and it fails silently: it looks opaque and is not. A
/// function with nothing to derive from cannot derive, which is a stronger guarantee than a
/// comment asking nobody to.
/// </para>
/// <para>
/// <b>Two residuals are accepted here rather than left to be found later</b>, both because
/// <a href="https://github.com/dseelinger/d47/issues/167">#167</a>'s rule is that a residual is
/// decided as acceptable in writing or it is not decided at all.
/// </para>
/// <para>
/// <i>Re-identification gets easier, and it was never zero.</i> #167 already flags that
/// pseudonymised is not anonymous — a jump sequence with timestamps, matched against a public EDSM
/// or Inara history, is not obviously defeated by stand-in names. Thirteen months of accumulation
/// makes that materially easier than a ten-minute excerpt does: more surface, more distinctive
/// routes, more chance one rare system pins the whole set to a person. <b>Accepted.</b> The
/// donation is voluntary, per-press, described in full before it leaves, and erasable by one
/// object delete; the alternative that removes this residual also removes the feature. What is
/// refused is leaving it unsaid — a donor reads this before the first donation, not after.
/// </para>
/// <para>
/// <i>A fork is possible, and it is accepted rather than defended against.</i> The token lives in
/// <c>data\</c> beside everything else d47 writes, so an ordinary upgrade keeps it: the installer
/// and <c>get-local</c> never touch that folder and <c>tools/data-backup.ps1</c> snapshots it.
/// What does not survive is a second PC, a fresh install with no restore, or a Commander who
/// clears the folder — each starts a second pile under a new token, and the two halves of one
/// history are then indistinguishable from two donors. <b>Accepted, and stated.</b> The only
/// alternative is a token that outlives <c>data\</c>, which means putting it somewhere
/// machine-shaped, which is exactly the derived identifier the paragraph above refuses. A split
/// corpus costs an accumulation. A machine-derived token costs the property this whole design is
/// for.
/// </para>
/// </summary>
public static class DonorToken
{
    /// <summary>
    /// Sixteen bytes, written as thirty-two lowercase hex characters.
    /// <para>
    /// Hex rather than base64 or base32 because this travels in an HTTP header and lands in an
    /// object key: one unambiguous alphabet, no padding, no case question, nothing to escape, and
    /// a shape the endpoint can refuse on sight. 128 bits is what a v4 GUID spends on randomness
    /// and it is not the weak link in anything here.
    /// </para>
    /// </summary>
    public const int Bytes = 16;

    /// <summary>How long a written token is. The endpoint checks this exact length.</summary>
    public const int Length = Bytes * 2;

    /// <summary>
    /// A fresh token. <b>Takes no arguments, on purpose</b> — see the note on this class about
    /// derivation. Cryptographic randomness rather than <c>Guid.NewGuid</c> so there is no version
    /// nibble, no variant bits, and nothing in the value that says anything at all.
    /// </summary>
    public static string NewToken() =>
        Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(Bytes));

    /// <summary>
    /// Whether a string is a token this build would have written. Used on the way out as well as
    /// on the way in: a hand-edited file must fail as "no token" rather than travel as one.
    /// </summary>
    public static bool IsWellFormed(string? token) =>
        token is { Length: Length } && token.All(IsLowerHex);

    /// <summary>
    /// The token on this installation, or null where there is none. <b>Never creates one</b> — a
    /// read is a read, and a token that came into existence because something asked whether it
    /// existed is a token nobody consented to.
    /// </summary>
    public static string? Read(string file)
    {
        try
        {
            if (!File.Exists(file))
            {
                return null;
            }

            var read = File.ReadAllText(file).Trim();
            return IsWellFormed(read) ? read : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Unreadable is the same answer as absent, and deliberately so: the caller's next move
            // either way is to mint one, and a donation must not fail because a file was locked.
            return null;
        }
    }

    /// <summary>
    /// The token on this installation, minting and writing one if there is none.
    /// <para>
    /// <b>Called from the send, not from startup.</b> An installation that has never donated has
    /// no token and no file — which is what makes "have you got an identifier for me" answerable
    /// with "not until you donate" rather than with an explanation.
    /// </para>
    /// </summary>
    public static string Ensure(string file)
    {
        if (Read(file) is { } existing)
        {
            return existing;
        }

        var minted = NewToken();
        AtomicFile.WriteAllText(file, minted + Environment.NewLine);
        return minted;
    }

    /// <summary>
    /// Withdrawal. Deleting the token ends the linkage going forward and costs the donor nothing
    /// but their accumulation — no posting anywhere, no request to anybody, and nothing harder
    /// than the consent that started it, which is #167's criterion.
    /// <para>
    /// <b>It does not reach back.</b> What has already been sent stays under the old token until
    /// it is deleted at the store, which is a separate act and is the endpoint's business rather
    /// than this file's. Saying so is the whole reason this returns what it deleted.
    /// </para>
    /// </summary>
    /// <returns>The token that was forgotten, or null where there was nothing to forget.</returns>
    public static string? Forget(string file)
    {
        var held = Read(file);

        try
        {
            File.Delete(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        return held;
    }

    /// <summary>What a Commander is told about their own token, on the privacy page.</summary>
    public static string Summarise(string? token) =>
        token is null
            ? "No donation identifier exists on this installation. One is created the first time "
              + "you donate, and never before."
            : string.Create(
                CultureInfo.InvariantCulture,
                $"Your donations are grouped under {token}. It is a random number made on this "
                + $"machine, it is not derived from your Commander name or anything else about "
                + $"you, and it is used for donations and nothing else. Forgetting it stops future "
                + $"donations joining the ones already sent.");

    private static bool IsLowerHex(char character) =>
        character is >= '0' and <= '9' or >= 'a' and <= 'f';
}
