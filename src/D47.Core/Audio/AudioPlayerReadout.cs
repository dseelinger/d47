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
