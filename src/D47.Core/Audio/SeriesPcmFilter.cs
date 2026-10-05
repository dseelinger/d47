namespace D47.Core.Audio;

/// <summary>Two running filters in series: everything the first returns is pushed into the second.</summary>
internal sealed class SeriesPcmFilter(IPcmFilter first, IPcmFilter second) : IPcmFilter
{
    public byte[] Push(ReadOnlySpan<byte> pcm) => second.Push(first.Push(pcm));

    public byte[] Finish()
    {
        var rest = second.Push(first.Finish());
        var tail = second.Finish();
        var pcm = new byte[rest.Length + tail.Length];

        rest.CopyTo(pcm, 0);
        tail.CopyTo(pcm, rest.Length);

        return pcm;
    }
}
