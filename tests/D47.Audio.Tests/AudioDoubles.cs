using D47.Core.Audio;
using D47.Core.Listening;
using NAudio.Wave;

namespace D47.Audio.Tests;

/// <summary>
/// The downstream end of the capture path, remembering what it was handed.
/// <para>
/// Buffers are copied rather than kept by reference: every caller in the real path writes into
/// a reused array, so a double that stored the span's backing store would report the last frame
/// several times over and call it a pass.
/// </para>
/// </summary>
internal sealed class RecordingCaptureSink : ICaptureSink
{
    private readonly List<float[]> _writes = [];

    public IReadOnlyList<float[]> Writes => _writes;

    public int Resets { get; private set; }

    /// <summary>Every sample handed downstream, in order, with the framing flattened out.</summary>
    public float[] All => [.. _writes.SelectMany(write => write)];

    public void Write(ReadOnlySpan<float> samples) => _writes.Add(samples.ToArray());

    public void Reset() => Resets++;
}

/// <summary>A render reference tap nothing is rendering into, so a test can raise it by hand.</summary>
internal sealed class FakeRenderTap : IRenderReferenceTap
{
    public event Action<RenderReferenceFrame>? Rendered;

    /// <summary>Whether anything is listening. This is what <c>Start</c> and <c>Stop</c> change.</summary>
    public bool Subscribed => Rendered is not null;

    public void Raise(RenderReferenceFrame frame) => Rendered?.Invoke(frame);
}

/// <summary>A sample source answering from a fixed buffer, so the tap has something to read.</summary>
internal sealed class ArraySampleProvider(WaveFormat format, params float[] samples) : ISampleProvider
{
    private int _position;

    public WaveFormat WaveFormat { get; } = format;

    public int Read(float[] buffer, int offset, int count)
    {
        var take = Math.Min(count, samples.Length - _position);

        for (var i = 0; i < take; i++)
        {
            buffer[offset + i] = samples[_position + i];
        }

        _position += take;
        return take;
    }
}
