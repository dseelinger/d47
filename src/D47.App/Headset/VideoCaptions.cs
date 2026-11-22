using D47.App.Panel;
using D47.Core.Vr;

namespace D47.App.Headset;

/// <summary>Gives the caption band the subtitles of the video the headset panel draws, at the video's position.</summary>
public sealed class VideoCaptions(CaptionLayer layer)
{
    private IReadOnlyList<SubtitleCue>? _followed;

    /// <summary>Called on every headset tick, on the UI thread.</summary>
    /// <param name="drawn">Whether the headset panel draws the video's picture.</param>
    public void Serve(WatchHost? watch, bool drawn)
    {
        var cues = drawn && watch is { CaptionsOn: true, Loaded: true } ? watch.Cues : null;

        if (!ReferenceEquals(cues, _followed))
        {
            _followed = cues;
            layer.Follow(cues);
        }

        if (cues is not null)
        {
            layer.At(watch!.Video.Position);
        }
    }
}
