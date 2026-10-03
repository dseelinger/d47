using D47.Core.Audio;
using D47.Core.Messages;
using D47.Core.Speech;

namespace D47.Core.Stories;

/// <summary>
/// Who speaks one story line: the role it is spoken as, who it is posted from, the cast member when it is one, and the
/// voice pinned to that member.
/// </summary>
public sealed record StoryLineVoice(VoiceRole Role, string From, StorySpeakerShown? Cast = null, PinnedVoice? Pinned = null)
{
    /// <summary>The cast member's line for the writer: who they are and how they speak.</summary>
    public string? Who { get; init; }

    /// <summary>Whether the narrator speaks it, for the ship's lines while a stock core is aboard as well.</summary>
    public bool Narrated => Role == VoiceRole.Narrator;
}

/// <summary>What is on this PC for a story's cast to speak with.</summary>
public sealed record CastVoicesHere(bool Kokoro, bool Chatterbox, bool OwnRecording)
{
    /// <summary>Everything installed and recorded.</summary>
    public static CastVoicesHere All { get; } = new(true, true, true);
}

/// <summary>Who speaks each story line, and what a story's cast needs on this PC before it can speak.</summary>
public static class StoryVoices
{
    /// <summary>
    /// The voice of a line by <paramref name="speaker"/>. The ship is the core aboard, or the narrator while that core is
    /// stock; a cast member speaks in its pinned voice, the version a Commander of <paramref name="gender"/> meets. A
    /// speaker the story does not cast is the ship's.
    /// </summary>
    public static StoryLineVoice Of(string? speaker, StorySecret secret, string? gender, Persona.Persona? core)
    {
        ArgumentNullException.ThrowIfNull(secret);

        if (speaker == StorySpeaker.Narrator || (core?.Stock == true && IsShip(speaker, secret)))
        {
            return new StoryLineVoice(VoiceRole.Narrator, MessageStore.Narrator);
        }

        if (Member(secret, speaker) is not { } member)
        {
            return new StoryLineVoice(VoiceRole.ShipAi, (core ?? Persona.PersonaCatalog.Covas).Id);
        }

        var shown = member.Shown(secret.Id, gender);

        return new StoryLineVoice(
            VoiceRole.Crew,
            shown.Name,
            shown,
            new PinnedVoice(shown.Provider, shown.Voice) { Link = member.Link, Effects = member.Effects })
        {
            Who = member.Who,
        };
    }

    /// <summary>Whether a line by <paramref name="speaker"/> is the ship's: named so, or naming nobody the story casts.</summary>
    public static bool IsShip(string? speaker, StorySecret secret)
    {
        ArgumentNullException.ThrowIfNull(secret);

        return speaker != StorySpeaker.Narrator && Member(secret, speaker) is null;
    }

    /// <summary>
    /// What the cast of <paramref name="secret"/>, as a Commander of <paramref name="gender"/> meets it, needs and does
    /// not have here, one sentence each; empty when every voice is ready.
    /// </summary>
    public static IReadOnlyList<string> Missing(StorySecret secret, string? gender, CastVoicesHere here)
    {
        ArgumentNullException.ThrowIfNull(secret);
        ArgumentNullException.ThrowIfNull(here);

        var voices = secret.Cast.Select(member => member.Shown(secret.Id, gender)).ToList();
        var missing = new List<string>();

        if (!here.Chatterbox && voices.Any(voice => voice.Provider == StorySpeaker.Chatterbox))
        {
            missing.Add($"Download Chatterbox, about {ChatterboxAssets.TotalMegabytes:0} MB, under Settings, Its voice.");
        }

        if (!here.Kokoro && voices.Any(voice => voice.Provider == StorySpeaker.Kokoro))
        {
            missing.Add($"Download the Kokoro local voice, about {KokoroAssets.TotalMegabytes:0} MB, under Settings, Its voice.");
        }

        if (!here.OwnRecording && voices.Any(voice => voice.Provider == StorySpeaker.Chatterbox && voice.Voice == StorySpeaker.Own))
        {
            missing.Add("Record your voice under Settings, Your voice.");
        }

        return missing;
    }

    /// <summary>The ship's message when a story cannot be picked until its voices are ready.</summary>
    public static string CannotStart(string title, IReadOnlyList<string> missing) =>
        $"{title} cannot start until its voices are ready. {string.Join(" ", missing)}";

    /// <summary>The ship's message when a running story is paused because a voice it needs is gone.</summary>
    public static string Paused(string title, IReadOnlyList<string> missing) =>
        $"{title} is paused because a voice it needs is not ready. {string.Join(" ", missing)} Then resume it on the Stories page.";

    /// <summary>Whether a story line by any member of the cast would be spoken by the given provider.</summary>
    public static bool Uses(StorySecret secret, string? gender, string providerId)
    {
        ArgumentNullException.ThrowIfNull(secret);

        return secret.Cast.Any(member => member.Shown(secret.Id, gender).Provider == providerId);
    }

    private static StorySpeaker? Member(StorySecret secret, string? speaker) =>
        speaker is null ? null : secret.Cast.FirstOrDefault(member => string.Equals(member.Id, speaker, StringComparison.Ordinal));
}
