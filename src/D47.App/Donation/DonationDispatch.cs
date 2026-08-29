using D47.Core;
using D47.Core.Diagnostics.Donation;
using Microsoft.Extensions.Logging;

namespace D47.App.Donation;

/// <summary>What a send left behind: what the endpoint said, and where d47's own copy landed.</summary>
/// <param name="Outcome">What happened.</param>
/// <param name="Receipt">The receipt's path, or null where none could be written.</param>
public sealed record DonationSent(DonationOutcome Outcome, string? Receipt);

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
}
