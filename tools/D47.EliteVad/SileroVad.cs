using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace D47.EliteVad;

/// <summary>
/// Silero VAD v5 at 16 kHz: 512-sample chunks, each prefixed with the last 64 samples of the one before,
/// with the recurrent state carried from call to call.
/// </summary>
internal sealed class SileroVad : IDisposable
{
    public const int ChunkSamples = 512;
    private const int ContextSamples = 64;

    private readonly InferenceSession _session;
    private readonly float[] _input = new float[ContextSamples + ChunkSamples];
    private float[] _state = new float[2 * 128];
    private readonly DenseTensor<long> _sampleRate = new(new[] { 16000L }, Array.Empty<int>());

    public SileroVad(string modelPath)
    {
        var options = new SessionOptions { IntraOpNumThreads = 1, InterOpNumThreads = 1 };
        _session = new InferenceSession(modelPath, options);
    }

    /// <summary>Speech probability for the next 512 samples.</summary>
    public float Probability(ReadOnlySpan<float> chunk)
    {
        chunk.CopyTo(_input.AsSpan(ContextSamples));
        var inputs = new[]
        {
            NamedOnnxValue.CreateFromTensor("input", new DenseTensor<float>(_input, new[] { 1, _input.Length })),
            NamedOnnxValue.CreateFromTensor("state", new DenseTensor<float>(_state, new[] { 2, 1, 128 })),
            NamedOnnxValue.CreateFromTensor("sr", _sampleRate),
        };

        using var results = _session.Run(inputs);
        var probability = results[0].AsTensor<float>().GetValue(0);
        _state = results[1].AsTensor<float>().ToArray();
        _input.AsSpan(_input.Length - ContextSamples).CopyTo(_input);
        return probability;
    }

    public void Dispose() => _session.Dispose();
}
