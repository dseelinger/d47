using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Stories;

namespace D47.App.Voice;

/// <summary>Chooses the voice and picture of an addressed speaker, and the cast behind each announcement slot.</summary>
internal sealed class AddressedVoices(
    SettingsService settings,
    SpeechClients speech,
    VoicePipeline pipeline,
    GameStateStore gameState,
    Func<D47.Core.Interface.SpeakerPictures> pictures,
    Func<StoryDirector?> stories,
    Func<LineWriter> writer)
{
    private string Flying => gameState.Active?.Identity.FrontierId ?? string.Empty;

    /// <summary>The cast belonging to whoever speaks for an announcement's slot.</summary>
    public VoiceCast CastFor(Announcement announcement) => CastFor(announcement, settings.Current.Speech, speech.Casting.Of);

    /// <summary>The cast <paramref name="casting"/> gives the provider that speaks for an announcement's slot.</summary>
    internal static VoiceCast CastFor(Announcement announcement, SpeechSettings speech, Func<string, VoiceCast> casting) =>
        casting(VoiceGroups.ProviderFor(speech, VoiceGroups.Of(announcement.Voice, announcement.CommsChannel)));

    /// <summary>The accent of the voice an incoming message resolves to, assigning its sender that voice on first use.</summary>
    public string? AccentHeardAs(Announcement heardAs) => AccentHeardAs(heardAs, CastFor);

    /// <inheritdoc cref="AccentHeardAs(Announcement)"/>
    internal static string? AccentHeardAs(Announcement heardAs, Func<Announcement, VoiceCast> castFor)
    {
        ArgumentNullException.ThrowIfNull(castFor);

        var cast = castFor(heardAs);
        return cast.AccentOf(HeardVoiceOf(heardAs, _ => cast).Voice.VoiceId);
    }

    /// <summary>The voice of a turn answered by an addressed speaker. Disposing restores the ship AI's.</summary>
    public sealed class AddressedVoice(VoicePipeline pipeline, VoiceSelection voice, string? captionSpeaker) : IDisposable
    {
        public void Dispose()
        {
            pipeline.Voice = voice;
            pipeline.Pinned = null;
            pipeline.SpeakingLanguage = null;
            pipeline.Picture = null;
            pipeline.CaptionSpeaker = captionSpeaker;
            pipeline.SpeakingAs = VoiceRole.ShipAi;
        }
    }

    /// <summary>Speaks the rest of this turn as the speaker it was addressed to, captioned with their name.</summary>
    public AddressedVoice SpeakAs(TurnEvent.Addressed addressed)
    {
        var restore = new AddressedVoice(pipeline, pipeline.Voice, pipeline.CaptionSpeaker);

        // A story's cast member speaks in the voice the story gave them, as their lines are spoken.
        if (CastVoice(addressed) is { Pinned: { } pinned } cast)
        {
            var line = new Announcement(StoryLines.KeyPrefix, string.Empty)
            {
                Voice = VoiceRole.Crew,
                Speaker = addressed.Name,
                Pinned = pinned,
            };

            pipeline.Voice = SpeakerAccent.VoiceOf(CastFor(line), line);
            pipeline.Pinned = pinned;
            pipeline.SpeakingLanguage = cast.Language;
            pipeline.Picture = pictures().For(cast.Cast);
            pipeline.SpeakingAs = VoiceRole.Crew;
            pipeline.CaptionSpeaker = addressed.Name;

            return restore;
        }

        // A comms NPC answers in the voice, and from the voice group, their incoming message is heard in.
        if (addressed.HeardAs is { } heardAs)
        {
            (pipeline.Voice, pipeline.SpeakingAs) = HeardVoiceOf(heardAs, CastFor);
            pipeline.Picture = AddressedPicture(addressed);
            pipeline.CaptionSpeaker = addressed.Name;

            return restore;
        }

        // An invented speaker keeps the voice their exchange was heard in.
        var provider = VoiceGroups.ProviderFor(settings.Current.Speech, VoiceGroups.Of(addressed.Role));

        pipeline.Voice = addressed.Role switch
        {
            VoiceRole.Comms => AddressedVoiceOf(addressed, writer().NpcCast, provider),
            VoiceRole.Narrator => CastFor(new Announcement(NarratorCallout.KeyPrefix, string.Empty) { Voice = VoiceRole.Narrator })
                .For(VoiceRole.Narrator),
            _ => AddressedVoiceOf(addressed, speech.Cast, provider),
        };
        pipeline.Picture = AddressedPicture(addressed);
        pipeline.SpeakingAs = addressed.Role;
        pipeline.CaptionSpeaker = addressed.Name;

        return restore;
    }

    /// <summary>
    /// The voice a reply is spoken in and the role whose voice group synthesises it: those <see cref="Announcer"/>
    /// gives the incoming message <paramref name="heardAs"/>.
    /// </summary>
    internal static (VoiceSelection Voice, VoiceRole SpeakingAs) HeardVoiceOf(Announcement heardAs, Func<Announcement, VoiceCast> castFor)
    {
        ArgumentNullException.ThrowIfNull(heardAs);
        ArgumentNullException.ThrowIfNull(castFor);

        return (SpeakerAccent.VoiceOf(castFor(heardAs), heardAs), heardAs.Voice);
    }

    /// <summary>
    /// The voice an addressed speaker is heard in from <paramref name="cast"/>: their own when <paramref name="provider"/>,
    /// which speaks their role, issued it and the cast lists it; otherwise their role's.
    /// </summary>
    internal static VoiceSelection AddressedVoiceOf(TurnEvent.Addressed addressed, VoiceCast cast, string provider)
    {
        ArgumentNullException.ThrowIfNull(addressed);
        ArgumentNullException.ThrowIfNull(cast);

        var own = addressed.Voice is { } voice && string.Equals(voice.Provider, provider, StringComparison.OrdinalIgnoreCase)
            ? voice.VoiceId
            : null;

        return cast.ForSender(addressed.Name, isPlayer: false, addressed.Role, own: own);
    }

    /// <summary>The gender of the voice <paramref name="role"/> is speaking in, by its provider's listing.</summary>
    public VoiceGender RoleGender(VoiceRole role)
    {
        var cast = CastFor(new Announcement(string.Empty, string.Empty) { Voice = role });
        return cast.GenderOf(cast.For(role).VoiceId);
    }

    /// <summary>The picture name the Conversation page shows an addressed turn's reply with, or null.</summary>
    public string? AddressedPicture(TurnEvent.Addressed addressed) => CastVoice(addressed) is { Cast: { } cast }
        ? pictures().For(cast)
        : addressed.Picture ?? addressed.Role switch
    {
        VoiceRole.CarrierCaptain or VoiceRole.TowerControl =>
            D47.Core.Interface.SpeakerPictures.ByVoice(addressed.Role, RoleGender(addressed.Role), Flying),
        VoiceRole.Crew => Announcer.CrewPicture(addressed.Name, gameState.Active?.Crew),
        VoiceRole.Narrator => D47.Core.Interface.SpeakerPictures.Narrator,
        _ => null,
    };

    /// <summary>The picture name of the captain or tower in the voice now set for <paramref name="role"/>, for the flying Commander, or null.</summary>
    public string? SpeakerPicture(VoiceRole role) =>
        D47.Core.Interface.SpeakerPictures.ByVoice(role, RoleGender(role), Flying);

    /// <summary>
    /// Who speaks a story run's recorded line now: a cast member in the voice they speak in today, the Narrator in the
    /// Narrator's voice, and anyone else in the ship's. Null for a cast member no story on disk has.
    /// </summary>
    public StoryLineSpeech? LineSpeech(StoryRunLine line)
    {
        ArgumentNullException.ThrowIfNull(line);

        if (line.Cast is { } key)
        {
            if (stories()?.CastMember(key) is not { } member)
            {
                return null;
            }

            var spoken = new Announcement(StoryLines.KeyPrefix, string.Empty)
            {
                Voice = VoiceRole.Crew,
                Speaker = member.Shown.Name,
                Pinned = member.Speaks,
            };

            return new StoryLineSpeech(VoiceRole.Crew, SpeakerAccent.VoiceOf(CastFor(spoken), spoken) with { Language = member.Language }, member.Speaks);
        }

        var role = line.From == D47.Core.Messages.MessageStore.Narrator ? VoiceRole.Narrator : VoiceRole.ShipAi;
        var said = new Announcement(role == VoiceRole.Narrator ? NarratorCallout.KeyPrefix : string.Empty, string.Empty) { Voice = role };

        return new StoryLineSpeech(role, CastFor(said).For(role));
    }

    /// <summary>The voice of the running story's cast member answering a turn, or null for any other speaker.</summary>
    private StoryLineVoice? CastVoice(TurnEvent.Addressed addressed) =>
        addressed.Cast is { } castId && stories()?.LineVoice(gameState.Active?.Identity.FrontierId, castId) is { Cast: not null } cast
            ? cast
            : null;
}
