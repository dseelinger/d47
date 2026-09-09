namespace D47.App.Donation;

/// <summary>Counts bytes on their way out to the wire, and says so while they go (#212).</summary>
internal sealed class MeteredStream : Stream
{
    private readonly Stream _inner;
    private readonly IProgress<long> _sent;

    /// <summary>How far it has to move before it is worth saying so again.</summary>
    private readonly long _notch;

    private long _said = -1;

    /// <summary><param name="inner">The positioned, seekable spool.</summary>
    /// <param name="inner">The positioned, seekable spool.</param>
    /// <param name="sent">Told how many bytes have gone, cumulatively.</param>
    public MeteredStream(Stream inner, IProgress<long> sent)
    {
        _inner = inner;
        _sent = sent;

        // A donation is read in buffers of a few kilobytes, so 32 MB is thousands of reads and every one of
        // them marshalled to a UI thread would cost more than the bar is worth.
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

    /// <summary>Passes a read straight back, having said where that leaves us.</summary>
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
