using D47.Core.Audio;

namespace D47.Core.Callouts;

/// <summary>
/// The voice an NPC line will be spoken in, and what its accent asks of the words written for it. Cores
/// get no accent brief: they follow their personas.
/// </summary>
public static class SpeakerAccent
{
    /// <summary>What the model may and may not do with an accent.</summary>
    public const string Rules =
        "Let the words suit that accent through regional word choice, idiom and rhythm. It can be "
        + "funny, but never spell the accent out phonetically, never lean on a stereotype, and never "
        + "make the dialect the joke.";

    /// <summary>The voice an announcement is spoken in, assigning a sender theirs on first use.</summary>
    public static VoiceSelection VoiceOf(VoiceCast cast, Announcement announcement)
    {
        ArgumentNullException.ThrowIfNull(cast);
        ArgumentNullException.ThrowIfNull(announcement);

        return announcement.Speaker is { Length: > 0 } speaker
            ? cast.ForSender(speaker, announcement.SpeakerIsPlayer, announcement.Voice, announcement.SpeakerAllegiance)
            : cast.For(announcement.Voice);
    }

    /// <summary>
    /// The accent brief for the voice that will speak an NPC's line, or null for a core, the crew, or a
    /// voice whose listing names no accent.
    /// </summary>
    public static string? For(VoiceCast cast, Announcement announcement)
    {
        ArgumentNullException.ThrowIfNull(announcement);

        return announcement.Voice is VoiceRole.Comms or VoiceRole.CarrierCaptain or VoiceRole.TowerControl
            ? Sentence(cast.AccentOf(VoiceOf(cast, announcement).VoiceId))
            : null;
    }

    /// <summary>"Your voice has a British accent." and the rules, or null for no accent.</summary>
    public static string? Sentence(string? accent) =>
        accent is { Length: > 0 } ? $"Your voice has {Article(accent)} {accent} accent. {Rules}" : null;

    /// <summary>A speaker brief with the accent brief after it, either of which may be missing.</summary>
    public static string? Join(string? speaker, string? accent) =>
        (speaker, accent) switch
        {
            (null, _) => accent,
            (_, null) => speaker,
            _ => $"{speaker} {accent}",
        };

    /// <summary>"a" or "an" before an accent's name.</summary>
    public static string Article(string accent) =>
        accent.Length > 0 && "AEIOUaeiou".Contains(accent[0], StringComparison.Ordinal) ? "an" : "a";
}
