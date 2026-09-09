using D47.Core.Configuration;
using D47.Core.Debrief;

namespace D47.Core.Capabilities.Builtin;

/// <summary>The debrief pass, on the settings surface and nowhere else (#162).</summary>
public static class DebriefCapability
{
    public const string Id = "debrief";

    public const string EnabledKey = "debrief.enabled";

    /// <summary>The proposals, as a disclosure with the way into them beside it.</summary>
    public const string DirectionsKey = "debrief.directions";

    public static CapabilityDescriptor Create(DebriefBook? book) => new()
    {
        Id = Id,
        Group = "Conversation",
        Name = "Debrief",
        Summary =
            "After a session, draft standing directions from what the Commander corrected — and change "
            + "nothing until they take one.",
        Examples =
        [
            "shorter answers in combat",
            "stop calling it that",
            "from now on, give me the distance first",
        ],

        // Beside Memory and Persona, which are the other two cards about who is talking to whom.
        Display = new CapabilityDisplay { PanelTitle = "Debrief", Order = 14 },
        Settings = [EnabledRow(), DirectionsRow(book)],

        // Nothing.
        Tools = [],
    };

    /// <summary>The line the pane's header and the settings row both read.</summary>
    public static string Summarise(DebriefBook? book) =>
        book?.Summarise() ?? "Nothing is debriefing in this configuration.";

    private static SettingRow EnabledRow() => new()
    {
        Key = EnabledKey,
        Label = "Debrief me after a session",
        Help =
            "When you close D47, it reads back what you corrected it on and drafts directions from your "
            + "own words. Nothing changes until you take one. Off stops the drafting; it does not "
            + "withdraw anything you have already taken.",
        Kind = SettingKind.Toggle,
        DefaultDisplay = "on",
        DocsAnchor = "what-the-pass-reads",

        // Protected.
        Protected = true,
        Commands =
        [
            new SettingCommandPhrase("stop debriefing me", "false"),
            new SettingCommandPhrase("start debriefing me", "true"),
        ],
        Binding = new SettingBinding
        {
            Read = s => s.Debrief.Enabled ? "true" : "false",
            Write = (s, v) => s with { Debrief = s.Debrief with { Enabled = v is not "false" } },
        },
    };

    private static SettingRow DirectionsRow(DebriefBook? book) => new()
    {
        Key = DirectionsKey,
        Label = "Standing directions",
        Help =
            "What D47 has drafted, and what you have taken. Anything you take goes into the prompt at "
            + "the start of your next session — never in the middle of this one.",
        Kind = SettingKind.Info,
        DocsAnchor = "taking-one",
        Binding = new SettingBinding { Read = _ => Summarise(book) },
    };
}
