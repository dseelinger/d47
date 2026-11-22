using D47.Core.Audio;
using D47.Core.Configuration;

namespace D47.App.Panel;

/// <summary>What Media › Music reads and does, shared by the window and the headset.</summary>
public sealed class MusicHost(
    SettingsService settings,
    Func<MusicAction, string> control,
    Action thumbsDown,
    Func<TrackProgress?> progress,
    Func<TimeSpan, bool> seek)
{
    public SettingsService Settings { get; } = settings;

    /// <summary>Pauses, resumes or skips the ambient music and returns the reply.</summary>
    public string Control(MusicAction action) => control(action);

    /// <summary>Thumbs down the playing track and says the reply.</summary>
    public void ThumbsDown() => thumbsDown();

    /// <summary>How far through the playing track the music is, or null while nothing plays.</summary>
    public TrackProgress? Progress() => progress();

    /// <summary>Moves the playing track to this position; false when it cannot.</summary>
    public bool Seek(TimeSpan to) => seek(to);
}
