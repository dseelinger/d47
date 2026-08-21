namespace D47.Core.Conversation;

/// <summary>
/// The thing d47 last offered to put on the Commander's clipboard, so <i>"copy that"</i> has
/// something to mean (asked for 2026-08-21).
/// <para>
/// <b>The asking already worked; the offer did not exist.</b> Both halves have been here for
/// phases — a search that finds the nearest system holding a material, and a tool that puts a name
/// on the clipboard — and nothing joined them. A Commander who asked where to find Imperial
/// Shielding got a list of systems and then had to read a name off it and type it into the galaxy
/// map, which is the errand `copy_to_clipboard` was built to end.
/// </para>
/// <para>
/// <b>In memory and never on disk.</b> An offer is a moment in a conversation, not a preference:
/// restoring one from last week would have d47 offering to copy a system nobody is talking about.
/// This is the same distinction <see cref="Checklists.ChecklistService.Selected"/> makes and for
/// the same reason.
/// </para>
/// <para>
/// <b>One at a time, deliberately.</b> "That" means the last thing said, and a stack of past offers
/// would let a stale one answer — which is the failure a selection with no antecedent already
/// caused once on the checklist.
/// </para>
/// </summary>
public sealed class ClipboardOffer
{
    /// <summary>What would go on the clipboard — a system name, almost always.</summary>
    public string? Text { get; private set; }

    /// <summary>
    /// What it is, in the words the offer sentence used: "the system", "the trader's system". Kept
    /// so the confirmation can name the same thing the offer did rather than saying "copied" and
    /// leaving the Commander to guess which.
    /// </summary>
    public string? Subject { get; private set; }

    public bool IsStanding => Text is { Length: > 0 };

    /// <summary>
    /// Records what is being offered and returns the sentence to say. Null <paramref name="text"/>
    /// clears the offer and returns nothing to add — which is what an answer that found no place
    /// wants, so a failed search cannot leave the previous system standing as "that".
    /// </summary>
    public string? Offer(string? text, string subject)
    {
        if (text is not { Length: > 0 } wanted)
        {
            Text = null;
            Subject = null;
            return null;
        }

        Text = wanted;
        Subject = subject;

        return $"Say \"copy that\" and I will put {wanted} on your clipboard.";
    }

    /// <summary>
    /// <b>An offer stands until the next answer replaces it</b>, rather than being spent when it is
    /// taken up. Saying "copy that" twice copies the same system twice, which is what a Commander
    /// who did not hear the first confirmation means by it — where a spent offer would answer the
    /// second one with nothing at all.
    /// </summary>
    /// <summary>
    /// The phrases that take up a standing offer, as router commands carrying the text itself.
    /// <para>
    /// <b>Dynamic, because the argument is the whole point.</b> A declared phrase carries fixed
    /// arguments, and what is being copied changes with every answer — so this is the same
    /// mechanism the Commander's own macro names use, for the same reason.
    /// </para>
    /// <para>
    /// <b>No bare "yes".</b> That word is already spoken for while a checklist proposal is waiting,
    /// and a router phrase that means two things depending on what else is going on is a phrase
    /// nobody can predict. Every phrase here says the word copy or names the clipboard.
    /// </para>
    /// </summary>
    public IEnumerable<DynamicCommand> Phrases()
    {
        if (Text is not { Length: > 0 } text)
        {
            yield break;
        }

        var arguments = new Dictionary<string, string>(StringComparer.Ordinal) { ["text"] = text };

        foreach (var phrase in Taking)
        {
            yield return new DynamicCommand(
                phrase, Capabilities.Builtin.NavigationCapability.Id, "copy_to_clipboard", arguments);
        }
    }

    private static readonly string[] Taking =
    [
        "copy that", "copy it", "copy that one", "copy the system", "copy that system",
        "put it on my clipboard", "put that on my clipboard", "copy to clipboard",
        "yes copy it", "yes please copy it", "copy that please",
    ];
}
