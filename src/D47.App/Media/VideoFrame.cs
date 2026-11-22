namespace D47.App.Media;

/// <summary>
/// One decoded picture: <see cref="Height"/> rows of <see cref="Width"/> pixels, top-down, four bytes each in the order
/// blue, green, red and an unused byte. Its buffer is reused once two newer frames have been shown, so a reader copies
/// it before then.
/// </summary>
public sealed class VideoFrame(byte[] pixels, int width, int height, TimeSpan time)
{
    public byte[] Pixels { get; } = pixels;

    public int Width { get; } = width;

    public int Height { get; } = height;

    /// <summary>Where in the video it is shown from.</summary>
    public TimeSpan Time { get; } = time;
}

/// <summary>What a video playback is doing.</summary>
public enum VideoState
{
    /// <summary>The file or URL is being opened.</summary>
    Opening,

    Playing,

    Paused,

    /// <summary>Playing, and no sample has arrived for <see cref="VideoDecoder.StallAfter"/>.</summary>
    Stalled,

    /// <summary>The soundtrack played to its end.</summary>
    Ended,

    /// <summary>It could not be opened, decoded or read; <see cref="VideoPlayback.Failure"/> says which.</summary>
    Failed,
}

/// <summary>Why a video could not be played.</summary>
public enum VideoFailure
{
    /// <summary>The file or URL could not be opened, or stopped being readable partway.</summary>
    Unreachable,

    /// <summary>It has no picture, or Windows has no decoder for it.</summary>
    Picture,

    /// <summary>Windows has no decoder for its soundtrack.</summary>
    Soundtrack,
}

/// <summary>Why a video is paused.</summary>
public enum VideoPause
{
    None,

    /// <summary>The Commander paused it, or a story or another Media item started.</summary>
    Commander,

    /// <summary>Push-to-talk opened the microphone.</summary>
    PushToTalk,

    /// <summary>The Commander said stop or pressed Cancel.</summary>
    Interrupted,

    /// <summary>D47 is speaking an answer or an alert.</summary>
    Speaking,
}
