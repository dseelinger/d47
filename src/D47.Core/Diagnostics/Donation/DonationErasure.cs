using System.Globalization;
using System.Text;
using D47.Core.Storage;

namespace D47.Core.Diagnostics.Donation;

/// <summary>
/// What an erasure request left behind
/// (<a href="https://github.com/dseelinger/d47/issues/167">#167</a>).
/// </summary>
/// <param name="Asked">
/// Whether the store was asked at all. <b>False is not a failure by itself</b>: an installation
/// with no address configured has nowhere to ask, and one that never donated has nothing to ask
/// about. It is false again when the store refused, which is the case that must not be mistaken
/// for the other two.
/// </param>
/// <param name="Answered">Whether the store said it had done it. False where it was never asked, or refused.</param>
/// <param name="Deleted">How many objects went.</param>
/// <param name="More">
/// Whether the store said there is more behind what it deleted. It bounds one press rather than
/// paging for ever, so a donor past that ceiling is <b>told</b> to press again rather than left
/// believing a partial deletion was the whole of it.
/// </param>
/// <param name="Keys">What went, so a receipt can name it rather than claim it.</param>
/// <param name="Said">What happened, in words, whichever way it went.</param>
public sealed record ErasureOutcome(
    bool Asked,
    bool Answered,
    int Deleted,
    bool More,
    IReadOnlyList<string> Keys,
    string Said)
{
    /// <summary>The store did it.</summary>
    public static ErasureOutcome Done(int deleted, bool more, IReadOnlyList<string> keys) =>
        new(
            Asked: true,
            Answered: true,
            deleted,
            more,
            keys,
            more
                ? string.Create(
                    CultureInfo.InvariantCulture,
                    $"{deleted:N0} donations were deleted at the store, and there are more behind "
                    + $"them. Press again to take the rest.")
                : deleted == 0
                    ? "The store had nothing under this identifier, so there was nothing to delete."
                    : string.Create(
                        CultureInfo.InvariantCulture,
                        $"{deleted:N0} donations were deleted at the store."));

    /// <summary>The store was asked and would not.</summary>
    public static ErasureOutcome Refused(string said) =>
        new(Asked: true, Answered: false, Deleted: 0, More: false, Keys: [], said);

    /// <summary>There was nobody to ask.</summary>
    public static ErasureOutcome NotAsked(string said) =>
        new(Asked: false, Answered: false, Deleted: 0, More: false, Keys: [], said);
}

/// <summary>
/// The donor's own copy of a withdrawal, written on this machine at the moment of asking
/// (<a href="https://github.com/dseelinger/d47/issues/167">#167</a>).
/// <para>
/// <b>The mirror image of <see cref="DonationReceipt"/>, and for the same reason.</b> A donation
/// leaves the Commander evidence of what it sent; a withdrawal has to leave them evidence of what
/// went — otherwise "it is deleted" is a claim about somebody else's disk that the person who
/// asked for it cannot check or quote afterwards. This names the objects the store said it
/// deleted, and it is written whether or not the store answered, because a refused erasure is a
/// thing that happened to their data too.
/// </para>
/// <para>
/// <b>It keeps the identifier that was forgotten, on purpose.</b> That looks backwards for a file
/// whose subject is being forgotten, and it is the one thing the donor cannot get back any other
/// way: the token is gone from <c>data\</c> the moment this is written, and without it nobody —
/// the custodian included — can find what an incomplete deletion left behind. A donor who wants
/// no trace at all deletes this file too, and is told so on it.
/// </para>
/// </summary>
public static class DonationErasure
{
    /// <summary>What the file is called. Stamped, so pressing twice does not overwrite the first.</summary>
    public static string NameFor(DateTimeOffset at) =>
        $"{DonationEnvelope.Stamp(at)}-erasure.md";

    /// <summary>
    /// Writes it and returns where it landed, or null where nothing could be written.
    /// <b>A receipt that cannot be written never stops an erasure</b> — the same rule
    /// <see cref="DonationReceipt"/> follows, for the same reason.
    /// </summary>
    public static string? Write(
        string folder,
        DateTimeOffset at,
        string? token,
        ErasureOutcome outcome,
        string? destination)
    {
        try
        {
            Directory.CreateDirectory(folder);

            var receipt = Path.Combine(folder, NameFor(at));
            AtomicFile.WriteAllText(receipt, Render(at, token, outcome, destination));

            return receipt;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>The receipt itself. Separate from the writing so a test can read it without a disk.</summary>
    public static string Render(
        DateTimeOffset at,
        string? token,
        ErasureOutcome outcome,
        string? destination)
    {
        var receipt = new StringBuilder();

        receipt.AppendLine("# d47 erasure receipt");
        receipt.AppendLine();
        receipt.AppendLine($"Asked {Stamp(at)}.");
        receipt.AppendLine();
        receipt.AppendLine($"> {outcome.Said}");
        receipt.AppendLine();

        receipt.AppendLine("## What was asked, and of whom");
        receipt.AppendLine();
        receipt.AppendLine("| | |");
        receipt.AppendLine("|---|---|");
        receipt.AppendLine($"| The identifier that was forgotten | `{token ?? "none — there was none to forget"}` |");
        receipt.AppendLine($"| Where it was asked | {(destination is { Length: > 0 } where ? $"`{where}`" : "nowhere — no donation address was set")} |");
        receipt.AppendLine($"| Objects deleted | {outcome.Deleted.ToString("N0", CultureInfo.InvariantCulture)} |");
        receipt.AppendLine($"| More left behind | {(outcome.More ? "**yes — press Forget again**" : "no")} |");

        if (outcome.Keys.Count > 0)
        {
            receipt.AppendLine();
            receipt.AppendLine("## What went");
            receipt.AppendLine();

            foreach (var key in outcome.Keys)
            {
                receipt.AppendLine($"- `{key}`");
            }
        }

        receipt.AppendLine();
        receipt.AppendLine("## What this does not reach");
        receipt.AppendLine();

        receipt.AppendLine(
            outcome.Answered
                ? "The objects above are gone from the store, and the identifier they were grouped "
                  + "under is gone from this machine — so donations made from here after today "
                  + "start a fresh pile with nothing joining them to these."
                : "**Nothing is confirmed deleted at the store.** The identifier above is what a "
                  + "custodian needs to find and delete what was sent, which is why it is written "
                  + "here rather than only in the file that has just been removed.");

        receipt.AppendLine();
        receipt.AppendLine(
            "**A fix is not deleted with the data that produced it.** A defect found from a "
            + "donation stays fixed, the release that carried the fix stays released, and the "
            + "changelog line naming it stays written. Those are what was decided because of the "
            + "data rather than the data itself, and a published release never moves.");

        receipt.AppendLine();
        receipt.AppendLine(
            "**Anything you copied or saved yourself is outside all of this.** A donation window "
            + "will put an excerpt on the clipboard or write it to a file if you ask it to, and "
            + "where those went afterwards is not something d47 can reach — anything posted "
            + "publicly can be archived beyond anyone's reach.");

        receipt.AppendLine();
        receipt.AppendLine(
            "This file is the only remaining copy of the identifier above. Delete it too if you "
            + "want no trace of it on this machine, knowing that nobody can then find what an "
            + "incomplete deletion left behind.");

        return receipt.ToString();
    }

    private static string Stamp(DateTimeOffset at) =>
        at.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ssZ", CultureInfo.InvariantCulture);
}
