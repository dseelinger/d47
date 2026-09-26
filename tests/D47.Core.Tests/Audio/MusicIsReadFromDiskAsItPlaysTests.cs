using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>Ambience tracks stay on disk until they play, and are read a buffer at a time.</summary>
public class MusicIsReadFromDiskAsItPlaysTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "d47-music-stream-tests",
        Guid.NewGuid().ToString("n"));

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // A temp folder that will not delete is not a test failure.
        }
    }

    /// <summary>The library holds a reference, so what plays is the file as it is when the track opens.</summary>
    [Fact]
    public void ATrackReadsTheFileAsItIsWhenItOpens()
    {
        var path = Write("general", "drift", Pcm(1, 2, 3, 4));
        var library = Load();

        File.WriteAllBytes(path, WavWriter.ToBytes(Pcm(9, 8, 7, 6), AudioFormat.Standard));

        using var stream = Assert.Single(library.Music(Situations.General)).Open();

        Assert.Equal(Pcm(9, 8, 7, 6), ReadAll(stream));
    }

    [Fact]
    public void AStreamHandsBackWholeFramesAndThenZero()
    {
        using var stream = WavReader.Open(
            new MemoryStream(WavWriter.ToBytes(Pcm(1, 2, 3), AudioFormat.Standard)),
            "three");

        var buffer = new byte[3];

        Assert.Equal(2, stream.Read(buffer));
        Assert.Equal(2, stream.Read(buffer));
        Assert.Equal(2, stream.Read(buffer));
        Assert.Equal(0, stream.Read(buffer));
    }

    /// <summary>The header is still checked at load, so a bad drop-in is reported rather than tried.</summary>
    [Fact]
    public void AMusicFileThatIsNotAWavIsSkipped()
    {
        var path = Path.Combine(_root, FolderAudioSource.MusicFolder, Situations.General, "noise.wav");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "not audio");

        var library = Load();

        Assert.Empty(library.Music(Situations.General));
        Assert.Contains(library.Skipped, reason => reason.StartsWith("noise:", StringComparison.Ordinal));
    }

    [Fact]
    public void AMusicFileAtTheWrongRateIsSkipped()
    {
        Write("general", "slow", Pcm(1, 2), new AudioFormat(44_100, 1));

        var library = Load();

        Assert.Empty(library.Music(Situations.General));
        Assert.Contains(library.Skipped, reason => reason.Contains("44100 Hz", StringComparison.Ordinal));
    }

    /// <summary>A file removed after the folder was read fails when the track opens, which ends that track.</summary>
    [Fact]
    public void ATrackWhoseFileHasGoneFailsToOpen()
    {
        var path = Write("general", "gone", Pcm(1, 2));
        var library = Load();

        File.Delete(path);

        Assert.ThrowsAny<IOException>(() => Assert.Single(library.Music(Situations.General)).Open());
    }

    [Fact]
    public void TheArbiterSendsMusicToTheSinkAsAStream()
    {
        var sink = new RecordingAudioSink();
        using var arbiter = new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start();
        var track = new MusicTrack("drift", () => throw new InvalidOperationException("not opened by the arbiter"));

        arbiter.PlayMusic(track);

        var request = Assert.Single(sink.Started);
        Assert.Same(track, request.Track);
        Assert.Null(request.Clip);
        Assert.False(request.Loop);
    }

    private CueLibrary Load() =>
        CueLibrary.Load(
            null,
            new EmbeddedCueSource(typeof(CueLibrary).Assembly),
            new FolderAudioSource(_root, NullLogger<FolderAudioSource>.Instance));

    private string Write(string situation, string track, byte[] pcm, AudioFormat? format = null)
    {
        var path = Path.Combine(_root, FolderAudioSource.MusicFolder, situation, $"{track}.wav");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, WavWriter.ToBytes(pcm, format ?? AudioFormat.Standard));

        return path;
    }

    private static byte[] Pcm(params short[] samples)
    {
        var bytes = new byte[samples.Length * 2];

        for (var i = 0; i < samples.Length; i++)
        {
            bytes[i * 2] = (byte)samples[i];
            bytes[(i * 2) + 1] = (byte)(samples[i] >> 8);
        }

        return bytes;
    }

    private static byte[] ReadAll(IPcmStream stream)
    {
        var all = new List<byte>();
        var buffer = new byte[4];
        int read;

        while ((read = stream.Read(buffer)) > 0)
        {
            all.AddRange(buffer[..read]);
        }

        return [.. all];
    }
}
