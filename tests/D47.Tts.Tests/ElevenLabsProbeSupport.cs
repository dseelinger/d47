using System.Text.Json;

namespace D47.Tts.Tests;

/// <summary>Helpers shared by the ElevenLabs probe tests.</summary>
internal static class ElevenLabsProbeSupport
{
    /// <summary>ElevenLabs' <c>detail.message</c>, or each <c>msg</c> of a validation error list.</summary>
    public static string? DetailOf(byte[] body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);

            if (!document.RootElement.TryGetProperty("detail", out var detail))
            {
                return null;
            }

            return detail.ValueKind switch
            {
                JsonValueKind.Object when detail.TryGetProperty("message", out var message) => message.GetString(),
                JsonValueKind.String => detail.GetString(),
                JsonValueKind.Array => string.Join("; ", detail.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.Object && item.TryGetProperty("msg", out _))
                    .Select(item => item.GetProperty("msg").GetString())),
                _ => null,
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static byte[] Wav(byte[] pcm, int sampleRate, int channels = 1)
    {
        using var stream = new MemoryStream(44 + pcm.Length);
        using var writer = new BinaryWriter(stream);

        writer.Write("RIFF"u8);
        writer.Write(36 + pcm.Length);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2 * channels);
        writer.Write((short)(2 * channels));
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(pcm.Length);
        writer.Write(pcm);
        writer.Flush();

        return stream.ToArray();
    }
}
