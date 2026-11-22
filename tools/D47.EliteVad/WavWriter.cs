namespace D47.EliteVad;

/// <summary>
/// Writes 16-bit PCM WAV files. Starts a new numbered part before a file passes the RIFF size limit,
/// and rewrites the header on <see cref="Checkpoint"/> so a killed run still leaves readable files.
/// </summary>
internal sealed class WavWriter : IDisposable
{
    private const long PartLimitBytes = 0xF000_0000;

    private readonly string _folder;
    private readonly string _stem;
    private readonly int _sampleRate;
    private readonly int _channels;
    private FileStream _stream = null!;
    private long _dataBytes;
    private int _part;

    public WavWriter(string folder, string stem, int sampleRate, int channels)
    {
        _folder = folder;
        _stem = stem;
        _sampleRate = sampleRate;
        _channels = channels;
        OpenPart();
    }

    /// <summary>Frames written to every part so far.</summary>
    public long FramesWritten { get; private set; }

    /// <summary>Called with the part's file name and its first frame when a new part opens.</summary>
    public Action<string, long>? PartOpened { get; set; }

    public void Write(ReadOnlySpan<byte> pcm)
    {
        if (_dataBytes + pcm.Length > PartLimitBytes)
        {
            ClosePart();
            OpenPart();
            PartOpened?.Invoke(Path.GetFileName(_stream.Name), FramesWritten);
        }

        _stream.Write(pcm);
        _dataBytes += pcm.Length;
        FramesWritten += pcm.Length / (2 * _channels);
    }

    public void WriteSilence(long frames)
    {
        Span<byte> zeros = stackalloc byte[4096];
        zeros.Clear();
        var bytes = frames * 2 * _channels;
        while (bytes > 0)
        {
            var n = (int)Math.Min(bytes, zeros.Length);
            Write(zeros[..n]);
            bytes -= n;
        }
    }

    public void Checkpoint()
    {
        WriteHeader();
        _stream.Flush();
    }

    public void Dispose() => ClosePart();

    private void OpenPart()
    {
        _part++;
        var name = _part == 1 ? $"{_stem}.wav" : $"{_stem}-{_part:00}.wav";
        _stream = new FileStream(Path.Combine(_folder, name), FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
        _dataBytes = 0;
        WriteHeader();
    }

    private void ClosePart()
    {
        WriteHeader();
        _stream.Dispose();
    }

    private void WriteHeader()
    {
        Span<byte> header = stackalloc byte[44];
        var blockAlign = 2 * _channels;
        "RIFF"u8.CopyTo(header);
        BitConverter.TryWriteBytes(header[4..], (uint)(36 + _dataBytes));
        "WAVE"u8.CopyTo(header[8..]);
        "fmt "u8.CopyTo(header[12..]);
        BitConverter.TryWriteBytes(header[16..], 16u);
        BitConverter.TryWriteBytes(header[20..], (ushort)1);
        BitConverter.TryWriteBytes(header[22..], (ushort)_channels);
        BitConverter.TryWriteBytes(header[24..], (uint)_sampleRate);
        BitConverter.TryWriteBytes(header[28..], (uint)(_sampleRate * blockAlign));
        BitConverter.TryWriteBytes(header[32..], (ushort)blockAlign);
        BitConverter.TryWriteBytes(header[34..], (ushort)16);
        "data"u8.CopyTo(header[36..]);
        BitConverter.TryWriteBytes(header[40..], (uint)_dataBytes);
        _stream.Position = 0;
        _stream.Write(header);
        _stream.Seek(0, SeekOrigin.End);
    }
}
