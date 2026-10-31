namespace D47.Core.Tests.Audio;

/// <summary>A silent 16-bit PCM RIFF file, written by hand so a test does not read back what the code under test wrote.</summary>
internal static class TestWav
{
    public static byte[] Bytes(int samples = 240, int sampleRate = 48_000, short channels = 1, short bits = 16)
    {
        var data = samples * channels * (bits / 8);

        using var stream = new MemoryStream();
        using var write = new BinaryWriter(stream);

        write.Write("RIFF"u8.ToArray());
        write.Write(36 + data);
        write.Write("WAVE"u8.ToArray());
        write.Write("fmt "u8.ToArray());
        write.Write(16);
        write.Write((short)1);
        write.Write(channels);
        write.Write(sampleRate);
        write.Write(sampleRate * channels * (bits / 8));
        write.Write((short)(channels * (bits / 8)));
        write.Write(bits);
        write.Write("data"u8.ToArray());
        write.Write(data);
        write.Write(new byte[data]);
        write.Flush();

        return stream.ToArray();
    }
}
