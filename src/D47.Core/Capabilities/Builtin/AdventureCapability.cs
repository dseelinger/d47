namespace D47.Core.Capabilities.Builtin;

/// <summary>Adventures — stories the Commander flies, told by the ship's AI (Phase 47).</summary>
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

        // The phrases that genuinely work.
        Examples =
        [
            "show me the adventures",
            "open the adventures tab",
        ],

        // None.
        Keywords = [],
        Display = new CapabilityDisplay { PanelTitle = "Adventures", Order = 59, ShowOnPanel = false },

        // Empty, deliberately.
        Tools = [],
    };
}
