using System.IO.Compression;
using D47.Core;
using D47.Core.Diagnostics.Donation;
using Microsoft.Extensions.Logging;

namespace D47.App.Donation;

/// <summary>What a send left behind: what the endpoint said, and where d47's own copy landed.</summary>
/// <param name="Outcome">What happened.</param>
/// <param name="Receipt">The receipt's path, or null where none could be written.</param>
public sealed record DonationSent(DonationOutcome Outcome, string? Receipt);

/// <summary>What a withdrawal left behind: what the store said, and where d47's own copy landed.</summary>
/// <param name="Outcome">What happened.</param>
/// <param name="Receipt">The receipt's path, or null where none could be written.</param>
public sealed record DonationForgotten(ErasureOutcome Outcome, string? Receipt);

/// <summary>
/// How far a corpus send has got, for a window to say
/// (<a href="https://github.com/dseelinger/d47/issues/181">#181</a>).
/// <para>
/// Two steps rather than a percentage, because they are two different waits and only one of them
/// can be counted: assembling the payload is journal files, and there is a number for that; the
/// upload afterwards is one POST of tens of megabytes with nothing to report until it answers.
/// A bar that sat at "reading 936 files" through a two-minute upload would be saying the wrong
/// thing rather than saying nothing.
/// </para>
/// </summary>
/// <param name="Sending">False while the payload is being assembled, true once it is on the wire.</param>
/// <param name="Files">How many journal files have been read.</param>
public sealed record DonationStep(bool Sending, int Files);

/// <summary>
/// Everything between "the Commander pressed send" and "the store has it"
/// (<a href="https://github.com/dseelinger/d47/issues/175">#175</a>): mint the token if there is
/// none, seal the envelope, post it, and write the receipt.
/// <para>
/// <b>Here rather than in the window, and rather than in <see cref="DonationUpload"/>.</b> The
/// window's job is to show what would leave and take a yes; the upload's job is one POST. This is
/// the order the four steps go in, which is the part that has to be the same wherever a donation
/// is pressed — and it is the part a test can drive without a window or a socket.
/// </para>
/// <para>
/// <b>The token is minted here, at the send.</b> An installation that has never donated has no
/// identifier and no file: creating one at startup would put an identifier on a machine that never
/// asked for one, and "have you got an identifier for me" should be answerable with "not until you
/// donate" (<a href="https://github.com/dseelinger/d47/issues/176">#176</a>).
/// </para>
/// <para>
/// <b>The receipt is written whichever way it went</b>, and a receipt that cannot be written never
/// stops a send — see <see cref="DonationReceipt"/> for why both.
/// </para>
/// </summary>
public sealed class DonationDispatch
{
    private readonly AppPaths _paths;
    private readonly Func<string?> _endpoint;
    private readonly DonationUpload _upload;
    private readonly ILogger? _log;

    /// <param name="paths">Where the token and the receipts live.</param>
    /// <param name="endpoint">
    /// The configured address, read at the moment of sending rather than captured — a Commander
    /// who sets it in the panel with the donation window open should not have to reopen the window.
    /// </param>
    public DonationDispatch(
        AppPaths paths,
        Func<string?> endpoint,
        DonationUpload upload,
        ILogger? log = null)
    {
        _paths = paths;
        _endpoint = endpoint;
        _upload = upload;
        _log = log;
    }

    /// <summary>
    /// The ordinary construction, said once
    /// (<a href="https://github.com/dseelinger/d47/issues/181">#181</a>).
    /// <para>
    /// Three places now need one — the excerpt window, the journal-history window, and the
    /// withdrawal press on the privacy page — and three constructions are three chances for them
    /// to disagree about where a donation goes or which logger says so.
    /// </para>
    /// </summary>
    public static DonationDispatch For(
        AppPaths paths, Func<string?> endpoint, ILoggerFactory loggers)
    {
        var log = loggers.CreateLogger("Donation");
        return new DonationDispatch(paths, endpoint, new DonationUpload(log: log), log);
    }

    /// <summary>Whether there is anywhere to send to. Decides whether a window offers a send at all.</summary>
    public bool CanSend => DonationUpload.IsUsable(_endpoint());

    /// <summary>Where a send would go, for the window to name before it happens.</summary>
    public string? Destination =>
        _endpoint() is { } endpoint && DonationUpload.IsUsable(endpoint)
            ? DonationUpload.Destination(endpoint)
            : null;

    /// <summary>
    /// An incident excerpt, where <b>the document is the payload</b> — the same string that filled
    /// the pane, byte for byte, which is what makes its receipt checkable with a hash.
    /// </summary>
    public Task<DonationSent> SendExcerptAsync(
        string document,
        ExcerptPaperwork paperwork,
        CancellationToken cancel = default) =>
        SendAsync(
            DonationEnvelope.Excerpt,
            document,
            DonationEnvelope.Utf8.GetBytes(document),
            documentIsPayload: true,
            paperwork,
            cancel);

    /// <summary>
    /// The general form, for a donation whose payload is not the document the Commander read — a
    /// journal history, where what was read is <see cref="CorpusReport"/> and what leaves is
    /// hundreds of thousands of scrubbed lines.
    /// </summary>
    /// <param name="kind">
    /// <see cref="DonationEnvelope.Excerpt"/> or <see cref="DonationEnvelope.Corpus"/>. Decides
    /// the prefix at the store, and therefore which of two opposite retention rules applies.
    /// </param>
    /// <param name="consentDocument">What was on screen when the Commander said yes, verbatim.</param>
    /// <param name="payload">The scrubbed bytes, exactly as the document described them.</param>
    /// <param name="documentIsPayload">
    /// Whether those two are the same bytes. Never guessed from <paramref name="kind"/>: it
    /// decides which claim the receipt makes about its own hash.
    /// </param>
    public async Task<DonationSent> SendAsync(
        string kind,
        string consentDocument,
        ReadOnlyMemory<byte> payload,
        bool documentIsPayload,
        ExcerptPaperwork paperwork,
        CancellationToken cancel = default)
    {
        var endpoint = _endpoint();

        if (endpoint is null || !DonationUpload.IsUsable(endpoint))
        {
            return new DonationSent(
                DonationOutcome.Refused(
                    "No donation address is set, so nothing was sent. Copy it or save a file "
                    + "instead."),
                Receipt: null);
        }

        // Minted at the send, and only here. See the note on this class.
        var donor = DonorToken.Ensure(_paths.DonorTokenFile);

        var envelope = DonationEnvelope.For(kind, donor, paperwork, payload.Span);
        var outcome = await _upload.SendAsync(endpoint, envelope, payload, cancel);

        var receipt = DonationReceipt.Write(
            _paths.Donations,
            envelope,
            outcome,
            DonationUpload.Destination(endpoint),
            consentDocument,
            documentIsPayload);

        if (receipt is null)
        {
            _log?.LogWarning("Could not write a donation receipt to {Folder}.", _paths.Donations);
        }

        return new DonationSent(outcome, receipt);
    }

    /// <summary>
    /// A journal history, where <b>the payload is never held whole</b> and the document the
    /// Commander read is the report about it rather than the thing itself
    /// (<a href="https://github.com/dseelinger/d47/issues/181">#181</a>).
    /// <para>
    /// <b>Spooled, hashed on the way past, and posted from the spool.</b> That is #181's open
    /// question answered, and the answer is forced rather than chosen: the endpoint refuses a
    /// donation that does not declare its length, so the compressed length has to be known before
    /// the request — which means the compressed bytes exist somewhere seekable first. Given a
    /// spool, a second walk over the journals purely to hash would be reading 383 MB twice to
    /// learn something the first walk could have told us. See <see cref="TallyStream"/>.
    /// </para>
    /// <para>
    /// <b>The receipt's hash is of exactly the bytes that left</b>, because it is taken from the
    /// same pass that produced them — not from a survey, not from a second read of the journals
    /// that Elite may have appended to in between.
    /// </para>
    /// </summary>
    /// <param name="consentDocument">The report that was on screen when the Commander said yes.</param>
    /// <param name="write">
    /// Writes the payload to a stream. <b>The same delegate the window's Save button uses</b>, so
    /// the file a Commander could have saved and the bytes that leave are one code path — which
    /// is the corpus half of "what is shown is what leaves".
    /// </param>
    public async Task<DonationSent> SendCorpusAsync(
        string consentDocument,
        Func<Stream, IProgress<int>, CancellationToken, Task> write,
        ExcerptPaperwork paperwork,
        IProgress<DonationStep>? progress = null,
        CancellationToken cancel = default)
    {
        var endpoint = _endpoint();

        // Checked before anything is spooled. Assembling tens of megabytes to discover there is
        // nowhere to send them would be the whole cost of the feature for none of the benefit.
        if (endpoint is null || !DonationUpload.IsUsable(endpoint))
        {
            return new DonationSent(
                DonationOutcome.Refused(
                    "No donation address is set, so nothing was sent. Save it to a file instead."),
                Receipt: null);
        }

        var donor = DonorToken.Ensure(_paths.DonorTokenFile);
        var files = 0;

        var counting = new Progress<int>(read =>
        {
            files = read;
            progress?.Report(new DonationStep(Sending: false, read));
        });

        DonationEnvelope envelope;
        DonationOutcome outcome;

        try
        {
            Directory.CreateDirectory(_paths.Donations);

            // **Beside the executable in data\, like everything else d47 writes**, rather than in
            // the system temp folder — and DeleteOnClose, so the spool goes whatever happens to
            // this method, including a process that never reaches the finally.
            var spooled = Path.Combine(
                _paths.Donations,
                $"{DonationEnvelope.Stamp(paperwork.TakenAt)}-{DonationEnvelope.Corpus}.sending");

            await using var spool = new FileStream(
                spooled,
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 64 * 1024,
                FileOptions.DeleteOnClose | FileOptions.Asynchronous);

            var (bytes, sha256) = await SpoolAsync(spool, write, counting, cancel);

            await spool.FlushAsync(cancel);
            spool.Position = 0;

            progress?.Report(new DonationStep(Sending: true, files));

            envelope = DonationEnvelope.For(
                DonationEnvelope.Corpus, donor, paperwork, bytes, sha256);

            outcome = await _upload.SendAsync(endpoint, envelope, spool, cancel);
        }
        catch (OperationCanceledException)
        {
            return new DonationSent(
                DonationOutcome.Refused("Stopped. Nothing was confirmed as stored."),
                Receipt: null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log?.LogWarning(ex, "Could not assemble a corpus donation in {Folder}.", _paths.Donations);

            return new DonationSent(
                DonationOutcome.Refused(
                    "d47 could not assemble the donation on this machine, so nothing was sent."),
                Receipt: null);
        }

        // **documentIsPayload: false, and never guessed from the kind.** What is kept beside the
        // executable is the report the Commander read; the payload itself is hundreds of megabytes
        // and a second copy of it on their own disk would tell them nothing the hash does not.
        var receipt = DonationReceipt.Write(
            _paths.Donations,
            envelope,
            outcome,
            DonationUpload.Destination(endpoint),
            consentDocument,
            documentIsPayload: false);

        if (receipt is null)
        {
            _log?.LogWarning("Could not write a donation receipt to {Folder}.", _paths.Donations);
        }

        return new DonationSent(outcome, receipt);
    }

    /// <summary>
    /// Withdrawal (<a href="https://github.com/dseelinger/d47/issues/167">#167</a>): the store is
    /// asked to delete every donation made from this installation, and then the identifier they
    /// were grouped under is forgotten here.
    /// <para>
    /// <b>That order, and it matters.</b> The token is the only handle anybody has on what was
    /// sent — the store cannot find it without one and neither can the Commander — so forgetting
    /// it first and then failing to reach the store would strand the data permanently under a
    /// name nobody holds. A refused erasure therefore <b>keeps the token</b> and says so, and the
    /// press can simply be made again.
    /// </para>
    /// <para>
    /// <b>Never <see cref="DonorToken.Ensure"/>.</b> An installation that never donated has no
    /// identifier, and minting one in order to forget it would be the funniest possible way to
    /// break the rule that a token exists only because somebody donated.
    /// </para>
    /// </summary>
    public async Task<DonationForgotten> ForgetAsync(CancellationToken cancel = default)
    {
        var at = DateTimeOffset.Now;
        var endpoint = _endpoint();
        var token = DonorToken.Read(_paths.DonorTokenFile);

        if (token is null)
        {
            return new DonationForgotten(
                ErasureOutcome.NotAsked(
                    "There is no donation identifier on this installation, so there is nothing to "
                    + "forget and nothing was ever grouped under one."),
                Receipt: null);
        }

        var outcome = endpoint is { } address && DonationUpload.IsUsable(address)
            ? await _upload.ForgetAsync(address, token, cancel)
            : ErasureOutcome.NotAsked(
                "No donation address is set, so d47 could not ask a store to delete anything. The "
                + "identifier on this machine is forgotten, which stops future donations joining "
                + "the ones already sent.");

        // Kept where the store was asked and refused — see the note on this method. Forgotten
        // where it answered, and forgotten too where there was nobody to ask, which is the case
        // an installation that never had an address configured is in.
        if (outcome.Answered || !outcome.Asked)
        {
            DonorToken.Forget(_paths.DonorTokenFile);
        }

        var receipt = DonationErasure.Write(
            _paths.Donations,
            at,
            token,
            outcome,
            endpoint is { } where && DonationUpload.IsUsable(where)
                ? DonationUpload.Destination(where, DonationUpload.ForgetPath)
                : null);

        if (receipt is null)
        {
            _log?.LogWarning("Could not write an erasure receipt to {Folder}.", _paths.Donations);
        }

        return new DonationForgotten(outcome, receipt);
    }

    /// <summary>
    /// Writes the payload into the spool, compressing it and hashing it in one pass.
    /// <para>
    /// The tuple is read before the <c>GZipStream</c> is disposed and that is correct rather than
    /// lucky: <see cref="TallyStream"/> counts and hashes at the moment of writing, above the
    /// compressor, so both are final as soon as the caller's last write has returned. What
    /// disposal adds is the gzip footer, which is not payload and is deliberately not hashed.
    /// </para>
    /// </summary>
    private static async Task<(long Bytes, string Sha256)> SpoolAsync(
        Stream spool,
        Func<Stream, IProgress<int>, CancellationToken, Task> write,
        IProgress<int> progress,
        CancellationToken cancel)
    {
        await using var gzip = new GZipStream(spool, CompressionLevel.Optimal, leaveOpen: true);
        using var tally = new TallyStream(gzip);

        await write(tally, progress, cancel);
        await tally.FlushAsync(cancel);

        return (tally.Bytes, tally.Sha256);
    }
}
