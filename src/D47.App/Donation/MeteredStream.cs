namespace D47.App.Donation;

/// <summary>
/// Counts bytes on their way out to the wire, and says so while they go
/// (<a href="https://github.com/dseelinger/d47/issues/212">#212</a>).
/// <para>
/// <b>The read-side twin of <see cref="TallyStream"/>, and it exists for the same reason in the
/// other direction.</b> A corpus donation is up to 356 MB read from a spool and handed to one
/// POST; before this, that step reported itself once, before the request began, so the longest
/// and least reversible part of the feature was indistinguishable from a hang.
/// </para>
/// <para>
/// <b>It reports the spool's position rather than a running total of its own.</b> The two agree
/// while the reads are sequential, which they are — but the position cannot drift, and a bar that
/// went backwards would be worse than no bar at all.
/// </para>
/// <para>
/// <b>What it measures is bytes handed to the network stack, not bytes the store acknowledged.</b>
/// Nothing on this side of the socket can know the second one, and the outcome is what says
/// whether the donation landed. So the bar reaching its end is "d47 has finished handing it over",
/// and the sentence beside it is still the thing that says what was stored.
/// </para>
/// <para>
/// <b>It does not own what it wraps.</b> Disposing this leaves the spool open, because the spool
/// belongs to <see cref="DonationDispatch"/> and is deleted on close by the file handle rather
/// than by anybody remembering to.
/// </para>
/// </summary>
internal sealed class MeteredStream : Stream
{
    private readonly Stream _inner;
    private readonly IProgress<long> _sent;

    /// <summary>How far it has to move before it is worth saying so again.</summary>
    private readonly long _notch;

    private long _said = -1;

    /// <param name="inner">The positioned, seekable spool. Its length is the denominator.</param>
    /// <param name="sent">Told how many bytes have gone, cumulatively.</param>
    public MeteredStream(Stream inner, IProgress<long> sent)
    {
        _inner = inner;
        _sent = sent;

        // A donation is read in buffers of a few kilobytes, so 32 MB is thousands of reads and
        // every one of them marshalled to a UI thread would cost more than the bar is worth. Two
        // hundred steps is finer than a bar three pixels tall can draw.
        _notch = Math.Max(64 * 1024, inner.Length / 200);
    }

    public override bool CanRead => _inner.CanRead;

    public override bool CanSeek => _inner.CanSeek;

    public override bool CanWrite => false;

    public override long Length => _inner.Length;

    public override long Position
    {
        get => _inner.Position;
        set => _inner.Position = value;
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        Said(_inner.Read(buffer, offset, count));

    public override int Read(Span<byte> buffer) => Said(_inner.Read(buffer));

    public override Task<int> ReadAsync(
        byte[] buffer, int offset, int count, CancellationToken cancel) =>
        ReadAsync(buffer.AsMemory(offset, count), cancel).AsTask();

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer, CancellationToken cancel = default) =>
        Said(await _inner.ReadAsync(buffer, cancel));

    public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);

    public override void Flush() => _inner.Flush();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    /// <summary>
    /// Passes a read straight back, having said where that leaves us.
    /// <para>
    /// <b>The end is always reported, whatever the notch.</b> A bar that stopped at ninety-nine
    /// percent because the last chunk was smaller than a step would say the send never finished,
    /// which is the one thing this is here to stop it saying.
    /// </para>
    /// </summary>
    private int Said(int read)
    {
        var at = _inner.Position;

        if (at != _said && (read == 0 || at >= _inner.Length || at - _said >= _notch))
        {
            _said = at;
            _sent.Report(at);
        }

        return read;
    }
}
