using System.Security.Cryptography;

namespace D47.App.Donation;

/// <summary>
/// Counts and hashes bytes on their way past
/// (<a href="https://github.com/dseelinger/d47/issues/181">#181</a>).
/// <para>
/// <b>This is the hashing decision, and it is settled by a hard constraint rather than by
/// taste.</b> #181 offered two roads for a payload that is deliberately never held whole: hash in
/// one pass and stream in a second, or spool and hash the spool. The endpoint refuses a donation
/// that does not declare its length, a compressed length cannot be known without compressing, and
/// the journals are hundreds of megabytes — so the compressed bytes have to land somewhere
/// seekable before the POST whichever road is taken. Once there is a spool, a second walk over the
/// journals to hash is pure cost. So the payload is hashed <b>as it is written</b>, by this, and
/// the corpus is read exactly once for the send.
/// </para>
/// <para>
/// <b>Over the payload, not over the wire.</b> This sits above the <c>GZipStream</c> rather than
/// below it, so what it hashes is the scrubbed text — the bytes a donor can reproduce and check
/// with an ordinary <c>sha256sum</c> after a gunzip. A hash over compressed output would prove the
/// transfer and nothing anybody cares about, because gzip output is not reproducible from its
/// input across levels and implementations.
/// </para>
/// <para>
/// <b>It does not own what it wraps.</b> Disposing this disposes the hash and nothing else: the
/// chain below it is the caller's, and closing a <c>GZipStream</c> early would write its footer
/// before the caller had finished with the file underneath.
/// </para>
/// </summary>
internal sealed class TallyStream : Stream
{
    private readonly Stream _inner;
    private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private long _bytes;

    public TallyStream(Stream inner) => _inner = inner;

    /// <summary>How many bytes went past, counted rather than estimated.</summary>
    public long Bytes => _bytes;

    /// <summary>
    /// SHA-256 of everything that went past, lowercase hex — the one spelling the envelope, the
    /// receipt and the endpoint all use.
    /// <para>
    /// Read rather than finished: <see cref="IncrementalHash.GetCurrentHash()"/> leaves the hash
    /// able to keep going, so asking twice is not a way of getting two different answers.
    /// </para>
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
