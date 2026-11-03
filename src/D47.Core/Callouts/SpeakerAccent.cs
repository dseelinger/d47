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
        "Write in English. One word from the accent's own language is allowed; a sentence is not. "
        + "Let the words suit that accent through regional word choice, idiom and rhythm. It can be "
        + "funny, but never spell the accent out phonetically, never lean on a stereotype, and never "
        + "make the dialect the joke.";

    /// <summary>The voice an announcement is spoken in, assigning a sender theirs on first use.</summary>
    public static VoiceSelection VoiceOf(VoiceCast cast, Announcement announcement)
    {
        ArgumentNullException.ThrowIfNull(cast);
        ArgumentNullException.ThrowIfNull(announcement);

        return announcement.Speaker is { Length: > 0 } speaker && !announcement.InRoleVoice
            ? cast.ForSender(speaker, announcement.SpeakerIsPlayer, announcement.Voice, announcement.SpeakerAllegiance)
            : cast.For(announcement.Voice);
    }

    /// <summary>
    /// What a first line from a named NPC waits for before it is cast: the sex of the sender's given name. Null
    /// where it need not wait.
    /// </summary>
    public static Task? NameAnswerFor(VoiceCast cast, Announcement announcement) =>
        announcement.Speaker is { Length: > 0 } speaker && !announcement.InRoleVoice
            ? cast.SexAnswerFor(speaker, announcement.SpeakerIsPlayer, announcement.Voice)
            : null;

    /// <summary>Splits a batch into the lines to speak now and the first lines from NPCs that wait, each with its answer.</summary>
    public static (List<Announcement> Ready, List<(Announcement Line, Task Answer)> Held) SplitByNameWait(
        IEnumerable<Announcement> batch,
        Func<Announcement, VoiceCast> castFor)
    {
        var ready = new List<Announcement>();
        var held = new List<(Announcement, Task)>();

        foreach (var announcement in batch)
        {
            if (NameAnswerFor(castFor(announcement), announcement) is { } answer)
            {
                held.Add((announcement, answer));
            }
            else
            {
                ready.Add(announcement);
            }
        }

        return (ready, held);
    }

    /// <summary>Completes when <paramref name="answer"/> does or the ceiling passes, whichever is first.</summary>
    public static async Task WaitForNameAsync(Task answer, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(answer);

        using var ceiling = new CancellationTokenSource();
        var elapsed = Task.Delay(NameAccents.AnswerCeiling, time ?? TimeProvider.System, ceiling.Token);

        await Task.WhenAny(answer, elapsed).ConfigureAwait(false);
        await ceiling.CancelAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// The accent brief for the voice that will speak an NPC's line, or null for a core, the crew, a
    /// voice whose listing names no accent, or a line whose roll against <paramref name="percent"/> misses.
    /// </summary>
    public static string? For(VoiceCast cast, Announcement announcement, AccentRoll roll, int percent)
    {
        ArgumentNullException.ThrowIfNull(announcement);
        ArgumentNullException.ThrowIfNull(roll);

        return announcement.Voice is VoiceRole.Comms or VoiceRole.CarrierCaptain or VoiceRole.TowerControl
            && Sentence(cast.AccentOf(VoiceOf(cast, announcement).VoiceId)) is { } sentence
            && roll.Hits(percent)
                ? sentence
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
