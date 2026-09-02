using D47.Core.Audio;

namespace D47.Core.Callouts;

/// <summary>How much a callout outranks whatever else is being said.</summary>
public enum CalloutUrgency
{
    /// <summary>
    /// Worth saying, not worth interrupting for. Route progress, arrivals, milestones. Queued
    /// behind whatever is currently being spoken.
    /// </summary>
    Routine,

    /// <summary>
    /// Said now, over the top of anything else. Danger and fuel. An alert that waits for the
    /// current sentence to finish is not an alert, which is why <see cref="AudioChannel.Alert"/>
    /// exists above <see cref="AudioChannel.Speech"/>.
    /// </summary>
    Urgent,
}

/// <summary>
/// Something d47 has decided to say without being asked (Phase 8).
/// <para>
/// A callout emits one of these from the tick loop, which is synchronous and must not block —
/// so this is a description of what to say, not the act of saying it. The app drains these onto
/// the thread pool and into the voice pipeline. That split is what keeps a slow TTS synthesis
/// from stalling push-to-talk.
/// </para>
/// </summary>
/// <param name="Key">
/// Identity for cooldown purposes: two announcements sharing a key are the same warning said
/// twice. "fuel.low", "danger.shields", "route.progress" — coarse enough that a repeat is
/// suppressed, specific enough that a different warning is not.
/// </param>
/// <param name="Text">What to say. Complete sentences: this goes to a voice, not a status bar.</param>
public sealed record Announcement(string Key, string Text, CalloutUrgency Urgency = CalloutUrgency.Routine)
{
    /// <summary>
    /// How long this key stays suppressed after being said. Zero means the callout manages its
    /// own repetition — the material milestones do, since each threshold fires once by
    /// construction and a time-based cooldown would swallow a legitimate second one.
    /// </summary>
    public TimeSpan Cooldown { get; init; } = TimeSpan.Zero;

    public AudioChannel Channel =>
        Urgency == CalloutUrgency.Urgent ? AudioChannel.Alert : AudioChannel.Speech;

    /// <summary>
    /// A marker played immediately ahead of the line, saying which warning this is before the
    /// sentence has arrived (Phase 15).
    /// <para>
    /// Null for everything that came before Phase 15, which is most of what is said: a cue per
    /// announcement would make the common ones into an alarm and would leave the Commander no way
    /// to tell the four that matter apart. It is set where the game has already said which
    /// situation this is, and that is the only case where a distinct sound can mean anything.
    /// </para>
    /// </summary>
    public AlertCue? Cue { get; init; }

    /// <summary>
    /// Which of a callout's stock lines this is, when it has a numbered set to pick from — the
    /// index <see cref="AmbientLines.Pick"/> was given. Null for every callout with one line to
    /// say (Phase 43).
    /// <para>
    /// Carried here for the reason the situation travels on the key: the app asks the model for
    /// the line in character after the callout has moved on, and what it asks for depends on
    /// which one this was. It is the only deterministic index a flavour call has, which is what
    /// makes it the thing that decides whether the Commander's story rides along — no Core
    /// component reads a clock or a seed, and a recorded session has to replay to the same call.
    /// </para>
    /// </summary>
    public int? Variant { get; init; }

    /// <summary>
    /// The least time the Commander asked for between two of these, or null for an announcement
    /// said because something <em>happened</em>
    /// (<a href="https://github.com/dseelinger/d47/issues/257">#257</a>).
    /// <para>
    /// <b>Non-null is the signal</b>, the way <see cref="Transcript"/>'s is. It marks the two
    /// callouts that speak because nothing has — a remark from inside the ship, an exchange among
    /// people outside it — and <see cref="CalloutEngine"/> keeps any two of them apart. Two of
    /// these arriving nose to tail is one companion filling silence with itself, which is the
    /// impression each one's own timing rules were written to avoid.
    /// </para>
    /// <para>
    /// <b>And the value is the clamp, from both ends.</b> The floor in force is the least of
    /// <see cref="CalloutEngine.ChatterSpacing"/>, this, and whatever the line that last spoke
    /// asked for — so nothing is ever held longer than its own row asks, and no voice can demand
    /// more air behind it than it leaves in front of itself. The second half is what stops a fast
    /// kind starving a slow one instead of spacing it out. Together they are what make the floor
    /// undetectable to a Commander running one of the two, and keep it a rule <em>between</em> the
    /// features rather than a rule about either.
    /// </para>
    /// <para>
    /// <b>Orthogonal to <see cref="Urgency"/>, which answers a different question.</b> Route
    /// progress, an arrival and a milestone are all <see cref="CalloutUrgency.Routine"/> and none
    /// of them is chatter: they report something that happened, and a floor that delayed them
    /// would be spacing out the news. Urgency answers "does this interrupt"; this answers "was
    /// anything the matter", and neither can be derived from the other.
    /// </para>
    /// <para>
    /// Carried rather than read back off <see cref="Cooldown"/>, which happens to hold the same
    /// value today. That field is an identity for suppression and nothing promises to keep it
    /// equal to the interval — the argument <see cref="CommsChannel"/> already makes about
    /// <see cref="Key"/>, for the same reason.
    /// </para>
    /// </summary>
    public TimeSpan? Chatter { get; init; }

    /// <summary>
    /// Who says it. Defaults to the ship's AI, which is what every Phase 8 callout is —
    /// d47 speaking. Phase 11 adds announcements that are somebody else talking: a re-voiced
    /// in-game message, a carrier's tower. Carried here rather than resolved by the caller
    /// because the callout is the only thing that knows whose line it is.
    /// </summary>
    public Audio.VoiceRole Voice { get; init; } = Audio.VoiceRole.ShipAi;

    /// <summary>
    /// The individual speaking, when the role has more than one member — the name of the
    /// Commander or NPC whose message this is. Null for the ship AI and for the carrier roles,
    /// which have exactly one member each.
    /// <para>
    /// Untrusted: another Commander chose this text. It is used to look up a voice and to label
    /// the line on the panel, and it never reaches the model.
    /// </para>
    /// </summary>
    public string? Speaker { get; init; }

    /// <summary>
    /// Whether <see cref="Speaker"/> is a player rather than an NPC. Decides which scope their
    /// voice assignment lives in: players survive hyperspace, NPCs do not (Phase 11).
    /// </summary>
    public bool SpeakerIsPlayer { get; init; }

    /// <summary>
    /// The in-game chat channel this arrived on, or null for a line that is not chat — every
    /// callout, the crew, the carrier (Phase 57).
    /// <para>
    /// <b><see cref="SpeakerIsPlayer"/> cannot answer what this answers.</b> That boolean says
    /// whether a person typed it, which is the question Phase 11 needed: it decides how long a
    /// voice assignment sticks. Phase 57 asks a different one — is this a person the Commander
    /// <em>chose</em> to be in contact with — and a squadron mate and a stranger shouting in
    /// local are both players. The channel is what separates them, and
    /// <see cref="Audio.VoiceGroups.Of"/> is what reads it.
    /// </para>
    /// <para>
    /// Carried rather than parsed back out of <see cref="Key"/>, which does hold it today. That
    /// key is an identity for cooldown purposes and nothing else promises to keep its shape;
    /// routing a bill to a provider off a string that exists to suppress repeats is a coupling
    /// that would break quietly.
    /// </para>
    /// </summary>
    public string? CommsChannel { get; init; }

    /// <summary>
    /// The line to write onto the panel's Technical page, or null for an announcement that is
    /// only ever heard.
    /// <para>
    /// Separate from <see cref="Text"/> because the two want different things. What is spoken is
    /// a voice arriving in a headset, and it does not need to be told whose voice it is — the
    /// voice is the answer. What is read is a page of lines with no voices on it, and a message
    /// with no sender on it is unattributable. So a re-voiced message says just the words and
    /// writes down who said them.
    /// </para>
    /// <para>
    /// Non-null is also the signal that this belongs on the Technical page rather than on the
    /// conversation: in-game comms are neither the conversation nor a diagnostic, and that page
    /// is where "true, useful, and not the conversation" already lives. What the ship's AI says
    /// goes the other way — see <see cref="ConversationLine"/>.
    /// </para>
    /// </summary>
    public string? Transcript { get; init; }

    /// <summary>
    /// The line the <em>conversation</em> page should carry, or null when this belongs on
    /// another page.
    /// <para>
    /// A callout in the ship's AI's own voice is d47 talking, and the page for the Commander and
    /// the ship's AI is the conversation — which also puts it on Technical, that being the same
    /// runs with the diagnostics left in. This documentation used to claim every Phase 8 callout
    /// "already reaches the transcript by the route everything d47 says reaches it". Nothing did:
    /// a fuel warning was heard once and was afterwards findable only in the log file
    /// (remediation.md, "Ship AI callouts belong on the Conversation and Technical tabs").
    /// </para>
    /// <para>
    /// Read after the line has been varied, so what is written is what was said rather than the
    /// authored line a persona may have replaced.
    /// </para>
    /// </summary>
    public string? ConversationLine =>
        Transcript is null && Voice == Audio.VoiceRole.ShipAi ? Text : null;
}
