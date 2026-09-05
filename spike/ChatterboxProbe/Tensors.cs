using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace ChatterboxProbe;

/// <summary>
/// The dtype plumbing, which is the part of the port that is fiddly rather than hard.
/// <para>
/// The published builds mix precisions across the four graphs — Nano's speech encoder hands out
/// fp16 speaker features and its decoder wants fp32 — so every value crossing a graph boundary has
/// to be checked against the receiving graph's declared type rather than assumed. The KV cache is
/// the exception and is never read: it goes back in as the <see cref="OrtValue"/> that came out,
/// which is what keeps a 24-layer cache off the managed heap on every one of a few hundred steps.
/// </para>
/// </summary>
internal static class Tensors
{
    public static OrtValue Make(long[] data, long[] shape) =>
        OrtValue.CreateTensorValueFromMemory(data, shape);

    public static OrtValue Make(float[] data, long[] shape, TensorElementType type)
    {
        if (type == TensorElementType.Float)
        {
            return OrtValue.CreateTensorValueFromMemory(data, shape);
        }

        if (type != TensorElementType.Float16)
        {
            throw new NotSupportedException($"{type} is not a float tensor this probe writes.");
        }

        var value = OrtValue.CreateAllocatedTensorValue(
            OrtAllocator.DefaultInstance, TensorElementType.Float16, shape);
        var span = value.GetTensorMutableDataAsSpan<Float16>();

        for (var i = 0; i < data.Length; i++)
        {
            span[i] = (Float16)data[i];
        }

        return value;
    }

    public static float[] ReadFloats(OrtValue value)
    {
        var type = value.GetTensorTypeAndShape().ElementDataType;

        if (type == TensorElementType.Float)
        {
            return value.GetTensorDataAsSpan<float>().ToArray();
        }

        if (type != TensorElementType.Float16)
        {
            throw new NotSupportedException($"{type} is not a float tensor this probe reads.");
        }

        var half = value.GetTensorDataAsSpan<Float16>();
        var floats = new float[half.Length];

        for (var i = 0; i < half.Length; i++)
        {
            floats[i] = (float)half[i];
        }

        return floats;
    }

    public static long[] ReadLongs(OrtValue value) => value.GetTensorDataAsSpan<long>().ToArray();

    /// <summary>The value itself when it is already the right type, and a converted copy otherwise.</summary>
    public static OrtValue Cast(OrtValue value, TensorElementType type)
    {
        if (value.GetTensorTypeAndShape().ElementDataType == type)
        {
            // Handed straight back and deliberately not owned by the caller's `using`: OrtValue's
            // Dispose is idempotent, so the double release is harmless and the alternative is a
            // copy of the speaker features on every line.
            return value;
        }

        return Make(ReadFloats(value), value.GetTensorTypeAndShape().Shape, type);
    }

    /// <summary>
    /// Joins the reference clip's conditioning embedding onto the front of the text embedding, along
    /// the sequence axis. This is the only place the two halves of the model meet.
    /// </summary>
    public static OrtValue Concatenate(OrtValue first, OrtValue second, TensorElementType type)
    {
        var left = first.GetTensorTypeAndShape().Shape;
        var right = second.GetTensorTypeAndShape().Shape;

        if (left[2] != right[2])
        {
            throw new InvalidOperationException(
                $"conditioning is {left[2]} wide and the text embedding is {right[2]}.");
        }

        var joined = new float[(left[1] + right[1]) * left[2]];
        var a = ReadFloats(first);
        var b = ReadFloats(second);

        a.CopyTo(joined, 0);
        b.CopyTo(joined, a.Length);

        return Make(joined, [1, left[1] + right[1], left[2]], type);
    }
}
