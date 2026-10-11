using D47.App.Voice;
using D47.Core.Audio;
using D47.Core.Capabilities;

namespace D47.App.Settings;

/// <summary>What the Custom voices page reads and drives.</summary>
public sealed record CustomVoicesEditing(
    CustomVoices Store,
    OwnVoiceRecording Own,
    CustomVoiceDraft Draft,
    Action<AudioClip> Play,
    Func<bool> ChatterboxInstalled,
    LongPress DownloadChatterbox)
{
    /// <summary>The settings row's line: whether Your voice is recorded, and how many other voices are kept.</summary>
    public string Summary()
    {
        var count = Store.List().Count;
        var others = count switch
        {
            0 => "No other custom voices.",
            1 => "1 other custom voice.",
            _ => $"{count} other custom voices.",
        };

        return Own.Recorded
            ? $"Your voice is recorded. {others}"
            : $"Your voice is not recorded. {others}";
    }
}
