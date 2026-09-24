using System.Buffers.Binary;
using System.Net;
using System.Text;
using D47.Core.Listening;

namespace D47.Stt;

/// <summary>What the hosted transcribers share: the audio they upload and how a refusal is classified.</summary>
internal static class HostedTranscription
{
    public static TranscriptionFailure Reason(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => TranscriptionFailure.KeyRejected,
        HttpStatusCode.TooManyRequests => TranscriptionFailure.RateLimited,
        _ => TranscriptionFailure.Failed,
    };

    /// <summary>The utterance as a 16-bit mono PCM WAV at its own sample rate.</summary>
    public static byte[] Wav(Utterance utterance)
    {
        const int HeaderBytes = 44;
        const short Channels = 1;
        const short BitsPerSample = 16;
        const short BlockAlign = Channels * BitsPerSample / 8;

        var dataBytes = utterance.Samples.Length * BlockAlign;
        var wav = new byte[HeaderBytes + dataBytes];
        var span = wav.AsSpan();

        Encoding.ASCII.GetBytes("RIFF", span[0..4]);
        BinaryPrimitives.WriteInt32LittleEndian(span[4..8], HeaderBytes - 8 + dataBytes);
        Encoding.ASCII.GetBytes("WAVE", span[8..12]);
        Encoding.ASCII.GetBytes("fmt ", span[12..16]);
        BinaryPrimitives.WriteInt32LittleEndian(span[16..20], 16);
        BinaryPrimitives.WriteInt16LittleEndian(span[20..22], 1);
        BinaryPrimitives.WriteInt16LittleEndian(span[22..24], Channels);
        BinaryPrimitives.WriteInt32LittleEndian(span[24..28], utterance.SampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(span[28..32], utterance.SampleRate * BlockAlign);
        BinaryPrimitives.WriteInt16LittleEndian(span[32..34], BlockAlign);
        BinaryPrimitives.WriteInt16LittleEndian(span[34..36], BitsPerSample);
        Encoding.ASCII.GetBytes("data", span[36..40]);
        BinaryPrimitives.WriteInt32LittleEndian(span[40..44], dataBytes);

        for (var i = 0; i < utterance.Samples.Length; i++)
        {
            var sample = (short)Math.Round(Math.Clamp(utterance.Samples[i], -1f, 1f) * short.MaxValue);
            BinaryPrimitives.WriteInt16LittleEndian(span.Slice(HeaderBytes + (i * 2), 2), sample);
        }

        return wav;
    }
}
