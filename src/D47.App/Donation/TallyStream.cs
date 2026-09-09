using System.Security.Cryptography;

namespace D47.App.Donation;

/// <summary>Counts and hashes bytes on their way past (#181).</summary>
internal sealed class TallyStream : Stream
{
    private readonly Stream _inner;
    private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private long _bytes;

    public TallyStream(Stream inner) => _inner = inner;

    /// <summary>How many bytes went past, counted rather than estimated.</summary>
    public long Bytes => _bytes;

    /// <summary>
    /// SHA-256 of everything that went past, lowercase hex — the one spelling the envelope, the receipt
    /// and the endpoint all use.
    /// </summary>
    public string Sha256 => Convert.ToHexStringLower(_hash.GetCurrentHash());

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => _bytes;

    public override long Position
    {
        get => _bytes;
        set => throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count) =>
        Write(buffer.AsSpan(offset, count));

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        _hash.AppendData(buffer);
        _bytes += buffer.Length;
        _inner.Write(buffer);
    }

    public override void WriteByte(byte value) => Write([value]);

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancel) =>
        WriteAsync(buffer.AsMemory(offset, count), cancel).AsTask();

    public override async ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer, CancellationToken cancel = default)
    {
        _hash.AppendData(buffer.Span);
        _bytes += buffer.Length;
        await _inner.WriteAsync(buffer, cancel);
    }

    public override void Flush() => _inner.Flush();

    public override Task FlushAsync(CancellationToken cancel) => _inner.FlushAsync(cancel);

    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) =>
        throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hash.Dispose();
        }

        base.Dispose(disposing);
    }
}
