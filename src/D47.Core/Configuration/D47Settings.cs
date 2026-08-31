using Microsoft.Extensions.Logging;

namespace D47.Core.Configuration;

/// <summary>
/// The settings store's whole shape. Anything not declared here is an unknown key and is
/// rejected on load (Phase 1).
/// </summary>
public sealed record D47Settings
{
    /// <summary>
    /// Settings as they are before anyone chooses anything. A row reads this to answer "does
    /// this setting have an unset state at all" — which decides whether the panel offers a way
    /// to clear it. Asking the binding beats a second flag that could disagree with it.
    /// </summary>
    public static readonly D47Settings Defaults = new();

    public int SchemaVersion { get; init; } = 1;

    public LoggingSettings Logging { get; init; } = new();

    public LlmSettings Llm { get; init; } = new();

    public SpeechSettings Speech { get; init; } = new();

    /// <summary>
    /// Per-category level, mute and ducking (Phase 12, "#96 Ambient audio mixer").
    /// <para>
    /// The arbiter's own record rather than a settings-shaped copy of it. Two records that have
    /// to agree about five categories and three numbers each is two records that eventually
    /// disagree, and the thing they would disagree about is how loud d47 is.
    /// </para>
    /// </summary>
    public Audio.AudioMix Audio { get; init; } = new();

    public UiSettings Ui { get; init; } = new();

    public HotkeySettings Hotkeys { get; init; } = new();

    public UpdateSettings Updates { get; init; } = new();

    /// <summary>
    /// Where a donated excerpt or journal history is sent, when one is
    /// (<a href="https://github.com/dseelinger/d47/issues/175">#175</a>).
    /// </summary>
    public DonationSettings Donation { get; init; } = new();

    public CalloutSettings Callouts { get; init; } = new();

    public ListeningSettings Listening { get; init; } = new();

    public VrSettings Vr { get; init; } = new();

    public ActionSettings Actions { get; init; } = new();

    public PersonaSettings Persona { get; init; } = new();

    public KnowledgeSettings Knowledge { get; init; } = new();

    /// <summary>
    /// What d47 keeps about the Commander, and for how long (Phase 31).
    /// </summary>
    public MemorySettings Memory { get; init; } = new();

    /// <summary>
    /// Whether d47 debriefs itself after a session, and what it does with what it finds
    /// (<a href="https://github.com/dseelinger/d47/issues/162">#162</a>).
    /// </summary>
    public DebriefSettings Debrief { get; init; } = new();

    /// <summary>
    /// How a Commander's log is written when one is asked for (Phase 33).
    /// </summary>
    public LogbookSettings Logbook { get; init; } = new();

    /// <summary>
    /// What each Commander has set of the rows that are theirs rather than the installation's
    /// (Phase 44). One entry per Frontier id, <b>keyed inside the document</b>: the id
    /// comes out of the journal, and turning untrusted input into a filename buys a
    /// path-traversal surface for an organisational convenience — the rule every per-Commander
    /// store already follows.
    /// <para>
    /// Everything else in this record stays the installation's, and the fields above that a
    /// Commander may override keep their keys and their place: the file is append-only, and a
    /// value written before anyone was identified stays where it was written, as the installation's
    /// default. See <see cref="CommanderScope"/> for how the two layers are read and written.
    /// </para>
    /// </summary>
    public IReadOnlyList<CommanderSettings> Commanders { get; init; } = [];
}

/// <summary>
/// One Commander's overlay over the installation's settings (Phase 44).
/// <para>
/// <b>Null is unset and empty is deliberately blank</b>, and the two are kept apart on purpose.
/// A Commander who has never touched About Me reads the installation's; one who cleared it reads
/// nothing, and must not fall back to somebody else's story because the box happened to be
/// empty. Only the fields here are per Commander — the list is the declaration, and
/// <c>CommanderScopeTests</c> holds it against the rows that say they are.
/// </para>
/// </summary>
public sealed record CommanderSettings
{
    public required string CommanderFid { get; init; }

    /// <summary>
    /// Who that is, for a person reading a file two Commanders share. Written beside the id and
    /// never read back — the same standing as the hull beside a ship-core binding.
    /// </summary>
    public string? CommanderName { get; init; }

    /// <summary>This Commander's <see cref="LlmSettings.AboutMe"/>. Empty means none, on purpose.</summary>
    public string? AboutMe { get; init; }

    /// <summary>This Commander's <see cref="LlmSettings.CharacterSheet"/>. Empty means none, on purpose.</summary>
    public string? CharacterSheet { get; init; }

    /// <summary>
    /// This Commander's <see cref="PersonaSettings.ShipCoreShip"/> — a ship id, which only means
    /// something for the Commander whose fleet it counts. Zero means none chosen, as it does there.
    /// </summary>
    public int? ShipCoreShip { get; init; }
}

/// <summary>
/// The Commander's log (Phase 33). Three choices and no switch, because there is nothing
/// to switch off: nothing here happens until somebody asks for it, which is item 4's requirement
/// and is enforced by there being no caller but a button and a phrase.
/// <para>
/// Its own block rather than fields on <see cref="LlmSettings"/>. The voice is not a property of
/// the endpoint — it is a property of whose log it is — and a Commander who changes provider has
/// not thereby changed their mind about writing in the first person.
/// </para>
/// </summary>
public sealed record LogbookSettings
{
    /// <summary>
    /// Whose voice writes it. See <see cref="Logbook.LogVoices"/> for the three and for the rule
    /// that governs the two needing a personality.
    /// <para>
    /// <b>First person by default</b>, which item 3 calls the plain one. The ship's-AI log is the
    /// thing only d47 can do, and a thing only d47 can do is a thing to opt into rather than a
    /// thing to have happen to you the first time you press a button.
    /// </para>
    /// </summary>
    public string Voice { get; init; } = "first-person";

    /// <summary>
    /// What span a log covers when nobody named one — a session, today, a week, a month. Two
    /// explicit dates are reachable from the panel and are never a default.
    /// </summary>
    public string Range { get; init; } = "session";

    /// <summary>
    /// How long it runs to. It sets the output budget, which is most of what the estimate is
    /// pricing, so it is a setting rather than a constant.
    /// </summary>
    public string Length { get; init; } = "standard";
}

/// <summary>
/// The memory store's two settings (Phase 31, "It forgets, and can be read and emptied").
/// <para>
/// Its own block rather than a pair of flags on <see cref="LlmSettings"/>, because the store is not
/// the model's: the panel writes to it, the journal observer writes to it, and it is still there and
/// still readable with no provider selected at all. A Commander running local-only has a memory.
/// </para>
/// </summary>
public sealed record MemorySettings
{
    /// <summary>
    /// Whether d47 remembers anything at all. Off stops every write and stops recall reaching the
    /// prompt; it does <b>not</b> erase what is already there, because a switch that emptied a file
    /// would be a delete button wearing a toggle's clothes. Emptying is its own action, in the
    /// privacy section, and it says what it is.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// How many days an entry lives before it is forgotten. 0 is "never", which is a real choice
    /// rather than an absence.
    /// <para>
    /// <b>Ninety days by default.</b> A store that only grows is a liability with a countdown on it,
    /// and defaulting to "never" would ship that liability to everybody who never opens this row.
    /// What makes ninety days tolerable is the other half of the item: an expiry that removes
    /// something the Commander <em>stated</em> is said out loud rather than happening quietly, so
    /// the default cannot silently lose the one tier a person authored.
    /// </para>
    /// </summary>
    public int ExpiryDays { get; init; } = 90;
}

/// <summary>
/// The debrief pass (<a href="https://github.com/dseelinger/d47/issues/162">#162</a>).
/// <para>
/// <b>One switch, and there is deliberately no second one.</b> A row for "adopt what it drafts
/// automatically" is the one setting this feature must not have: the merge gate is the whole
/// design, and a toggle that removed it would put a loop that rewrites d47's own instructions one
/// click away from every installation.
/// </para>
/// </summary>
public sealed record DebriefSettings
{
    /// <summary>
    /// Whether the pass runs at the end of a session at all. On by default, which is safe because
    /// what it produces is a list nobody has agreed to: with this on and the pane never opened,
    /// the prompt is byte-for-byte what it was.
    /// </summary>
    public bool Enabled { get; init; } = true;
}

/// <summary>
/// Looking things up outside this machine (Phase 14).
/// <para>
/// Its own block rather than a flag on <see cref="LlmSettings"/>, because these destinations are
/// not the model's. A Commander can run a local model and still want the galaxy search, or run a
/// cloud model and want nothing else leaving — and a setting that bundled them could express
/// neither.
/// </para>
/// </summary>
public sealed record KnowledgeSettings
{
    /// <summary>
    /// Whether d47 may reach the galaxy search. Off by default, which is the deliberate choice:
    /// this is the first capability whose answers come from a third party, and a fresh install
    /// should not start talking to one before the Commander has seen it in the disclosure and
    /// said yes. Off is a capability that is off, not an error — the tool says so and the turn
    /// carries on (Phase 3).
    /// </summary>
    public bool GalaxySearch { get; init; }

    /// <summary>
    /// Whether a generated adventure may fetch the catalogue of notable places from edastro.com
    /// (Phase 47). Off by default, like every third-party destination.
    /// </summary>
    public bool NotablePlaces { get; init; }
}

/// <summary>
/// Which companion character is aboard (Phase 11).
/// <para>
/// Its own block rather than a field on <see cref="LlmSettings"/>, because the persona is not
/// a property of the model. It picks the prompt block, it picks the voice, and it picks the
/// transcript — and two of those three still mean something with the provider set to "none".
/// </para>
/// </summary>
public sealed record PersonaSettings
{
    /// <summary>
    /// A core id from <see cref="D47.Core.Persona.PersonaCatalog"/>. An id d47 no longer ships
    /// resolves to the default rather than failing the load: this is a stale value in a known
    /// key, not the unknown-key case the loader exists to catch.
    /// </summary>
    public string Id { get; init; } = D47.Core.Persona.PersonaCatalog.DefaultId;

    /// <summary>
    /// What the Commander calls the ship's AI. Null means the persona's own name, which is why
    /// this is nullable rather than defaulted to a string: a name stored here would stop
    /// following the persona the moment they switched core, and "defaults to the persona's
    /// name" is the whole of the requirement.
    /// </summary>
    public string? ShipName { get; init; }

    /// <summary>
    /// Whether a name the Commander gave the ship's AI survives a change of core.
    /// <para>
    /// On, because a Commander who named their ship's AI named the <em>ship's</em> AI: the name
    /// is a property of their ship in their fiction, and eleven cores answering to it is the
    /// point. Off is the other reading, equally coherent — the cores are separate characters and
    /// a name belongs to the one it was given to — and it clears the name on the switch rather
    /// than keeping it and ignoring it, because a row showing "Fred" while the answer is "I am
    /// Cora" is the shape of bug this codebase has already fixed once.
    /// </para>
    /// </summary>
    public bool KeepShipName { get; init; } = true;

    /// <summary>
    /// Whether the cores are allowed an occasional light touch of wit
    /// (<a href="https://github.com/dseelinger/d47/issues/243">#243</a> — "it's so serious all
    /// the time").
    /// <para>
    /// Off by default, because off is exactly the register that shipped. On adds one line to the
    /// persona layer of the prompt — permission for humor inside each core's own character, never
    /// a personality transplant — and reaches everything the core says in character:
    /// conversation and the flavour briefs alike.
    /// </para>
    /// </summary>
    public bool Humor { get; init; }

    /// <summary>
    /// The voice paired to each core, keyed by persona id (Phase 11, #33). Written by
    /// the background pairing at first startup and by the Commander choosing one by hand;
    /// nothing distinguishes the two, on purpose, because a pairing the Commander has
    /// overridden should never be quietly re-derived.
    /// </summary>
    public IReadOnlyDictionary<string, string> Voices { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// Which ship the core-binding rows are pointed at, by its <c>ShipID</c>.
    /// <para>
    /// <b>A selector rather than a preference</b> (remediation.md 15, item 13). Binding a core used
    /// to be scoped to the ship being flown, so setting one meant boarding that ship in game first;
    /// two dropdowns replace that, and the first needs somewhere to keep what it points at.
    /// </para>
    /// <para>
    /// <b>It lives here because <see cref="D47.Core.Capabilities.SettingBinding"/> reads and writes
    /// through settings and nothing else.</b> The alternative was widening that contract for one
    /// row, which is architecture.md §5 D5 territory and touches every capability. The cost is one
    /// property that can never be removed, and "which ship am I editing" surviving a restart is a
    /// convenience rather than a wart.
    /// </para>
    /// <para>
    /// Zero means none chosen. A ship the Commander no longer owns reads as none rather than
    /// failing the load, the same way a retired persona id does.
    /// </para>
    /// </summary>
    public int ShipCoreShip { get; init; }

    /// <summary>
    /// Whether the background voice pairing has run. A flag rather than "is
    /// <see cref="Voices"/> empty", because a Commander who cleared every pairing by hand
    /// should not have them silently regenerated on the next launch.
    /// </summary>
    public bool VoicesPaired { get; init; }

    /// <summary>
    /// Whether the pairings have been checked against the gender each core is written with.
    /// <para>
    /// Once, and then never again. The pairing pass ran for a while without being told which
    /// cores are men, and a file written in that time can hold a core speaking in the wrong
    /// voice — but a file is also where a Commander's own choice lives, and re-deciding that on
    /// every launch is the one thing <see cref="Voices"/> promises not to do. So the repair is a
    /// flag: it runs on a file that has not had it, and a choice made afterwards stands forever.
    /// </para>
    /// </summary>
    public bool VoicesGenderChecked { get; init; }

    /// <summary>
    /// Superseded by <see cref="VoicesRepaired"/>, and kept because unknown keys are rejected on
    /// load: every file written between v0.6.2 and v0.6.4 carries this, and removing the
    /// property would refuse those files rather than ignore the value. Nothing reads it.
    /// </summary>
    public bool VoicesNamedChecked { get; init; }

    /// <summary>
    /// Which revision of the named-default repair this file has had.
    /// <para>
    /// A number rather than a flag, because the flag could not say the one thing that turned out
    /// to matter: the repair itself shipped wrong. v0.6.2 stamped "done" on files it had only
    /// half-repaired — it moved the named voice onto the core it belongs to but left it on
    /// whoever else was holding it — and v0.6.3 corrected the repair to a file that would never
    /// run it again. A revision lets a corrected repair reach the files the broken one stamped,
    /// and costs nothing on a file that is already right.
    /// </para>
    /// <para>
    /// Bumped only when a repair is corrected or added, never as routine: each bump re-decides
    /// something the Commander may have decided differently, which is the cost that makes this a
    /// number to raise deliberately rather than a version to keep in step with the app's.
    /// </para>
    /// </summary>
    public int VoicesRepaired { get; init; }
}

/// <summary>
/// Acting on the game (Phase 10).
/// <para>
/// Two switches with deliberately different shapes. <see cref="Keyboard"/> is one decision
/// covering every spoken command, because a Commander who wants voice control of their ship
/// wants it for the ship rather than per key. The autonomous actions are the opposite: each is
/// off on its own row, because an action that fires a game input on a journal event with
/// nobody asking is a different category and the checklist gives the category its rule before
/// the second member of it exists.
/// </para>
/// </summary>
public sealed record ActionSettings
{
    /// <summary>
    /// Whether spoken commands may send key bindings to Elite at all. Off until the Commander
    /// says otherwise, and protected from the model.
    /// </summary>
    public bool Keyboard { get; init; }

    /// <summary>
    /// Whether the discovery scanner fires by itself on arriving in a system. Off by default,
    /// on its own row, and protected — the first member of the autonomous-action category.
    /// </summary>
    public bool HonkOnArrival { get; init; }

    /// <summary>
    /// Whether d47 may drive the galaxy map to plot a course. Separate from
    /// <see cref="Keyboard"/> because it is best-effort by nature — it depends on the map's
    /// focus and layout — and a Commander may reasonably want spoken flight controls without
    /// wanting something opening their map and typing into it.
    /// </summary>
    public bool AutoPlot { get; init; }

    /// <summary>
    /// Whether d47 may type into Elite's chat. Its own row because it is the only thing here
    /// other people can see, and because it cannot be taken back.
    /// </summary>
    public bool Chat { get; init; }

    /// <summary>
    /// Whether a mapped HOTAS switch may operate the ship (Phase 21). Off by default,
    /// on its own row, and protected — a hostile in-game message must not be able to give a
    /// switch the keyboard.
    /// <para>
    /// Separate from <see cref="Keyboard"/> and gated by it rather than replacing it. A
    /// Commander who has not allowed key injection at all has not allowed it for switches
    /// either, and the switch row saying otherwise would be a second answer to a question that
    /// already has one.
    /// </para>
    /// </summary>
    public bool Switches { get; init; }

    /// <summary>
    /// Whether "take us out" may walk the left panel to the launch button (Phase 52).
    /// <para>
    /// Its own row, and the one here with the weakest guarantee: Elite has no launch binding, so
    /// this is a menu walk rather than a key press and it depends on the panel starting where d47
    /// expects. Gated by <see cref="Keyboard"/> as well, which is off until the Commander says
    /// otherwise, so on by default here does not mean on out of the box.
    /// </para>
    /// </summary>
    public bool TakeUsOut { get; init; } = true;

    /// <summary>
    /// Whether "separate and engage" may go to full throttle and boost out of a mass lock
    /// (Phase 52). Gated by <see cref="Keyboard"/>.
    /// </summary>
    public bool SeparateAndEngage { get; init; } = true;

    /// <summary>
    /// The same, ending in supercruise. Its own row because the two fail differently in the game:
    /// a jump needs a destination locked in the nav panel and refuses without one, where
    /// supercruise needs nothing.
    /// </summary>
    public bool SeparateAndSupercruise { get; init; } = true;
}

/// <summary>
/// The headset (Phase 9).
/// <para>
/// There is no "is a headset present" setting and there never will be: that is a state d47
/// discovers and reports, not a thing the Commander configures. This is only whether they want
/// the overlays at all, plus how the surfaces are placed once there is somewhere to place them.
/// </para>
/// </summary>
public sealed record VrSettings
{
    /// <summary>
    /// On by default, which costs nothing on a machine with no headset: the runtime is looked
    /// for, not found, and the state machine reports Unavailable. Off is for the Commander who
    /// has SteamVR installed for something else and does not want d47 in it.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Which content set the panel is showing: "full" or "mini". A value rather than a second
    /// surface, because the checklist is explicit that mini is a mode of the same panel.
    /// <para>
    /// Mini out of the box (docs/plans/change-requests.md item 9). The full panel is a 1.1 m quad
    /// — near fifty degrees of view — and a Commander meets it before they have any idea it can
    /// be moved or shrunk. Mini is the mode that suits a headset beside a running game, and the
    /// full panel is one switch away.
    /// </para>
    /// <para>
    /// Only for a fresh install. Every property is written to <c>settings.json</c>, so a
    /// Commander who already has one keeps whatever it says — a default is what d47 starts from,
    /// not something it imposes later on a layout somebody has already arranged.
    /// </para>
    /// </summary>
    public string Mode { get; init; } = "mini";

    /// <summary>
    /// Whether d47 touches the motion controllers at all — the action manifest, the trigger, the
    /// grip, and the ninety-times-a-second pose read behind the aim ray
    /// (<a href="https://github.com/dseelinger/d47/issues/198">#198</a>).
    /// <para>
    /// <b>Off, and that is a withdrawal rather than a preference.</b> Every put-down of a
    /// controller while d47 was connected to SteamVR and that then reached standby failed to come
    /// back on its own; every put-down while d47 was not connected came back by itself
    /// (<a href="https://github.com/dseelinger/d47/issues/18">#18</a>). d47 read controller poses
    /// on the order of 350,000 times in one 64-minute session in which no ray ever crossed the
    /// panel — the reading does not depend on pointing at anything — and a pose read running
    /// across SteamVR's standby transition is the undocumented interaction that correlation
    /// implicates. Turning it off is the only change that stops d47 touching the device, so it is
    /// also the test.
    /// </para>
    /// <para>
    /// <b>A switch rather than a deletion, on the Commander's own framing: it is not forever.</b>
    /// A session with this on and a session with it off are the experiment, and the day #18 is
    /// understood — or the day the withdrawal is shown not to have helped — turning it back on is
    /// one line rather than a rebuild from a git history.
    /// </para>
    /// <para>
    /// What it costs while off is known and accepted: nothing on the panel can be pressed in the
    /// headset, the headset Settings tab is unreachable, and there is no way to grab the panel
    /// and carry it. Voice keeps tab and breadcrumb navigation, back, scrolling, answering a
    /// prompt that is already open, re-anchoring, and — since #199 — placing a panel by nudging
    /// it.
    /// </para>
    /// </summary>
    public bool Controllers { get; init; }

    /// <summary>Where the full panel sits and what it looks like.</summary>
    public VrSurfaceSettings Panel { get; init; } = new();

    /// <summary>
    /// And the mini one, separately. Not a scaled copy of the row above: the two modes have
    /// different reasons to exist, so a Commander who parks mini out at the edge of vision and
    /// keeps the full panel in front of them is doing the expected thing rather than fighting
    /// a shared setting.
    /// </summary>
    public VrSurfaceSettings Mini { get; init; } = VrSurfaceSettings.Mini();

    /// <summary>
    /// How solid the panel is, whichever of the two is on screen. <b>One knob for two surfaces</b>
    /// (asked for 2026-08-24).
    /// <para>
    /// It used to be one of the six settings each surface kept its own copy of, and that is what
    /// made <i>"set the opacity to 0.5"</i> a question with two answers: it went to the big panel's
    /// copy while the mini panel was the one in front of the Commander, and the number they could
    /// see never moved. Everything else on those surfaces is genuinely per-surface — mini exists to
    /// be smaller and further out of the way, so its distance, size and drop have to differ. How
    /// see-through the glass is does not: it is one preference about how much cockpit shows through
    /// d47, and a Commander who wants it at half never means <em>half, in one of the two modes</em>.
    /// </para>
    /// <para>
    /// The per-surface copies stay on <see cref="VrSurfaceSettings"/> because
    /// <c>settings.json</c> is append-only, and <see cref="OpacityShared"/> records that their
    /// value has been brought up here. Nothing reads them any more.
    /// </para>
    /// </summary>
    public double Opacity { get; init; } = 0.95;

    /// <summary>
    /// Which revision of the shared-opacity repair this file has had — the same counter idiom as
    /// <see cref="PitchRepaired"/>, and for the same reason: a repair that ships wrong can only
    /// reach the files it already stamped if the stamp can be raised.
    /// </summary>
    public int OpacityShared { get; init; }

    /// <summary>
    /// The caption layer. Its own block because captions are their own overlay, and because
    /// everything on it is something the caption standard leaves to the viewer - nothing here
    /// is a number the standard fixes.
    /// </summary>
    public Vr.CaptionSettings Captions { get; init; } = new();

    /// <summary>
    /// Which revision of the panel-pitch repair this file has had.
    /// <para>
    /// <see cref="VrSurfaceSettings.Pitch"/> changed from the whole tilt angle to a trim on top of
    /// one derived from distance and drop, so every value already on disk means something else
    /// now. A number rather than a flag for the reason
    /// <see cref="PersonaSettings.VoicesRepaired"/> is one: a repair that ships wrong can only
    /// reach the files it already stamped if the stamp can be raised.
    /// </para>
    /// </summary>
    public int PitchRepaired { get; init; }
}

/// <summary>
/// The microphone and the key that opens it (Phase 6).
/// <para>
/// Push-to-talk is one gate policy over a continuous stream, so "toggle instead of hold" is a
/// value here rather than a second mechanism — and continuous listening and a wake word arrive
/// later as further values, not as a rewrite.
/// </para>
/// </summary>
public sealed record ListeningSettings
{
    /// <summary>
    /// The input device id, or null for the system default. An id rather than a name because a
    /// friendly name is not stable across driver updates.
    /// <para>
    /// The checklist is specific about why this row exists: a blank selection produces a silent
    /// default and a turn reporting no speech detected, with nothing indicating why.
    /// </para>
    /// </summary>
    public string? InputDevice { get; init; }

    /// <summary>
    /// Hold to talk. Null means d47 never listens, which stays a legitimate configuration —
    /// clearing the row is how a Commander asks for it.
    /// <para>
    /// Bound out of the box, to right shift. The opposite used to be the default, on the
    /// argument that a microphone opening on a key nobody chose is a microphone opening by
    /// surprise. What that reasoning missed is what the unbound state actually costs: a voice
    /// companion that cannot hear anything until the Commander finds a settings row, which is
    /// the whole product not working on first run. The surprise is bounded and the miss is not
    /// — nothing is captured unless the key is held, and nothing is transcribed at all until a
    /// speech model is installed, which is still a deliberate choice the Commander makes.
    /// </para>
    /// <para>
    /// Right shift because of where the Commander's hands are: on a stick and a throttle, with
    /// a spare thumb and not much else. It is polled as a sided code, so the left shift they
    /// are already using for something in the game is not this.
    /// </para>
    /// <para>
    /// Protected: a model that can rebind or unbind the Commander's microphone key has taken
    /// away how they talk to it.
    /// </para>
    /// </summary>
    public string? PushToTalkKey { get; init; } = "RightShift";

    /// <summary>
    /// A stick button to talk with, as <c>NonRoamableId#index</c> (Phase 53).
    /// <para>
    /// <b>A new key beside <see cref="PushToTalkKey"/> rather than that key learning a second
    /// meaning</b>, because <c>settings.json</c> is append-only and because a Commander who bound
    /// a key and later bound a button has said two things. Both stay live and either opens the
    /// microphone (the Commander's call, 2026-08-25) — neither answer should be inferred from the
    /// other having been set.
    /// </para>
    /// <para>
    /// Empty by default, and a machine with no stick is not a fault: the button is simply never
    /// seen and the key carries on.
    /// </para>
    /// </summary>
    public string? PushToTalkButton { get; init; }

    /// <summary>
    /// "hold", "toggle", "continuous" or "wake" — the gate policy (Phase 6 and 13).
    /// <para>
    /// Protected, and this is the row where that matters most. The last two open the microphone
    /// and keep it open; a model that can put d47 into one of them can start continuous capture
    /// on the Commander's machine, and anything the model can call, a hostile in-game message
    /// can attempt to invoke (architecture.md §7).
    /// </para>
    /// </summary>
    public string Mode { get; init; } = "hold";

    /// <summary>
    /// How much audio from before the key was noticed is kept, in milliseconds. Exists as a
    /// setting because it is the one number that trades memory against clipped first syllables,
    /// and the right value depends on the machine.
    /// </summary>
    public int PreRollMilliseconds { get; init; } = 500;

    /// <summary>
    /// Which Whisper model transcribes. The smallest English one on a fresh install; "none"
    /// stays a real choice, and is where the selection sits until the download is accepted.
    /// <para>
    /// This is a selection, not a download. Nothing is fetched at launch: a selected model that
    /// is not on disk clears itself back to "none" and becomes the offer on the panel, stating
    /// the size and the host, and the file arrives only if the Commander says yes. So the
    /// default is the question being asked rather than the answer being assumed — which is the
    /// difference between proposing 75 MB and taking it.
    /// </para>
    /// </summary>
    public string Model { get; init; } = Listening.WhisperModels.DefaultId;

    /// <summary>
    /// Run inference on the GPU. Off by default and deliberately so.
    /// <para>
    /// <b>It reaches a GPU as of #187, and did not before.</b> Only CPU natives shipped, the CPU
    /// runtime accepts a GPU request without complaint, and the log reported the request rather
    /// than the result — so the toggle changed one log line and nothing else. It is Vulkan now
    /// rather than CUDA, which runs on any vendor's card and bundles no vendor runtime.
    /// </para>
    /// <para>
    /// Off by default because the cost is real and lands somewhere else: in VR the GPU is
    /// already the scarce resource, so a model running there surfaces as dropped frames rather
    /// than as anything resembling a speech problem — which the checklist calls the hardest kind
    /// of setting to diagnose. A short push-to-talk clip on the small English models absorbs CPU
    /// inference fine. Measured on an RTX 5080 with <c>small.en</c>: 189 ms against 924 ms, for
    /// 469 MB of video memory — close to the model file's own size, since the weights are almost
    /// all of it.
    /// </para>
    /// </summary>
    public bool UseGpu { get; init; }

    /// <summary>
    /// Subtract what d47 is playing from what the microphone hears (Phase 13).
    /// <para>
    /// On, and on in every mode rather than only the hands-free ones. It is what makes talking
    /// over d47 work at all on speakers, and push-to-talk benefits from that as much as
    /// continuous listening does — a Commander who holds the key while a callout is being read
    /// out is otherwise transcribing the callout.
    /// </para>
    /// </summary>
    public bool EchoCancellation { get; init; } = true;

    /// <summary>
    /// Take the room out of the captured audio, which the same module does for free. It also
    /// makes the voice-activity decision easier, since that decision is entirely about how far a
    /// frame sits above the room.
    /// </summary>
    public bool NoiseSuppression { get; init; } = true;

    /// <summary>
    /// How far above the room a sound has to be before continuous listening calls it speech, in
    /// decibels. The one number that trades opening on a cough against missing a quiet question.
    /// </summary>
    public int Sensitivity { get; init; } = 9;

    /// <summary>
    /// How long the quiet after a sentence has to run before the utterance is finished, in
    /// milliseconds. Long enough that a Commander pausing mid-sentence is not cut in half.
    /// </summary>
    public int SilenceMilliseconds { get; init; } = 700;

    /// <summary>
    /// What d47 answers to in wake-word mode, comma-separated. Empty means the ship's AI name,
    /// whatever the Commander currently has it set to — which is why this is empty rather than
    /// defaulted to a string: a name stored here would stop following the one they chose, and
    /// a wake word that is not what you call your ship's AI is a wake word nobody will say.
    /// </summary>
    public string? WakeWords { get; init; }

    /// <summary>
    /// How long d47 goes on listening after answering to its name with nothing after it, in
    /// seconds. Zero means it does not — the name and the request have to arrive together.
    /// </summary>
    public int WakeWindowSeconds { get; init; } = 12;
}

/// <summary>
/// What d47 says without being asked (Phase 8).
/// <para>
/// One toggle per callout rather than one for all of them: a Commander who finds route progress
/// chatty should not have to switch off danger warnings to stop it. The master switch exists
/// for the case where somebody wants d47 to speak only when spoken to.
/// </para>
/// </summary>
public sealed record CalloutSettings
{
    /// <summary>Off means d47 never speaks unprompted. Everything else keeps running.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Interdiction, shields, hull, heat, a full hold.</summary>
    public bool Danger { get; init; } = true;

    /// <summary>Low fuel, and the unscoopable-next-star case that strands a Commander.</summary>
    public bool Fuel { get; init; } = true;

    public bool Route { get; init; } = true;

    public bool LongJump { get; init; } = true;

    /// <summary>Home, carrier, stored ships, engineering.</summary>
    public bool Arrival { get; init; } = true;

    public bool Materials { get; init; } = true;

    /// <summary>
    /// Systems that might be running High Grade Emissions, and what would be in them
    /// (Phase 40).
    /// <para>
    /// <b>On.</b> It speaks once per system, only above a million population, only about materials
    /// there is still room for — so a Commander who has finished gathering stops hearing it without
    /// touching this, which is the behaviour that earns a default of on.
    /// </para>
    /// </summary>
    public bool Emissions { get; init; } = true;

    /// <summary>
    /// A reminder to buy limpets on docking somewhere that sells them (Phase 41).
    /// <para>
    /// <b>Off, unlike most callouts.</b> It is for Commanders who fly limpets, and one who never
    /// does should not have to switch it off to stop hearing about it. Phase 32's habit callout is
    /// the precedent for defaulting off with a reason.
    /// </para>
    /// </summary>
    public bool Limpets { get; init; }

    /// <summary>
    /// The smallest cargo capacity worth reminding about, in tonnes. Below it the Commander is not
    /// running a limpet operation and the reminder is noise.
    /// </summary>
    public int LimpetCargoFloor { get; init; } = 64;

    /// <summary>
    /// The limpet threshold, as a percentage <b>of cargo capacity</b> (the Commander's ruling,
    /// 2026-08-21). The denominator is stated on the row itself, because a percentage whose
    /// denominator is not written down is a number nobody can set confidently.
    /// </summary>
    public int LimpetPercent { get; init; } = 5;

    /// <summary>
    /// An attack an NPC has announced but not yet made (Phase 15). On, because it is the
    /// only warning here that arrives while there is still something to do about it.
    /// </summary>
    public bool AnnouncedAttack { get; init; } = true;

    /// <summary>Flying in a rival Power's space (Phase 15).</summary>
    public bool RivalTerritory { get; init; } = true;

    /// <summary>
    /// A checklist item the journal has just changed its mind about, and the last unit a plan
    /// needed (Phase 17). On, because a computed tick going backwards is something the
    /// Commander wants to know rather than a glitch to hide.
    /// </summary>
    public bool Checklist { get; init; } = true;

    /// <summary>
    /// What a prospector limpet found, spoken in the ring (Phase 18). On, because a
    /// Commander mining is looking at the rock rather than at a panel — but separable from the core
    /// alert below, since this one arrives every 48 seconds at the median and that one is rare.
    /// </summary>
    public bool Prospector { get; init; } = true;

    /// <summary>
    /// A core asteroid (Phase 18). Its own row rather than sharing the prospector's,
    /// because it is 3 in 1,633 prospects and it is the announcement somebody turning the running
    /// commentary off still wants.
    /// </summary>
    public bool CoreAsteroid { get; init; } = true;

    /// <summary>
    /// Organic sampling progress on the surface (Phase 18). On, because the distance is the
    /// number nobody can eyeball and the reason a sample gets wasted.
    /// </summary>
    public bool Sampling { get; init; } = true;

    /// <summary>How often route progress is reported, in jumps. 0 silences the progress line.</summary>
    public int RouteEveryNJumps { get; init; } = 3;

    /// <summary>
    /// How long a hyperspace jump has to run before it is worth remarking on, measured from
    /// entering hyperspace rather than from the jump being initiated.
    /// </summary>
    public double LongJumpSeconds { get; init; } = 30;

    /// <summary>
    /// The Commander's home system. Null means no home callout — there is no sensible default,
    /// since where someone considers home is not something any journal event reports.
    /// </summary>
    public string? HomeSystem { get; init; }

    /// <summary>
    /// In-character remarks about where the Commander is, said because nothing has happened
    /// rather than because something has (Phase 11, "Ambient Voice").
    /// <para>
    /// On, because a companion that only ever answers questions is a search box with a voice.
    /// The interval is what makes that tolerable.
    /// </para>
    /// </summary>
    public bool Ambient { get; init; } = true;

    /// <summary>
    /// The shortest gap between two ambient remarks, in seconds. 0 silences them, which is the
    /// same as turning <see cref="Ambient"/> off and is offered because a Commander reaching for
    /// "less" will reach for this row rather than the switch.
    /// <para>
    /// Seconds rather than minutes because minutes could not express the interesting end of the
    /// range: the difference between a companion that speaks up now and then and one that never
    /// shuts up is a minute and a half, and a row whose smallest step is a minute cannot be set
    /// to it.
    /// </para>
    /// </summary>
    public int AmbientSeconds { get; init; } = 45;

    /// <summary>
    /// Invented background chatter (#244): made-up exchanges between people who do not exist —
    /// passers-by, the dock, the occasional one-way hail. Not the game's own NPC traffic, which
    /// is <see cref="SpeechSettings.SpeakNpcMessages"/> and somebody else's words.
    /// </summary>
    public bool NpcChatter { get; init; } = true;

    /// <summary>
    /// The shortest gap between two exchanges, in seconds. 0 silences them. Longer than the
    /// ambient default because an exchange is a scene rather than a sentence, and scenes wear
    /// out faster.
    /// </summary>
    public int NpcChatterSeconds { get; init; } = 1200;

    /// <summary>
    /// What this row held when it was in minutes. Kept because unknown keys are rejected on
    /// load — every file written before the change carries it — and read exactly once, by
    /// <see cref="SettingsStore"/>, which converts it and clears it.
    /// </summary>
    public int? AmbientMinutes { get; init; }

    /// <summary>
    /// What to say on arriving in a system d47 knows something about (Phase 23).
    /// <para>
    /// Three states rather than a switch and a switch, because a lookup with the remark off is
    /// detail about something that was never announced.
    /// </para>
    /// <para>
    /// <b>The lookup half is subordinate to <see cref="LlmSettings.WebSearch"/>, which is off
    /// until a Commander turns it on.</b> That is what makes this default to the fullest setting
    /// without defaulting anybody into unprompted spending: the consent for searching was already
    /// asked for once, on its own row, and this does not ask for it again.
    /// </para>
    /// </summary>
    public Callouts.LoreRemarks Lore { get; init; } = Callouts.LoreRemarks.Lookup;

    /// <summary>
    /// One line at the start of a session, picking up where the Commander left off (Phase 31,
    /// "Picking up where you left off").
    /// <para>
    /// On, and it costs nothing to leave on: it is silent unless there is a store to read, and it
    /// is the item the other three exist to make possible.
    /// </para>
    /// </summary>
    public bool Continuity { get; init; } = true;

    /// <summary>
    /// <b>Retired with Habits itself in v0.83.0, and kept because unknown keys are rejected on
    /// load.</b> Every settings file written before that carries this key, and removing the
    /// property would <em>refuse</em> those files rather than ignore the value — the same reason
    /// <see cref="PersonaSettings.VoicesNamedChecked"/> is still here. Nothing reads it.
    /// <para>
    /// <b>The name is retired and is never reused</b>, on the ruling that took Phase 32 out: a
    /// later feature answering to <c>callouts.habits</c> would silently inherit whatever a
    /// Commander decided about a different one. Same treatment, and the same reason, as the
    /// phase number.
    /// </para>
    /// </summary>
    public bool Habits { get; init; }

    /// <summary>A beat of the Commander's adventure, said when it is reached (Phase 47).</summary>
    public bool Adventure { get; init; } = true;
}

public sealed record LlmSettings
{
    /// <summary>
    /// Which provider to use. "none" is a real, supported choice — every input path stays
    /// answerable through the model-free keyword router (Phase 3).
    /// </summary>
    public string Provider { get; init; } = "anthropic";

    /// <summary>Null uses the provider's own default rather than pinning a model here.</summary>
    public string? Model { get; init; }

    /// <summary>
    /// Null uses the provider's published endpoint. A value here points at something else
    /// speaking the same protocol — a gateway or a proxy — which is why changing it clears
    /// <see cref="Model"/>: model ids are a property of the endpoint's namespace, and a name
    /// carried across from another endpoint is a stale selection that fails at the first turn
    /// (Phase 4).
    /// </summary>
    public string? Endpoint { get; init; }

    /// <summary>
    /// False is "plain answers, no persona". The anti-invention guardrails are unaffected —
    /// they sit above the persona in the assembled prompt and there is no setter for them.
    /// </summary>
    public bool PersonalityEnabled { get; init; } = true;

    /// <summary>
    /// The Commander's story, in their own words, kept between sessions. Treated as true of the
    /// world the Commander and the ship's AI share (Phase 43).
    /// </summary>
    public string? AboutMe { get; init; }

    /// <summary>
    /// The Commander's character sheet — name, origin, age, accent: the few lines that are true
    /// of them in any sentence (Phase 43). A second field rather than the first paragraph
    /// of <see cref="AboutMe"/>, because the two want opposite handling off the turn path: the
    /// sheet goes with every line said in character and the story only sometimes, and a split
    /// inferred from paragraph breaks is one nobody can see in the settings page.
    /// </summary>
    public string? CharacterSheet { get; init; }

    /// <summary>
    /// Whether the model may search the web when it decides a question needs current
    /// information. Off by default, on the same reasoning as the galaxy search: this reaches a
    /// third party, and a fresh install should not start doing that before the Commander has
    /// seen it in the disclosure and said yes.
    /// <para>
    /// Here rather than on <see cref="KnowledgeSettings"/>, which is where the galaxy search
    /// lives, and the distinction is the one that record's own comment draws — those
    /// destinations are "not the model's". This one <em>is</em> the model's: the only host d47
    /// contacts is the endpoint already selected above, and the search happens on the far side
    /// of it. A Commander who has chosen no provider has nothing to turn on here, which is
    /// exactly the coupling <see cref="KnowledgeSettings"/> exists to avoid for spansh and
    /// exactly the coupling that is true here.
    /// </para>
    /// </summary>
    public bool WebSearch { get; init; }

    /// <summary>
    /// Which model answers the calls the Commander is not waiting on — ambient remarks, the
    /// opening brief, the gap reaction, the two lore lookups, and casting a voice (Phase 54).
    /// Null means they take <see cref="Model"/>, exactly as they always have.
    /// <para>
    /// <b>A call class, not a floor the router picks within.</b> The two dials do not behave
    /// alike: effort is a top-level request field outside the cached prefix, so routing it per
    /// turn is free, while caches are model-scoped and returning to the conversation model after
    /// a cheap detour costs roughly 23× what the detour saved. So there is no per-turn model
    /// routing and there is not going to be. What makes this one pay is that every call it
    /// reaches carries no conversation history and already declares a cold prefix — pointing
    /// them at a cheap model costs no cache at all.
    /// </para>
    /// <para>
    /// <b>Named for the call class rather than as <c>MinModel</c>.</b> <see cref="Model"/> can
    /// never be renamed to <c>MaxModel</c> — this file is append-only — and <em>min</em> implies
    /// a range something picks within, which is the reading above rejects.
    /// </para>
    /// <para>
    /// A model id belongs to its endpoint's namespace, so this is cleared by both the provider
    /// row and the endpoint row exactly as <see cref="Model"/> is. A stale one left behind is a
    /// request that fails where nothing is watching: <c>FlavourTurn</c> logs at Debug and returns
    /// null, and every ambient line then falls back to its authored text with nothing on screen.
    /// </para>
    /// </summary>
    public string? BackgroundModel { get; init; }

    /// <summary>
    /// The least effort any conversation turn may run at, or null for the router's own answer
    /// (Phase 54). The Commander saying a lookup still deserves more than the cheapest
    /// setting.
    /// <para>
    /// Conversation turns only. The background calls above are call-site decisions with stated
    /// reasons and are deliberately not clamped — a floor of High would turn every ambient
    /// remark into a reasoning call, which is the blow-up <see cref="BackgroundModel"/> exists
    /// to prevent.
    /// </para>
    /// </summary>
    public Conversation.ThinkingEffort? EffortFloor { get; init; }

    /// <summary>
    /// The most effort any conversation turn may run at, or null for the router's own answer
    /// (Phase 54). A cost dial, and a guard against the router's own false positives:
    /// <c>EffortRouter</c> matches substrings with no word boundaries, so "what do you think
    /// about" hits "think about" and routes to Max.
    /// </summary>
    public Conversation.ThinkingEffort? EffortCeiling { get; init; }
}

/// <summary>
/// Everything audible. One record rather than several because the arbiter is one component and
/// splitting its configuration across three would invite the settings to disagree with it.
/// </summary>
public sealed record SpeechSettings
{
    /// <summary>
    /// Which voice provider, or "none". "none" is a real, supported choice: d47 stays fully
    /// usable in text with cues still audible, which is what keeps local-only operation
    /// reachable rather than theoretical (Phase 4).
    /// </summary>
    public string Provider { get; init; } = "edge";

    /// <summary>
    /// Which provider speaks for each of the other five slots, keyed by
    /// <see cref="Audio.VoiceGroupInfo.Id"/> (Phase 57). <see cref="Provider"/> above is
    /// the sixth and is the one aboard the ship; it keeps its meaning exactly.
    /// <para>
    /// <b>Null means a file written before Phase 57</b>, and it loads as every slot following
    /// <see cref="Provider"/> — which is what d47 did, so such a file sounds identical until
    /// <see cref="Audio.VoiceGroups.Migrated"/> moves it, once and deliberately. An empty map is
    /// a different thing: a file this build has written whose Commander has put every slot back
    /// onto the ship's provider by hand. Nullable rather than merely empty so those two are
    /// distinguishable, exactly as <see cref="VoicesProvider"/> is.
    /// </para>
    /// <para>
    /// An absent key means the same as <see cref="Provider"/>, so a map that is written but
    /// partial is safe rather than silent — and a provider id d47 no longer ships resolves the
    /// way every other selection does, through <c>TtsProviderCatalog.Selected</c>.
    /// </para>
    /// </summary>
    public IReadOnlyDictionary<string, string>? GroupProviders { get; init; }

    /// <summary>Null means the provider's own default voice rather than pinning one here.</summary>
    public string? Voice { get; init; }

    /// <summary>
    /// Which provider the stored voices were chosen from — this one's, the two named roles',
    /// and every persona pairing.
    /// <para>
    /// A voice id is only meaningful to the provider that issued it, and without this there is
    /// nothing in the file that says which one that was. d47 used to notice a mismatch only by
    /// watching the provider change while it was running, which left a settings file that was
    /// <em>already</em> mismatched — written by an older build, or by a switch that never
    /// reached the check — failing every sentence on every launch, forever, with no way to
    /// recover from the panel: the voice picker's choices come from the new provider's list,
    /// and a rejected key makes that list empty.
    /// </para>
    /// <para>
    /// Null means a file written before this was recorded. Trusted rather than cleared, because
    /// clearing on a guess would throw away the choices of every Commander whose file was fine;
    /// a genuine mismatch is caught at the seam instead, by <see cref="Audio.TtsFault
    /// .VoiceRejected"/>.
    /// </para>
    /// </summary>
    public string? VoicesProvider { get; init; }

    /// <summary>
    /// The same fact for the carrier's two voices, which since Phase 57 can be on a different
    /// provider from the ship's.
    /// <para>
    /// A second property rather than a map keyed by slot, because exactly two slots own a stored
    /// voice: the ship's AI (with the per-core pairings behind it) and the carrier. The other
    /// four draw their voices from the provider's pool at the moment somebody speaks, so there is
    /// nothing of theirs to remember and a map would have four keys that could never be written.
    /// </para>
    /// <para>
    /// Null carries the same meaning it does above — a file written before this was recorded, and
    /// trusted rather than cleared for the same reason.
    /// </para>
    /// </summary>
    public string? CarrierVoicesProvider { get; init; }

    /// <summary>
    /// 1.0 is the voice's natural pace. Normalised here and converted at the provider seam,
    /// because providers disagree about both the units and the range (Phase 11).
    /// <para>
    /// The rate you like in general. <see cref="ProviderRates"/> is "except on this one".
    /// </para>
    /// </summary>
    public double Rate { get; init; } = 1.0;

    /// <summary>
    /// Which of Kokoro's eight published ONNX builds the local voice runs on (#139).
    /// <para>
    /// <b>Null means the default</b>, which is <c>fp32</c> and is what every file written before
    /// this existed meant too — so an older settings file needs no migration and sounds identical.
    /// </para>
    /// <para>
    /// <b>Written only once the file is on disk</b>, which is the rule the speech-to-text model row
    /// already enforces and the reason it is enforced here: a row that can name a build d47 cannot
    /// load is a silent voice on the next launch. It is a record of what was installed rather than
    /// a request for it, and <see cref="Speech.KokoroAssets.InstalledBuild"/> is what can contradict
    /// it — that reads the model file's own byte count, so a Commander who replaced the file by
    /// hand is told the truth rather than what this says.
    /// </para>
    /// </summary>
    public string? LocalVoiceBuild { get; init; }

    /// <summary>
    /// Speaking rate per provider, keyed by provider id, overriding <see cref="Rate"/> where
    /// present (Phase 11: "Differences between providers, such as speed, is maintained
    /// on a per-provider basis").
    /// <para>
    /// Normalising at the seam gets the <em>units</em> agreeing; it does not make 1.15 sound
    /// the same on two different synthesisers, and it cannot — one has a wide percentage offset
    /// and the other a multiplier it refuses to exceed. So the value the Commander settled on
    /// for one provider is remembered against that provider, and switching does not carry a
    /// number that meant something else.
    /// </para>
    /// </summary>
    public IReadOnlyDictionary<string, double> ProviderRates { get; init; } =
        new Dictionary<string, double>();

    /// <summary>
    /// The voices chosen while each <em>other</em> provider was selected, keyed by provider id
    /// (Phase 19, "Remember which voice you chose for each provider").
    /// <para>
    /// <see cref="ProviderRates"/> is the shape this copies and the reason it could not simply
    /// be copied: a rate is one number and a voice choice is the ship's, two named roles, one
    /// per core and the flag saying the pairing has run. So the value is a record rather than a
    /// scalar, and <see cref="VoiceMemory"/> owns moving choices in and out of it.
    /// </para>
    /// <para>
    /// The provider currently selected is never in here — its choices are the live ones above.
    /// An entry appears when a provider is switched away from and is consumed when it is
    /// switched back to, so the map holds the providers not in use and nothing else.
    /// </para>
    /// </summary>
    public IReadOnlyDictionary<string, VoiceChoices> ProviderVoices { get; init; } =
        new Dictionary<string, VoiceChoices>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// What a thousand characters costs, in US dollars, per provider (Phase 19, "What
    /// the voices cost, beside what the model costs").
    /// <para>
    /// Absent means the provider's published list price stands. Present means the Commander has
    /// corrected it, which they are the only ones who can: the rate actually paid is a property
    /// of a subscription tier, and the API reports neither the tier nor whether the month's
    /// bundled credits have run out.
    /// </para>
    /// </summary>
    public IReadOnlyDictionary<string, double> CharacterPrices { get; init; } =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The same thing per minute of audio, for a provider whose bill is a function of that rather
    /// than of the characters handed over
    /// (<a href="https://github.com/dseelinger/d47/issues/63">#63</a>).
    /// <para>
    /// <b>A second dictionary rather than a re-meaning of <see cref="CharacterPrices"/>.</b> This
    /// file is append-only: a property never changes what it means, and a per-minute number
    /// arriving under a name that says characters would be read as characters by every build
    /// written before it.
    /// </para>
    /// <para>
    /// Absent carries the same meaning it does above — the published figure stands — and the same
    /// caveat applies with one more on top: OpenAI publishes no per-minute rate at all, so the
    /// default is a proxy derived from the rate they do publish. See
    /// <c>TtsProviderCatalog.OpenAi</c>, where that is set out rather than assumed.
    /// </para>
    /// </summary>
    public IReadOnlyDictionary<string, double> MinutePrices { get; init; } =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The voice a fleet carrier answers in, or null for the ship AI's (Phase 11,
    /// "Carrier Captain").
    /// </summary>
    public string? CarrierCaptainVoice { get; init; }

    /// <summary>And its tower, separately, because they are two people.</summary>
    public string? TowerVoice { get; init; }

    /// <summary>
    /// Whether in-game messages are spoken aloud, re-voiced (Phase 11, "Speak incoming
    /// messages in another voice").
    /// <para>
    /// Off by default, and not only because it is chatty. Message text is written by other
    /// players and turning this on sends it to a third-party synthesiser — that is egress the
    /// Commander should opt into rather than discover.
    /// </para>
    /// </summary>
    public bool SpeakIncomingMessages { get; init; }

    /// <summary>
    /// Whether NPC chatter is included when messages are spoken. Its own switch because the
    /// volume is completely different: a station approach produces a steady stream of NPC
    /// traffic, and a Commander who wants to hear their wing does not necessarily want that.
    /// </summary>
    public bool SpeakNpcMessages { get; init; }

    /// <summary>
    /// The output device id, or null for the system default. An id rather than a name because
    /// a friendly name is not stable across driver updates.
    /// </summary>
    public string? OutputDevice { get; init; }

    /// <summary>The loop-state cues (Phase 5, #20).</summary>
    public bool CuesEnabled { get; init; } = true;

    /// <summary>The bed under a working turn (#18).</summary>
    public bool ThinkingBedEnabled { get; init; } = true;

    /// <summary>Which bed. Names come from the shipped set, never from a literal list.</summary>
    public string? ThinkingBed { get; init; }

    /// <summary>
    /// <b>Cancel</b> (Phase 5 as "Shut up"; widened by
    /// <a href="https://github.com/dseelinger/d47/issues/221">#221</a>). System-wide rather than
    /// window-scoped, because the case this exists for is Elite holding the foreground — a key
    /// that only works when d47 has focus is gated by definition, and this one is never gated.
    /// <para>
    /// <b>The name is older than the job and stays that way.</b> It bound "shut up" from Phase 5;
    /// it now abandons the running turn as well, which stops the spending rather than only the
    /// mouth. <c>settings.json</c> is append-only, so a property is never renamed — a build that
    /// renamed this would unbind every Commander who had set it.
    /// </para>
    /// <para>
    /// Protected: it gates nothing dangerous, but a model that can unbind the Commander's
    /// stop button has removed the one control that outranks it.
    /// </para>
    /// </summary>
    public string? ShutUpHotkey { get; init; } = "Ctrl+Alt+X";

    /// <summary>
    /// The same act on a stick button (#221), beside the key rather than instead of it — the
    /// arrangement push-to-talk already has, for the reason it has it: a Commander who bound both
    /// said two things rather than replaced one.
    /// <para>
    /// <b>Cancel is the second binding in d47 that takes a stick button, and the first that fires
    /// once.</b> Push-to-talk needs both edges and is held; this is a press. Both are polled from
    /// the tick through <c>BoundButton</c>, because Windows does not deliver controller buttons to
    /// a registered hotkey — which is why the interface hotkeys stay keyboard-only.
    /// </para>
    /// <para>
    /// Unbound out of the box. A Commander on a HOTAS has every button spoken for already, and
    /// guessing one is how d47 would take a button Elite wanted.
    /// </para>
    /// </summary>
    public string? CancelButton { get; init; }

    /// <summary>How many times a failing turn is tried in total. 1 disables retrying.</summary>
    public int RetryAttempts { get; init; } = 3;

    public double RetryWaitSeconds { get; init; } = 2;

    /// <summary>"sequential" or "logarithmic" (Phase 5).</summary>
    public string RetryBackoff { get; init; } = "sequential";

    /// <summary>How long one attempt may run before it counts as a failure worth reporting.</summary>
    public double TurnTimeoutSeconds { get; init; } = 45;
}

public sealed record LoggingSettings
{
    /// <summary>Applies to any subsystem with no explicit entry below.</summary>
    public LogLevel Default { get; init; } = LogLevel.Information;

    /// <summary>
    /// Per-subsystem overrides, keyed by <see cref="Diagnostics.Subsystems"/> name. Unknown
    /// subsystem names are rejected on load along with any other unknown key.
    /// </summary>
    public IReadOnlyDictionary<string, LogLevel> Subsystems { get; init; } =
        new Dictionary<string, LogLevel>();
}

public sealed record UiSettings
{
    /// <summary>
    /// A theme id from the shipped set. Colour lives in one place and no view hardcodes a
    /// literal, so this is the only thing that has to change to repaint the app (Phase 4,
    /// "Themes").
    /// </summary>
    public string Theme { get; init; } = "elite";

    /// <summary>
    /// Whether the settings page shows every row, or only the ones most Commanders change
    /// (<a href="https://github.com/dseelinger/d47/issues/60">#60</a>).
    /// <para>
    /// <b>Off by default, and nothing is switched off by being hidden.</b> Every folded row keeps
    /// working at its value or its last setting — this is a display decision and touches no other
    /// setting, ever. See <see cref="SettingsFold"/>, which owns the three cases that outrank it.
    /// </para>
    /// </summary>
    public bool ShowEverySetting { get; init; }

    /// <summary>
    /// How large the panel is drawn, as a percentage (Phase 9, "Zoom the desktop
    /// window"). A setting rather than view state, because the checklist puts it alongside the
    /// theme: it is how the Commander wants d47 to look, not how they happened to leave a card.
    /// <para>
    /// Snapped to <see cref="Interface.ZoomLadder"/> on read, so a hand-edited 137 becomes 125
    /// rather than a level no gesture can step off.
    /// </para>
    /// </summary>
    public int ZoomPercent { get; init; } = Interface.ZoomLadder.Default;

    /// <summary>
    /// Which content set the desktop window is showing: "full" or "mini" (Phase 51).
    /// <para>
    /// <b>Its own row rather than the headset's.</b> Phase 45 settled the principle in one line —
    /// what you are reading is shared, and mini/full and zoom are how a surface draws it — so this
    /// sits here beside <see cref="Theme"/> and <see cref="ZoomPercent"/>, and <c>vr.mode</c> goes
    /// on meaning the quad. A Commander in a headset who says "mini panel" must not shrink a
    /// window they cannot see, and one at a desk must not resize a quad they are not wearing.
    /// </para>
    /// <para>
    /// Machine-wide rather than per-Commander, which is the default scope and is deliberate here:
    /// <b>a window is a property of the desk and not of whoever is flying today.</b>
    /// </para>
    /// </summary>
    public string Mode { get; init; } = "full";

    /// <summary>
    /// The mini panel on a monitor, for a Commander with no headset (Phase 48). Under
    /// <see cref="UiSettings"/> rather than beside <see cref="VrSettings"/> because there is no
    /// headset in it: a Commander goes looking where the theme, the zoom and the hotkeys already
    /// are rather than under a card about a device they are not wearing.
    /// </summary>
    public OverlaySettings Overlay { get; init; } = new();
}

/// <summary>
/// The flat mini panel: a chromeless, click-through, topmost strip pinned over the game
/// (Phase 48).
/// <para>
/// Three knobs and nothing else. Where it sits is deliberately <em>not</em> here — a monitor
/// coordinate is not something a Commander typed, so it joins the VR anchors and the window's
/// own rectangle in <see cref="ViewState"/>, and <c>settings.json</c> is append-only for
/// anything that ever is.
/// </para>
/// </summary>
public sealed record OverlaySettings
{
    /// <summary>
    /// <b>Off by default.</b> A strip pinned over the screen is the most intrusive thing d47 can
    /// draw, and it is for one arrangement — no headset, one monitor, the game in front — rather
    /// than for everybody. Off also settles the headset question by itself: a Commander in VR who
    /// wants this as well turns it on, and one who does not never meets it.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// How large the strip is drawn, on <see cref="Interface.ZoomLadder"/>'s rungs.
    /// <para>
    /// <b>Scale is the lever, because there are no metres.</b> The headset's mini panel is fixed
    /// at 512x280 for a stated reason — apparent size there is the pixel count and the quad's
    /// width in metres together, so the width is the lever and the height is a floor under a
    /// reduced content set — and on a monitor half of that product is missing. So the pixel size
    /// falls out of the ladder instead: a <c>LayoutTransform</c> that re-measures and rewraps
    /// rather than a scale that blurs, which is the same mechanism as
    /// <c>ui.zoom</c> seen from a second room.
    /// </para>
    /// <para>
    /// Its own rung rather than following <see cref="ZoomPercent"/>: the window is read at a desk
    /// and the strip is read at a glance from further back, and a Commander who zooms the window
    /// to read a table has not asked for a bigger thing over their cockpit.
    /// </para>
    /// </summary>
    public int ScalePercent { get; init; } = Interface.ZoomLadder.Default;

    /// <summary>
    /// How solid it is. One number for one surface, unlike the headset's, which needed a repair
    /// to become one knob for two.
    /// <para>
    /// Not all the way down to nothing: an overlay at zero is an overlay that is on, invisible,
    /// and indistinguishable from broken — which is the exact failure the display-mode row exists
    /// to prevent arriving by another road.
    /// </para>
    /// </summary>
    public double Opacity { get; init; } = 0.9;
}

/// <summary>
/// Bound gestures, stored as the display form the binding UI produces ("Ctrl+Shift+S"). One
/// property per action rather than a dictionary: an unknown action in a hand-edited file has
/// to be rejected like any other unknown key, and a dictionary would accept it silently.
/// <para>
/// These are window-scoped. A gesture that works while Elite has the foreground needs a
/// system-wide registration, which arrives with the phase that needs it — push-to-talk in
/// Phase 6 — rather than being built here for nothing to use.
/// </para>
/// </summary>
public sealed record HotkeySettings
{
    /// <summary>
    /// Ctrl+comma, which is what VS Code, Chrome, Slack and Discord all use for settings.
    /// Stored in <see cref="Avalonia"/>'s own gesture spelling, hence OemComma.
    /// </summary>
    public string? OpenSettings { get; init; } = "Ctrl+OemComma";

    public string? FocusAsk { get; init; } = "Ctrl+L";

    /// <summary>
    /// Snaps every world-locked headset surface back in front of the Commander (Phase 9,
    /// "Re-anchor the panels").
    /// <para>
    /// System-wide rather than window-scoped, and that is the whole point of it: the case this
    /// exists for is Elite holding the foreground with the panels drifted out of position, so a
    /// gesture that needs d47 focused is a gesture that does not work when it is wanted.
    /// </para>
    /// <para>
    /// Protected, like every hotkey row.
    /// </para>
    /// </summary>
    public string? Reanchor { get; init; } = "Ctrl+Alt+R";

    /// <summary>
    /// Binds the core aboard to the ship the Commander is in, and unbinds it when it is already
    /// that core (Phase 35, "The binding is the Commander's, and unreachable from the
    /// model").
    /// <para>
    /// System-wide, like re-anchoring and for the same reason: the moment this is wanted is the
    /// moment they are flying the ship, which is a moment Elite has the foreground. A gesture
    /// needing d47 focused would be a gesture that only works when the question is theoretical.
    /// </para>
    /// <para>
    /// One gesture for both directions rather than two. The act is a Commander saying "this one,
    /// for this ship" while sitting in it, and saying it again about the same core is them taking
    /// it back — which is the only reading of a second press that means anything, since binding
    /// the core already bound is a no-op.
    /// </para>
    /// </summary>
    public string? BindShipCore { get; init; } = "Ctrl+Alt+B";

    /// <summary>
    /// Shows and hides the flat mini panel (Phase 48).
    /// <para>
    /// <b>System-wide, and it is the one control that reaches the overlay from where it is
    /// looked at.</b> The strip is output-only — the pointer goes straight through it — so every
    /// way of changing it is somewhere else, and the somewhere else that works with a game
    /// filling the screen is a key.
    /// </para>
    /// <para>
    /// It writes the setting rather than holding a visibility of its own, so what a Commander did
    /// with a key and what the row says are one state and survive a restart together.
    /// </para>
    /// </summary>
    public string? ShowOverlay { get; init; } = "Ctrl+Alt+O";

    /// <summary>
    /// Puts the flat mini panel into place mode, where it briefly takes clicks so it can be
    /// dragged, and gives them back the moment it is done (Phase 48).
    /// <para>
    /// <b>Its own gesture rather than a row, because placement has to be explicit and has to be
    /// reachable from where the overlay is visible.</b> A settings page cannot be the answer here,
    /// since the overlay is hidden whenever d47's own window is the thing in front of the
    /// Commander.
    /// </para>
    /// </summary>
    public string? MoveOverlay { get; init; } = "Ctrl+Alt+M";

    /// <summary>
    /// Puts the desktop window into mini and back (Phase 51).
    /// <para>
    /// <b>This is the way back, and the way back must not live in the thing that disappears.</b>
    /// Mini takes the tab strip, the mode control, the breadcrumb, the search box, the banners and
    /// the header, all of it by design — so a Commander who lands there with no way out has
    /// bricked their window, and a first run that ends in a force-quit is the only way a feature
    /// like this is remembered. This is one of three ways back and the one that works when there
    /// is nothing at all on the surface; the others are the spoken phrase and the title bar, which
    /// mini keeps.
    /// </para>
    /// <para>
    /// Window-scoped rather than system-wide, unlike <see cref="ShowOverlay"/> and
    /// <see cref="MoveOverlay"/>. Those exist for a moment when Elite is filling the screen and
    /// d47 cannot be focused; this one is pressed by somebody looking at the window it acts on.
    /// </para>
    /// </summary>
    public string? WindowMode { get; init; } = "Ctrl+M";
}

public sealed record UpdateSettings
{
    /// <summary>
    /// The startup check contacts GitHub, so it is egress and is disclosed as such. Turning it
    /// off is part of what makes local-only operation a reachable configuration rather than a
    /// theoretical one (Phase 4, "Say what each provider receives").
    /// </summary>
    public bool CheckOnStartup { get; init; } = true;
}

/// <summary>
/// Where donations go (<a href="https://github.com/dseelinger/d47/issues/175">#175</a>).
/// <para>
/// <b>One field, and it is the switch as well as the address.</b> That is the shape the community
/// goals row already has and it is the right one twice over: the destination cannot be reached at
/// all until somebody puts an address in, which is a clearer act of consent than a checkbox, and
/// clearing it is how donation egress is turned off. A separate toggle beside an empty address
/// would be a control with nothing to control, and a second thing to get out of step with the
/// first.
/// </para>
/// </summary>
public sealed record DonationSettings
{
    /// <summary>
    /// The endpoint a donation is posted to, or null where there is none and donations can only
    /// be copied or saved.
    /// <para>
    /// <b>Null out of the box, and that is not a placeholder for a missing decision.</b> The store
    /// behind this is R2, which requires a payment method on an account that currently has none —
    /// a deliberate act rather than a footnote — so there is no address to bake in until the
    /// Commander has performed it. A shipped build with a default here would be a build sending
    /// donations somewhere before anybody chose to.
    /// </para>
    /// <para>
    /// <b>Reachable from the panel and not from the model.</b> Its row is protected: an address
    /// the model could set is an address a hostile in-game message could set, and this one names
    /// where a scrubbed journal goes.
    /// </para>
    /// </summary>
    public string? Endpoint { get; init; }
}
