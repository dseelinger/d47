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

            var midpoint = (short)((current + next) / 2);

            output[i * 4] = (byte)current;
            output[(i * 4) + 1] = (byte)(current >> 8);
            output[(i * 4) + 2] = (byte)midpoint;
            output[(i * 4) + 3] = (byte)(midpoint >> 8);
        }

        return output;
    }
}
