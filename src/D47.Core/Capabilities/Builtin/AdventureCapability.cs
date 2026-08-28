namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// Adventures — stories the Commander flies, told by the ship's AI (Phase 47).
/// <para>
/// <b>A capability with no tools, and that is the design rather than an omission.</b> Phase 47
/// settled it before the code: generation is a panel action, abandoning and removing are the
/// Commander's acts reachable from the panel and nothing else, and <em>nothing about an adventure
/// is callable by the model</em> — so a hostile in-game message cannot propose a story, end one,
/// or delete one. The standing context is readable by the model, which is the point: it plays off
/// the story it can see and cannot touch it.
/// </para>
/// <para>
/// It is registered all the same, because a registered capability is how d47 answers "what can you
/// do" honestly and how a page becomes mandatory. A whole tab the Commander can see, absent from
/// the one list that is supposed to be complete, is the invented-capability problem running
/// backwards: not a claim about something that does not exist, but a silence about something that
/// does.
/// </para>
/// <para>
/// Registered between Colonisation and System names, which puts it at the end of the run of
/// ledgers in the nav. That mirrors the Commander's own ruling recorded on
/// <see cref="D47.Core.Interface.PanelTab.Adventures"/>: the tab was moved to the end of them
/// because a story is read after a trip rather than during one.
/// </para>
/// </summary>
public static class AdventureCapability
{
    public const string Id = "adventures";

    public static CapabilityDescriptor Create() => new()
    {
        Id = Id,
        Group = "Knowledge",
        Name = "Adventures",
        Summary =
            "Stories the Commander flies, written by them or by the ship's AI, and advanced by "
            + "their own journal. Driven from the Adventures tab; nothing here is callable by the "
            + "model.",

        // The phrases that genuinely work. Reaching the tab is spoken navigation, which
        // PanelPhrases answers without a model; everything inside it is a press or a said value
        // on the panel's own say-or-type route. Listing "write me an adventure" here would be
        // listing something that does not work, which is the one thing the help projection
        // exists to prevent.
        Examples =
        [
            "show me the adventures",
            "open the adventures tab",
        ],

        // None. Keywords route to a tool with no required parameters, and this capability
        // deliberately has no tools at all.
        Keywords = [],
        Display = new CapabilityDisplay { PanelTitle = "Adventures", Order = 59, ShowOnPanel = false },

        // Empty, deliberately. See the remark above: generation, abandoning and removing are all
        // the Commander's acts, and none of them costs a byte of the advertised tool surface.
        Tools = [],
    };
}
