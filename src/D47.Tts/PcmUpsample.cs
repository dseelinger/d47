namespace D47.Tts;

/// <summary>
/// Sample-rate conversion for the one ratio every provider here needs.
/// <para>
/// Both providers deliver 24 kHz mono and the arbiter takes 48 kHz mono, which is an exact 2×
/// and therefore not a resampler so much as one pass with a midpoint. It lives on its own
/// because the second provider needed the identical conversion, and the alternative was
/// ElevenLabs reaching into a class named after Microsoft's wire format for its arithmetic.
/// </para>
/// </summary>
internal static class PcmUpsample
{
    /// <summary>
    /// 24 kHz to the arbiter's 48 kHz. Each output pair is the original sample and the midpoint
    /// to the next — linear interpolation, which for speech is inaudible against the
    /// alternative and costs one pass.
    /// </summary>
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
