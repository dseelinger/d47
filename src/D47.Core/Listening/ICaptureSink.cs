namespace D47.Core.Listening;

/// <summary>Anything that takes captured microphone audio: mono float PCM, already at the capture rate.</summary>
public interface ICaptureSink
{
    /// <summary>Feeds captured audio.</summary>
    void Write(ReadOnlySpan<float> samples);

    /// <summary>Drops anything in flight, because the device went away or changed.</summary>
    void Reset();
}
