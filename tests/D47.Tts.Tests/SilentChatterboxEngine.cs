using D47.Core.Audio;

namespace D47.Tts.Tests;

/// <summary>An engine that speaks silence; <paramref name="samples"/> defaults to a tenth of a second.</summary>
internal sealed class SilentChatterboxEngine(int? samples = null) : IChatterboxEngine
{
    public IDisposable Encode(float[] reference) => new Handle();

    public float[] Speak(long[] textIds, IDisposable voice, CancellationToken cancellationToken) =>
        new float[samples ?? ChatterboxPipeline.SampleRate / 10];

    public void Dispose()
    {
    }

    private sealed class Handle : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
