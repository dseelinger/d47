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
/// How far a corpus send has got, for a window to say (#181), and how far through the upload it is
/// (#212).
/// </summary>
/// <param name="Sending">
/// False while the payload is being assembled, true once it is on the wire.
/// </param>
/// <param name="Files">How many journal files have been read.</param>
/// <param name="Sent">How many compressed bytes have been handed to the network stack.</param>
/// <param name="Total">How many there are altogether.</param>
public sealed record DonationStep(bool Sending, int Files, long Sent = 0, long Total = 0)
{
    /// <summary>
    /// How far along, nought to one — or null where there is nothing to draw: the preparing step, which
    /// is counted in files rather than in bytes, and an upload with no length.
    /// </summary>
    public double? Fraction =>
        Sending && Total > 0 ? Math.Clamp((double)Sent / Total, 0, 1) : null;
}

/// <summary>
/// Everything between "the Commander pressed send" and "the store has it" (#175): mint the token if
/// there is none, seal the envelope, post it, and write the receipt.
/// </summary>
public sealed class DonationDispatch
{
    private readonly AppPaths _paths;
    private readonly Func<string?> _endpoint;
    private readonly DonationUpload _upload;
    private readonly ILogger? _log;

    /// <summary>
    /// <param name="paths">Where the token and the receipts live.</param> <param name="endpoint"> The
    /// configured address, read at the moment of sending rather than captured — a Commander who sets it
    /// in the panel with the donation window open should not have to reopen the window.
    /// </summary>
    /// <param name="paths">Where the token and the receipts live.</param>
    /// <param name="endpoint">
    /// The configured address, read at the moment of sending rather than captured — a Commander who
    /// sets it in the panel with the donation window open should not have to reopen the window.
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

    /// <summary>The ordinary construction, said once (#181).</summary>
    public static DonationDispatch For(
        AppPaths paths, Func<string?> endpoint, ILoggerFactory loggers)
    {
        var log = loggers.CreateLogger("Donation");
        return new DonationDispatch(paths, endpoint, new DonationUpload(log: log), log);
    }

    /// <summary>Whether there is anywhere to send to.</summary>
    public bool CanSend => DonationUpload.IsUsable(_endpoint());

    /// <summary>Where a send would go, for the window to name before it happens.</summary>
    public string? Destination =>
        _endpoint() is { } endpoint && DonationUpload.IsUsable(endpoint)
            ? DonationUpload.Destination(endpoint)
            : null;

    /// <summary>
    /// An incident excerpt, where the document is the payload — the same string that filled the pane,
    /// byte for byte, which is what makes its receipt checkable with a hash.
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
    /// journal history, where what was read is <see cref="CorpusReport"/> and what leaves is hundreds
    /// of thousands of scrubbed lines.
    /// </summary>
    /// <param name="kind">
    /// <see cref="DonationEnvelope.Excerpt"/> or <see cref="DonationEnvelope.Corpus"/>.
    /// </param>
    /// <param name="consentDocument">
    /// What was on screen when the Commander said yes, verbatim.
    /// </param>
    /// <param name="payload">The scrubbed bytes, exactly as the document described them.</param>
    /// <param name="documentIsPayload">Whether those two are the same bytes.</param>
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

        // Minted at the send, and only here.
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
    /// A journal history, where the payload is never held whole and the document the Commander read is
    /// the report about it rather than the thing itself (#181).
    /// </summary>
    /// <param name="consentDocument">
    /// The report that was on screen when the Commander said yes.
    /// </param>
    /// <param name="write">Writes the payload to a stream.</param>
    public async Task<DonationSent> SendCorpusAsync(
        string consentDocument,
        Func<Stream, IProgress<int>, CancellationToken, Task> write,
        ExcerptPaperwork paperwork,
        IProgress<DonationStep>? progress = null,
        CancellationToken cancel = default)
    {
        var endpoint = _endpoint();

        // Checked before anything is spooled.
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

            // **Beside the executable in data\, like everything else d47 writes**, rather than in the system
            // temp folder — and DeleteOnClose, so the spool goes whatever happens to this method, including a
            // process that never reaches the finally.
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

            // The denominator, read once, off the spool that holds exactly what will be posted.
            var wire = spool.Length;

            progress?.Report(new DonationStep(Sending: true, files, Sent: 0, Total: wire));

            envelope = DonationEnvelope.For(
                DonationEnvelope.Corpus, donor, paperwork, bytes, sha256);

            outcome = await _upload.SendAsync(
                endpoint,
                envelope,
                spool,
                progress is null ? null : new Uploading(progress, files, wire),
                cancel);
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

        // **documentIsPayload: false, and never guessed from the kind.** What is kept beside the executable
        // is the report the Commander read; the payload itself is hundreds of megabytes and a second copy of
        // it on their own disk would tell them nothing the hash does not.
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
    /// Withdrawal (#167): the store is asked to delete every donation made from this installation, and
    /// then the identifier they were grouped under is forgotten here.
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

        // Kept where the store was asked and refused — see the note on this method.
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

    /// <summary>Turns the upload's byte count into the step a window reads (#212).</summary>
    private sealed class Uploading : IProgress<long>
    {
        private readonly IProgress<DonationStep> _steps;
        private readonly int _files;
        private readonly long _total;

        public Uploading(IProgress<DonationStep> steps, int files, long total)
        {
            _steps = steps;
            _files = files;
            _total = total;
        }

        // The file count travels on through the send.
        public void Report(long sent) =>
            _steps.Report(new DonationStep(Sending: true, _files, sent, _total));
    }

    /// <summary>Writes the payload into the spool, compressing it and hashing it in one pass.</summary>
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
