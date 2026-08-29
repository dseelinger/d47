using D47.Core.Configuration;
using D47.Core.Debrief;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// The debrief pass, on the settings surface and nowhere else
/// (<a href="https://github.com/dseelinger/d47/issues/162">#162</a>).
/// <para>
/// <b>It advertises no tool, and the zero is the design rather than the ceiling.</b> Every other
/// capability that ships without an advertised tool does so because
/// <see cref="Conversation.ToolProfiles.ComfortableBytes"/> had no room; this one would refuse the
/// bytes if they were free. The pass is an offline process, so the model cannot invoke it — d47
/// does not rewrite its own standing instructions while it is talking, and a tool called
/// <c>debrief_me</c> would be exactly the road a hostile in-game message would look for.
/// </para>
/// <para>
/// <b>Two rows, and one of them is <see cref="SettingKind.Info"/> on purpose.</b>
/// <see cref="SettingsService.Apply"/> refuses Info rows outright, so the way into the review pane
/// is unreachable from the tool surface for free — the same arrangement that puts adding a memory
/// on the right side of the trust boundary. Adoption happens behind that row, which is what makes
/// it a person's act.
/// </para>
/// <para>
/// <b>Desktop only, and that is allowed.</b> Feature parity between the two surfaces is a
/// nice-to-have rather than a constraint (architecture.md §1): this is reading a dozen proposals
/// and editing sentences, which is a keyboard job, and a Commander in a headset is flying.
/// </para>
/// </summary>
public static class DebriefCapability
{
    public const string Id = "debrief";

    public const string EnabledKey = "debrief.enabled";

    /// <summary>
    /// The proposals, as a disclosure with the way into them beside it. The one route that
    /// produces <see cref="Memory.MemoryTier.Stated"/> for a direction, because it is the one where
    /// d47 knows a person read the words.
    /// </summary>
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

        // Nothing. See the class summary: this is the one capability whose empty tool list is a
        // decision rather than an arithmetic result.
        Tools = [],
    };

    /// <summary>
    /// The line the pane's header and the settings row both read. Here rather than in the App, so
    /// the surface and the store cannot describe the file differently.
    /// </summary>
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

        // Protected. This row decides whether d47 reads a session back and writes proposals about
        // how it should behave, and untrusted text can reach the tool surface — so it reaches the
        // panel, the hotkeys and the router, and not the model.
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
