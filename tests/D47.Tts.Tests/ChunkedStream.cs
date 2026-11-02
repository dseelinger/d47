namespace D47.Tts.Tests;

/// <summary>A read stream that hands back at most the next chunk size on each read.</summary>
internal sealed class ChunkedStream(byte[] body, int[] chunks, int? failAfter) : Stream
{
    private int _position;
    private int _reads;

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (failAfter is { } limit && _reads >= limit)
        {
            throw new IOException("the connection was reset");
        }

        var size = Math.Min(Math.Min(count, chunks[_reads % chunks.Length]), body.Length - _position);
        Array.Copy(body, _position, buffer, offset, size);
        _position += size;
        _reads++;
        return size;
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
