namespace D47.Core.Listening;

/// <summary>
/// Anything that takes captured microphone audio: mono float PCM, already at the capture rate.
/// <para>
/// It exists so that the thing between the microphone and the gate can be swapped without the
/// microphone knowing there is one. Phase 13 puts acoustic echo cancellation there, and the
/// device end of the path is unchanged by it — <see cref="ListenGate"/> is one of these, the
/// canceller is another that forwards to the gate, and the microphone writes to whichever it
/// was handed.
/// </para>
/// <para>
/// Declared in Core rather than beside the WASAPI capture for the usual reason: everything
/// above the device is gate policy and has to be testable with a WAV file and no hardware
/// (architecture.md §8).
/// </para>
/// </summary>
public interface ICaptureSink
{
    /// <summary>
    /// Feeds captured audio. Called from the audio thread, continuously, whether or not anyone
    /// downstream is currently interested.
    /// </summary>
    void Write(ReadOnlySpan<float> samples);

    /// <summary>
    /// Drops anything in flight, because the device went away or changed. Whatever was
    /// half-captured is no longer a thing the Commander finished saying, and whatever adaptive
    /// state was tracking that device describes a room that is no longer the one being heard.
    /// </summary>
    void Reset();
}
