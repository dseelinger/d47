using Microsoft.Extensions.Logging;

namespace D47.Core.Configuration;

/// <summary>
/// The settings store's whole shape. Anything not declared here is an unknown key and is
/// rejected on load (list.md Phase 1).
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

    public UiSettings Ui { get; init; } = new();

    public HotkeySettings Hotkeys { get; init; } = new();

    public UpdateSettings Updates { get; init; } = new();

    public CalloutSettings Callouts { get; init; } = new();

    public ListeningSettings Listening { get; init; } = new();

    public VrSettings Vr { get; init; } = new();

    public ActionSettings Actions { get; init; } = new();

    public PersonaSettings Persona { get; init; } = new();
}

/// <summary>
/// Which companion character is aboard (list.md Phase 11).
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
    /// The voice paired to each core, keyed by persona id (list.md Phase 11, #33). Written by
    /// the background pairing at first startup and by the Commander choosing one by hand;
    /// nothing distinguishes the two, on purpose, because a pairing the Commander has
    /// overridden should never be quietly re-derived.
    /// </summary>
    public IReadOnlyDictionary<string, string> Voices { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// Whether the background voice pairing has run. A flag rather than "is
    /// <see cref="Voices"/> empty", because a Commander who cleared every pairing by hand
    /// should not have them silently regenerated on the next launch.
    /// </summary>
    public bool VoicesPaired { get; init; }
}

/// <summary>
/// Acting on the game (list.md Phase 10).
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
}

/// <summary>
/// The headset (list.md Phase 9).
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
    /// </summary>
    public string Mode { get; init; } = "full";

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
    /// The caption layer. Its own block because captions are their own overlay, and because
    /// everything on it is something the caption standard leaves to the viewer - nothing here
    /// is a number the standard fixes.
    /// </summary>
    public Vr.CaptionSettings Captions { get; init; } = new();
}

/// <summary>
/// The microphone and the key that opens it (list.md Phase 6).
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

    /// <summary>"hold" or "toggle".</summary>
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
    /// In VR the GPU is already the scarce resource, so a large model running there surfaces as
    /// dropped frames and reprojection rather than as anything resembling a speech problem —
    /// which the checklist calls the hardest kind of setting to diagnose. A short push-to-talk
    /// clip on the small English models absorbs CPU inference fine.
    /// </para>
    /// </summary>
    public bool UseGpu { get; init; }
}

/// <summary>
/// What d47 says without being asked (list.md Phase 8).
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

    /// <summary>How often route progress is reported, in jumps. 0 silences the progress line.</summary>
    public int RouteEveryNJumps { get; init; } = 5;

    /// <summary>
    /// How long a hyperspace jump has to run before it is worth remarking on. The checklist
    /// specifies 20 seconds as the default.
    /// </summary>
    public double LongJumpSeconds { get; init; } = 20;

    /// <summary>
    /// The Commander's home system. Null means no home callout — there is no sensible default,
    /// since where someone considers home is not something any journal event reports.
    /// </summary>
    public string? HomeSystem { get; init; }

    /// <summary>
    /// In-character remarks about where the Commander is, said because nothing has happened
    /// rather than because something has (list.md Phase 11, "Ambient Voice").
    /// <para>
    /// On, because a companion that only ever answers questions is a search box with a voice.
    /// The interval is what makes that tolerable.
    /// </para>
    /// </summary>
    public bool Ambient { get; init; } = true;

    /// <summary>
    /// The shortest gap between two ambient remarks, in minutes. 0 silences them, which is the
    /// same as turning <see cref="Ambient"/> off and is offered because a Commander reaching for
    /// "less" will reach for this row rather than the switch.
    /// </summary>
    public int AmbientMinutes { get; init; } = 15;
}

public sealed record LlmSettings
{
    /// <summary>
    /// Which provider to use. "none" is a real, supported choice — every input path stays
    /// answerable through the model-free keyword router (list.md Phase 3).
    /// </summary>
    public string Provider { get; init; } = "anthropic";

    /// <summary>Null uses the provider's own default rather than pinning a model here.</summary>
    public string? Model { get; init; }

    /// <summary>
    /// Null uses the provider's published endpoint. A value here points at something else
    /// speaking the same protocol — a gateway or a proxy — which is why changing it clears
    /// <see cref="Model"/>: model ids are a property of the endpoint's namespace, and a name
    /// carried across from another endpoint is a stale selection that fails at the first turn
    /// (list.md Phase 4).
    /// </summary>
    public string? Endpoint { get; init; }

    /// <summary>
    /// False is "plain answers, no persona". The anti-invention guardrails are unaffected —
    /// they sit above the persona in the assembled prompt and there is no setter for them.
    /// </summary>
    public bool PersonalityEnabled { get; init; } = true;

    /// <summary>The Commander's standing prompt about themselves, kept between sessions.</summary>
    public string? AboutMe { get; init; }
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
    /// reachable rather than theoretical (list.md Phase 4).
    /// </summary>
    public string Provider { get; init; } = "edge";

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
    /// 1.0 is the voice's natural pace. Normalised here and converted at the provider seam,
    /// because providers disagree about both the units and the range (list.md Phase 11).
    /// <para>
    /// The rate you like in general. <see cref="ProviderRates"/> is "except on this one".
    /// </para>
    /// </summary>
    public double Rate { get; init; } = 1.0;

    /// <summary>
    /// Speaking rate per provider, keyed by provider id, overriding <see cref="Rate"/> where
    /// present (list.md Phase 11: "Differences between providers, such as speed, is maintained
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
    /// The voice a fleet carrier answers in, or null for the ship AI's (list.md Phase 11,
    /// "Carrier Captain").
    /// </summary>
    public string? CarrierCaptainVoice { get; init; }

    /// <summary>And its tower, separately, because they are two people.</summary>
    public string? TowerVoice { get; init; }

    /// <summary>
    /// Whether in-game messages are spoken aloud, re-voiced (list.md Phase 11, "Speak incoming
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

    /// <summary>The loop-state cues (list.md Phase 5, #20).</summary>
    public bool CuesEnabled { get; init; } = true;

    /// <summary>The bed under a working turn (#18).</summary>
    public bool ThinkingBedEnabled { get; init; } = true;

    /// <summary>Which bed. Names come from the shipped set, never from a literal list.</summary>
    public string? ThinkingBed { get; init; }

    /// <summary>
    /// Instant silence (list.md Phase 5, "Shut up"). System-wide rather than window-scoped,
    /// because the case this exists for is Elite holding the foreground — a key that only
    /// works when d47 has focus is gated by definition, and this one is never gated.
    /// <para>
    /// Protected: it gates nothing dangerous, but a model that can unbind the Commander's
    /// stop button has removed the one control that outranks it.
    /// </para>
    /// </summary>
    public string? ShutUpHotkey { get; init; } = "Ctrl+Alt+X";

    /// <summary>How many times a failing turn is tried in total. 1 disables retrying.</summary>
    public int RetryAttempts { get; init; } = 3;

    public double RetryWaitSeconds { get; init; } = 2;

    /// <summary>"sequential" or "logarithmic" (list.md Phase 5).</summary>
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
    /// literal, so this is the only thing that has to change to repaint the app (list.md
    /// Phase 4, "Themes").
    /// </summary>
    public string Theme { get; init; } = "elite";

    /// <summary>
    /// How large the panel is drawn, as a percentage (list.md Phase 9, "Zoom the desktop
    /// window"). A setting rather than view state, because the checklist puts it alongside the
    /// theme: it is how the Commander wants d47 to look, not how they happened to leave a card.
    /// <para>
    /// Snapped to <see cref="Interface.ZoomLadder"/> on read, so a hand-edited 137 becomes 125
    /// rather than a level no gesture can step off.
    /// </para>
    /// </summary>
    public int ZoomPercent { get; init; } = Interface.ZoomLadder.Default;
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
    /// Snaps every world-locked headset surface back in front of the Commander (list.md
    /// Phase 9, "Re-anchor the panels").
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
}

public sealed record UpdateSettings
{
    /// <summary>
    /// The startup check contacts GitHub, so it is egress and is disclosed as such. Turning it
    /// off is part of what makes local-only operation a reachable configuration rather than a
    /// theoretical one (list.md Phase 4, "Say what each provider receives").
    /// </summary>
    public bool CheckOnStartup { get; init; } = true;
}
