using System.Globalization;
using System.Text;
using D47.Core.Storage;

namespace D47.Core.Diagnostics.Donation;

/// <summary>What the endpoint said.</summary>
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

/// <summary>The donor's own copy, written on this machine at the moment of sending (#175).</summary>
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
    /// Writes both files and returns where the receipt landed, or null where nothing could be written.
    /// </summary>
    /// <param name="folder">Where receipts live — <c>data\donations</c>.</param>
    /// <param name="envelope">The envelope as sent.</param>
    /// <param name="outcome">What the endpoint said.</param>
    /// <param name="destination">The endpoint the request was made to.</param>
    /// <param name="document">The artefact the Commander read, verbatim.</param>
    /// <param name="documentIsPayload">
    /// Whether <paramref name="document"/> is the payload byte for byte.
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

            // The document first.
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

    /// <summary>The receipt itself.</summary>
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

        // **It named the object and never said who to name it to** (#166).
        receipt.AppendLine(DonationNotice.Line);

        receipt.AppendLine();
        receipt.AppendLine(
            envelope.Kind == DonationEnvelope.Corpus
                ? "A journal history is kept **indefinitely** — that is what it is for; a "
                  + "regression case that expires stops being one. It goes when you ask."
                // **Thirty days, and nothing else claimed as a mechanism** (#167).
                : "An incident excerpt is deleted **30 days** after it arrives, by a rule on the "
                  + "store rather than by anybody remembering to. You do not have to ask. It may "
                  + "well go sooner — there is no reason to keep one past the defect it was cut "
                  + "for — but that is somebody deleting it and not a rule, so the thirty days is "
                  + "what is promised. Asking for it sooner is one press, and immediate.");

        receipt.AppendLine();
        receipt.AppendLine(
            $"Deleting `{AppPaths.DataFolderName}\\{Path.GetFileName(DonorTokenFileName)}` stops "
            + "future donations joining these ones. It does not reach back: what has already been "
            + "sent stays under the identifier above until it is deleted at the store.");

        return receipt.ToString();
    }

    /// <summary>
    /// The token file's name, said here so the receipt's withdrawal sentence and <see
    /// cref="AppPaths.DonorTokenFile"/> cannot come to disagree about what to delete.
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
