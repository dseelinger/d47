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

    public AudioChannel Channel =>
        Urgency == CalloutUrgency.Urgent ? AudioChannel.Alert : AudioChannel.Speech;

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

    /// <summary>Whether <see cref="Speaker"/> is a player rather than an NPC.</summary>
    public bool SpeakerIsPlayer { get; init; }

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

    /// <summary>The line the conversation page should carry, or null when this belongs on another page.</summary>
    public string? ConversationLine =>
        Transcript is null && Voice == Audio.VoiceRole.ShipAi ? Text : null;
}
