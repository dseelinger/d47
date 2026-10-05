using D47.Core.Audio;

namespace D47.Core.Callouts;

/// <summary>How much a callout outranks whatever else is being said.</summary>
public enum CalloutUrgency
{
    /// <summary>Worth saying, not worth interrupting for.</summary>
    Routine,

    /// <summary>Said now, over the top of anything else.</summary>
    Urgent,
}

/// <summary>The arbiter groups unprompted speech is spoken in.</summary>
public static class SpokenGroup
{
    /// <summary>Callouts and relayed in-game comms.</summary>
    public const string Announcement = "announcement";

    /// <summary>Invented chatter, kept apart so a turn reply can drop it and nothing else (#61).</summary>
    public const string InventedChatter = "invented-chatter";
}

/// <summary>Something d47 has decided to say without being asked (Phase 8).</summary>
/// <param name="Key">
/// Identity for cooldown purposes: two announcements sharing a key are the same warning said twice.
/// "fuel.low", "danger.shields", "route.progress" — coarse enough that a repeat is suppressed, specific
/// enough that a different warning is not.
/// </param>
/// <param name="Text">What to say.</param>
public sealed record Announcement(string Key, string Text, CalloutUrgency Urgency = CalloutUrgency.Routine)
{
    /// <summary>How long this key stays suppressed after being said.</summary>
    public TimeSpan Cooldown { get; init; } = TimeSpan.Zero;

    /// <summary>A line not addressed to the Commander, heard as traffic further off.</summary>
    public bool Overheard { get; init; }

    public AudioChannel Channel =>
        Urgency == CalloutUrgency.Urgent ? AudioChannel.Alert
        : Overheard ? AudioChannel.Overheard
        : AudioChannel.Speech;

    /// <summary>The arbiter group this is spoken in; a reply drops invented chatter by it.</summary>
    public string Group =>
        Key == NpcChatter.LineKey ? SpokenGroup.InventedChatter : SpokenGroup.Announcement;

    /// <summary>
    /// A marker played immediately ahead of the line, saying which warning this is before the sentence
    /// has arrived (Phase 15).
    /// </summary>
    public AlertCue? Cue { get; init; }

    /// <summary>
    /// Which of a callout's stock lines this is, when it has a numbered set to pick from — the index
    /// <see cref="AmbientLines.Pick"/> was given.
    /// </summary>
    public int? Variant { get; init; }

    /// <summary>
    /// The least time the Commander asked for between two of these, or null for an announcement said
    /// because something happened (#257).
    /// </summary>
    public TimeSpan? Chatter { get; init; }

    /// <summary>Who says it.</summary>
    public Audio.VoiceRole Voice { get; init; } = Audio.VoiceRole.ShipAi;

    /// <summary>
    /// The individual speaking, when the role has more than one member — the name of the Commander or
    /// NPC whose message this is.
    /// </summary>
    public string? Speaker { get; init; }

    /// <summary>The <see cref="Seats.CrewSeat.Id"/> of the crew seat speaking, or null for a line no seat speaks.</summary>
    public string? Seat { get; init; }

    /// <summary>
    /// The provider and voice a story's cast member speaks in, whatever the slot settings say, or null for a line
    /// spoken by its role. A pinned line gets the member's own treatment and nothing else.
    /// </summary>
    public Stories.PinnedVoice? Pinned { get; init; }

    /// <summary>Whether <see cref="Speaker"/> is a player rather than an NPC.</summary>
    public bool SpeakerIsPlayer { get; init; }

    /// <summary>
    /// The docked station's <c>StationAllegiance</c> when <see cref="Speaker"/> is that station, or null
    /// — most stations have none, and every other speaker is unaffected (#68).
    /// </summary>
    public string? SpeakerAllegiance { get; init; }

    /// <summary>
    /// The in-game chat channel this arrived on, or null for a line that is not chat — every callout,
    /// the crew, the carrier (Phase 57).
    /// </summary>
    public string? CommsChannel { get; init; }

    /// <summary>
    /// The line to write onto the panel's Technical page, or null for an announcement that is only ever
    /// heard.
    /// </summary>
    public string? Transcript { get; init; }

    /// <summary>The raw <c>$</c>-key of a Frontier-canned message from the Commander's own carrier, or null.</summary>
    public string? MessageKey { get; init; }

    /// <summary>
    /// A Frontier-written line the subject of this announcement said, for a flavour brief to refer back
    /// to, or null. Never spoken as written.
    /// </summary>
    public string? Callback { get; init; }

    /// <summary>The invented chatter line this speaks, or null for anything else.</summary>
    public NpcChatterHeard? Invented { get; init; }

    /// <summary>The beat a scene marker asks an exchange for, or null for anything else.</summary>
    public SceneBeat? Scene { get; init; }

    /// <summary>The running story's aside for a line about a mission, or null. A line with no text is spoken only as the model writes it.</summary>
    public Stories.MissionAside? StoryAside { get; init; }

    /// <summary>Words said unchanged after <see cref="Text"/>. Never given to a model, so never put in a brief or the conversation feed.</summary>
    public string? Verbatim { get; init; }

    /// <summary><see cref="Text"/> followed by <see cref="Verbatim"/>, which is what is heard; a full stop ends a <see cref="Text"/> that has none.</summary>
    public string Heard => Verbatim is not { Length: > 0 } verbatim ? Text
        : Text.TrimEnd() is { Length: > 0 } lead && !".!?:;".Contains(lead[^1], StringComparison.Ordinal) ? $"{lead}. {verbatim}"
        : $"{Text.TrimEnd()} {verbatim}";

    /// <summary>The line the conversation page should carry, or null when this belongs on another page.</summary>
    public string? ConversationLine =>
        Transcript is null && Voice == Audio.VoiceRole.ShipAi ? Text : null;
}
