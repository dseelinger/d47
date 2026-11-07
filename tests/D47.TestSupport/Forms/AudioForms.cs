using D47.Core.Audio;

namespace D47.Core.Audio;

public static class AudioForms
{
    extension(CueLibrary)
    {
        public static CueLibrary Load() => CueLibrary.Load(new EmbeddedCueSource(typeof(CueLibrary).Assembly));

        public static CueLibrary Load(ICueSource source) => CueLibrary.Load(logger: null, source);
    }

    extension(RadioVoice)
    {
        public static Func<AudioClip, AudioClip>? Colours(VoiceRole role) => RadioVoice.Colours(role, 1, overheard: false);

        public static AudioClip Apply(AudioClip clip) => RadioVoice.Apply(clip, 1);
    }
}
