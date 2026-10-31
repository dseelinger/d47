using D47.Core.Audio;

namespace D47.Core.Tests.Audio;

/// <summary>Feeds PCM to a running filter whole or in random cuts.</summary>
internal static class PcmChunking
{
    /// <summary>Chunk sizes of 1 to 4,999 bytes, odd sizes included, so samples are split across pushes.</summary>
    public static IEnumerable<int> RandomCuts(int length, int seed)
    {
        var random = new Random(seed);

        for (var at = 0; at < length;)
        {
            var size = Math.Min(length - at, random.Next(1, 5_000));
            yield return size;
            at += size;
        }
    }

    public static byte[] Whole(IPcmFilter filter, ReadOnlySpan<byte> pcm) => [.. filter.Push(pcm), .. filter.Finish()];

    public static byte[] Chunked(IPcmFilter filter, byte[] pcm, int seed)
    {
        var output = new List<byte>();
        var at = 0;

        foreach (var size in RandomCuts(pcm.Length, seed))
        {
            output.AddRange(filter.Push(pcm.AsSpan(at, size)));
            at += size;
        }

        output.AddRange(filter.Finish());
        return [.. output];
    }

    public static void AppendInCuts(ArrivingClip source, byte[] pcm, int seed)
    {
        var at = 0;

        foreach (var size in RandomCuts(pcm.Length, seed))
        {
            source.Append(pcm.AsSpan(at, size));
            at += size;
        }
    }
}
