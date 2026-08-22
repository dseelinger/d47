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
/// Something d47 has decided to say without being asked (list.md Phase 8).
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
    /// sentence has arrived (list.md Phase 15).
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
    /// say (list.md Phase 43).
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
    /// voice assignment lives in: players survive hyperspace, NPCs do not (list.md Phase 11).
    /// </summary>
    public bool SpeakerIsPlayer { get; init; }

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
