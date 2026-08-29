using System.Globalization;
using System.Text;
using D47.Core.Storage;

namespace D47.Core.Diagnostics.Donation;

/// <summary>
/// What the endpoint said. <b>Every failure is a sentence a Commander can read</b>, because the
/// only thing worse than a donation that did not arrive is one that silently did not.
/// </summary>
/// <param name="Sent">Whether the store has it.</param>
/// <param name="Key">The object name the endpoint stored it under, where it said one.</param>
/// <param name="Said">What happened, in words, whichever way it went.</param>
public sealed record DonationOutcome(bool Sent, string? Key, string Said)
{
    public static DonationOutcome Stored(string key) =>
        new(Sent: true, key, $"Sent. The store has it as {key}.");

    public static DonationOutcome Refused(string said) =>
        new(Sent: false, Key: null, said);
}

/// <summary>
/// The donor's own copy, written on this machine at the moment of sending
/// (<a href="https://github.com/dseelinger/d47/issues/175">#175</a>).
/// <para>
/// <b>This is what an upload costs, and what buys it back.</b>
/// <a href="https://github.com/dseelinger/d47/issues/160">#160</a>'s verification was that the
/// human was the transport: "what is shown is what leaves" was observable because the Commander
/// carried the bytes themselves. An upload turns that into a claim about code. The receipt turns
/// it back into something checkable — the artefact they read, kept byte for byte, and the hash of
/// the payload beside it, so the claim can be tested with an ordinary <c>sha256sum</c> rather than
/// believed.
/// </para>
/// <para>
/// <b>Two files, and the split is the honest part.</b> One is the document that was on screen,
/// verbatim. The other is the envelope and what the endpoint said. For an excerpt those are the
/// same bytes twice over — the document <i>is</i> the payload — and the receipt says so. For a
/// corpus the payload is hundreds of megabytes and is deliberately not kept: what is kept is the
/// report that was consented to, which is the artefact the Commander actually read, plus the hash
/// that makes the unkept part checkable by anyone who later holds it. Writing 383 MB beside the
/// executable to prove a point about 32 MB of it is not a receipt, it is a second copy of the
/// problem.
/// </para>
/// <para>
/// <b>Written whether or not the send worked.</b> A refused donation is a thing that happened to a
/// Commander's data and they should be able to see what was attempted — and a receipt that exists
/// only on success is one that quietly rewrites a failed upload as an upload that never occurred.
/// </para>
/// </summary>
public static class DonationReceipt
{
    /// <summary>
    /// The two file names for one donation, both derived from the envelope so neither can name a
    /// different donation from the other.
    /// </summary>
    /// <param name="envelope">What is being sent.</param>
    /// <returns>The document's name, and the receipt's.</returns>
    public static (string Document, string Receipt) NamesFor(DonationEnvelope envelope)
    {
        var stem = $"{DonationEnvelope.Stamp(envelope.TakenAt)}-{envelope.Kind}";
        return ($"{stem}.md", $"{stem}.receipt.md");
    }

    /// <summary>
    /// Writes both files and returns where the receipt landed, or null where nothing could be
    /// written.
    /// <para>
    /// <b>A receipt that cannot be written never stops a send.</b> It is evidence about a thing
    /// the Commander already decided to do; failing their donation because a folder was read-only
    /// would be the tail wagging the dog. The caller says so on screen instead.
    /// </para>
    /// </summary>
    /// <param name="folder">Where receipts live — <c>data\donations</c>.</param>
    /// <param name="envelope">The envelope as sent.</param>
    /// <param name="outcome">What the endpoint said.</param>
    /// <param name="destination">The endpoint the request was made to.</param>
    /// <param name="document">
    /// The artefact the Commander read, verbatim. For an excerpt this is the payload itself; for a
    /// corpus it is the report that described it.
    /// </param>
    /// <param name="documentIsPayload">
    /// Whether <paramref name="document"/> is the payload byte for byte. Decides which of two
    /// claims the receipt makes, and it must never be guessed from the kind: a caller that gets
    /// this wrong writes a receipt that says a hash covers a file it does not.
    /// </param>
    public static string? Write(
        string folder,
        DonationEnvelope envelope,
        DonationOutcome outcome,
        string destination,
        string document,
        bool documentIsPayload)
    {
        var (documentName, receiptName) = NamesFor(envelope);

        try
        {
            Directory.CreateDirectory(folder);

            // The document first. If only one of the two lands, the one worth having is the one
            // holding the bytes rather than the one describing them.
            AtomicFile.WriteAllText(Path.Combine(folder, documentName), document);

            var receipt = Path.Combine(folder, receiptName);
            AtomicFile.WriteAllText(
                receipt,
                Render(envelope, outcome, destination, documentName, documentIsPayload));

            return receipt;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>The receipt itself. Separate from the writing so a test can read it without a disk.</summary>
    public static string Render(
        DonationEnvelope envelope,
        DonationOutcome outcome,
        string destination,
        string documentName,
        bool documentIsPayload)
    {
        var receipt = new StringBuilder();

        receipt.AppendLine("# d47 donation receipt");
        receipt.AppendLine();
        receipt.AppendLine(
            outcome.Sent
                ? $"Sent {Stamp(envelope.TakenAt)} to `{destination}`."
                : $"Attempted {Stamp(envelope.TakenAt)} to `{destination}`, and **it did not arrive**.");

        receipt.AppendLine();
        receipt.AppendLine($"> {outcome.Said}");
        receipt.AppendLine();

        receipt.AppendLine("## What was on the envelope");
        receipt.AppendLine();
        receipt.AppendLine("| | |");
        receipt.AppendLine("|---|---|");
        receipt.AppendLine($"| What kind | {Describe(envelope.Kind)} |");
        receipt.AppendLine($"| Format | {envelope.Format.ToString(CultureInfo.InvariantCulture)} |");
        receipt.AppendLine($"| Your donation identifier | `{envelope.Donor ?? "none"}` |");
        receipt.AppendLine($"| Cut from build | {envelope.Build} |");
        receipt.AppendLine($"| Size of what was sent | {envelope.Bytes.ToString("N0", CultureInfo.InvariantCulture)} bytes |");
        receipt.AppendLine($"| SHA-256 of what was sent | `{envelope.Sha256}` |");
        receipt.AppendLine(
            $"| Stored as | {(outcome.Key is { } key ? $"`{key}`" : "nothing — it did not arrive")} |");

        receipt.AppendLine();
        receipt.AppendLine("## Checking it yourself");
        receipt.AppendLine();

        receipt.AppendLine(
            documentIsPayload
                ? $"`{documentName}` beside this file **is the payload**, byte for byte — the same "
                  + "text that was on screen when you pressed send, and the same bytes that left. "
                  + "Hash it and you should get the number above:"
                : $"`{documentName}` beside this file is the report you read and agreed to, kept "
                  + "byte for byte. The payload itself is not kept here — it runs to hundreds of "
                  + "megabytes, and a second copy of it on your own disk would not tell you "
                  + "anything the hash above does not. Anyone holding the stored object can hash "
                  + "it and compare:");

        receipt.AppendLine();
        receipt.AppendLine("```");
        receipt.AppendLine(
            documentIsPayload
                ? $"certutil -hashfile {documentName} SHA256"
                : $"certutil -hashfile <the object, ungzipped> SHA256");
        receipt.AppendLine("```");

        receipt.AppendLine();
        receipt.AppendLine(
            "The bytes are compressed for the journey and stored that way, so a downloaded object "
            + "has to be ungzipped before it will hash to this number. Compression is not covered "
            + "by the hash on purpose: it is not reproducible from the payload, so a hash over it "
            + "would prove the transfer and nothing you care about.");

        receipt.AppendLine();
        receipt.AppendLine("## Asking for it back");
        receipt.AppendLine();
        receipt.AppendLine(
            outcome.Key is { } stored
                ? "Quote the object name and the hash above. They name one object and no other, "
                  + $"and deleting it is a single delete: `{stored}`."
                : "Nothing arrived, so there is nothing to delete.");

        receipt.AppendLine();

        // **It named the object and never said who to name it to** (#166). A receipt that tells a
        // donor exactly what to quote and nothing about where to quote it is a withdrawal route
        // that stops one step short of being one — so the address goes here, next to the thing to
        // be quoted, rather than at the end of the section.
        receipt.AppendLine(DonationNotice.Line);

        receipt.AppendLine();
        receipt.AppendLine(
            envelope.Kind == DonationEnvelope.Corpus
                ? "A journal history is kept **indefinitely** — that is what it is for; a "
                  + "regression case that expires stops being one. It goes when you ask."
                : "An incident excerpt is kept for **30 days**, or until the defect it was cut for "
                  + "is closed, whichever comes first. You do not have to ask.");

        receipt.AppendLine();
        receipt.AppendLine(
            $"Deleting `{AppPaths.DataFolderName}\\{Path.GetFileName(DonorTokenFileName)}` stops "
            + "future donations joining these ones. It does not reach back: what has already been "
            + "sent stays under the identifier above until it is deleted at the store.");

        return receipt.ToString();
    }

    /// <summary>
    /// The token file's name, said here so the receipt's withdrawal sentence and
    /// <see cref="AppPaths.DonorTokenFile"/> cannot come to disagree about what to delete.
    /// </summary>
    private static string DonorTokenFileName => new AppPaths(".").DonorTokenFile;

    private static string Describe(string kind) => kind switch
    {
        DonationEnvelope.Corpus => "Journal history — a replay case, kept indefinitely",
        DonationEnvelope.Excerpt => "Incident excerpt — evidence for one defect, expiring",
        _ => kind,
    };

    private static string Stamp(DateTimeOffset at) =>
        at.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ssZ", CultureInfo.InvariantCulture);
}
