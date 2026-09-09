using D47.Core.Audio;
using D47.Core.Listening;
using NAudio.Wave;

namespace D47.Audio.Tests;

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

internal sealed class FakeRenderTap : IRenderReferenceTap
{
    public event Action<RenderReferenceFrame>? Rendered;

    public bool Subscribed => Rendered is not null;

    public void Raise(RenderReferenceFrame frame) => Rendered?.Invoke(frame);
}

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
