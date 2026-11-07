namespace D47.Core.Audio;

/// <summary>What the audio player and its mini read-outs say about the ambient music.</summary>
public sealed record AudioPlayerReadout(
    string StateWord,
    bool Playing,
    string Track,
    string Folder,
    string PlayLabel,
    bool Enabled,
    bool Muted)
{
    public const string Nothing = "—";

    /// <summary>The text shown for an unknown time.</summary>
    public const string UnknownClock = "–:––";

    /// <summary>A time as <c>m:ss</c>, minutes unpadded and not wrapped at an hour; <see cref="UnknownClock"/> for null.</summary>
    public static string Clock(TimeSpan? time) =>
        time is not { } t ? UnknownClock : $"{(int)Math.Max(0, t.TotalMinutes)}:{Math.Max(0, t.Seconds):00}";

    public static AudioPlayerReadout From(MusicState state, bool muted)
    {
        var paused = state.Paused || state.Idle;
        var enabled = !muted && (state.Playing || state.Paused || state.Idle);
        var word = muted ? "Muted" : paused ? "Paused" : state.Playing ? "Playing" : "Nothing playing";
        var track = enabled && state.Track is { Length: > 0 } name ? Path.GetFileName(name) : Nothing;
        var folder = enabled && state.Folder is { Length: > 0 } where ? where : Nothing;

        return new AudioPlayerReadout(word, !muted && !paused && state.Playing, track, folder, state.Playing && !paused ? "Pause" : "Play", enabled, muted);
    }
}
