namespace D47.Tts;

/// <summary>Sample-rate conversion for the one ratio every provider here needs.</summary>
internal static class PcmUpsample
{
    /// <summary>24 kHz to the arbiter's 48 kHz.</summary>
    public static byte[] Double(ReadOnlySpan<byte> pcm)
    {
        var samples = pcm.Length / 2;
        var output = new byte[samples * 4];

        for (var i = 0; i < samples; i++)
        {
            var current = (short)(pcm[i * 2] | (pcm[(i * 2) + 1] << 8));
            var next = i + 1 < samples
                ? (short)(pcm[(i + 1) * 2] | (pcm[((i + 1) * 2) + 1] << 8))
                : current;

            Write(output.AsSpan(i * 4), current, next);
        }

        return output;
    }

    /// <summary>One input sample and the midpoint after it.</summary>
    internal static void Write(Span<byte> into, short current, short next)
    {
        var midpoint = (short)((current + next) / 2);

        into[0] = (byte)current;
        into[1] = (byte)(current >> 8);
        into[2] = (byte)midpoint;
        into[3] = (byte)(midpoint >> 8);
    }
}

/// <summary>
/// <see cref="PcmUpsample.Double"/> over a body that arrives in chunks: the same bytes, one sample behind,
/// since each sample's midpoint needs the sample after it.
/// </summary>
internal sealed class PcmUpsampler
{
    private short? _held;
    private byte? _odd;

    /// <summary>The output for every sample whose successor has now arrived.</summary>
    public byte[] Push(ReadOnlySpan<byte> pcm)
    {
        var total = (_odd.HasValue ? 1 : 0) + pcm.Length;
        var samples = total / 2;
        var output = new byte[(samples - (_held.HasValue ? 0 : Math.Min(samples, 1))) * 4];
        var written = 0;
        var at = 0;

        for (var i = 0; i < samples; i++)
        {
            short sample;

            if (i == 0 && _odd is { } low)
            {
                sample = (short)(low | (pcm[0] << 8));
                at = 1;
                _odd = null;
            }
            else
            {
                sample = (short)(pcm[at] | (pcm[at + 1] << 8));
                at += 2;
            }

            if (_held is { } held)
            {
                PcmUpsample.Write(output.AsSpan(written), held, sample);
                written += 4;
            }

            _held = sample;
        }

        if (at < pcm.Length)
        {
            _odd = pcm[at];
        }

        return output;
    }

    /// <summary>The last sample, which has no successor; a trailing odd byte is dropped.</summary>
    public byte[] Finish()
    {
        if (_held is not { } held)
        {
            return [];
        }

        var output = new byte[4];
        PcmUpsample.Write(output, held, held);
        _held = null;
        _odd = null;
        return output;
    }
}
