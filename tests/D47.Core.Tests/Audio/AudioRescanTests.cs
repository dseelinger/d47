using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>Cues, beds and ambience discovered from the convention folders and reloaded live.</summary>
public class AudioRescanTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "d47-rescan-tests",
        Guid.NewGuid().ToString("n"));

    public AudioRescanTests() =>
        Directory.CreateDirectory(Path.Combine(_root, FolderAudioSource.BedsFolder));

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

    private FolderAudioSource Source() => new(_root, NullLogger<FolderAudioSource>.Instance);

    private CueLibrary Load(FolderAudioSource drops) =>
        CueLibrary.Load(null, new EmbeddedCueSource(typeof(CueLibrary).Assembly), drops);

    private string BedPath(string name) =>
        Path.Combine(_root, FolderAudioSource.BedsFolder, $"{name}.wav");

    [Fact]
    public void AFileAddedBeforeAScanAppearsInTheLibrary()
    {
        Assert.Equal(CueLibrary.DefaultBed, Load(Source()).Bed().Name);

        WriteWav(BedPath("engine-room"));

        Assert.Equal("engine-room", Load(Source()).Bed().Name);
    }

    [Fact]
    public void AFileRemovedBeforeAScanDisappears()
    {
        WriteWav(BedPath("engine-room"));
        Assert.Equal("engine-room", Load(Source()).Bed().Name);

        File.Delete(BedPath("engine-room"));

        Assert.Equal(CueLibrary.DefaultBed, Load(Source()).Bed().Name);
    }

    [Fact]
    public void AScanDoesNotChangeUnderAnEarlierOne()
    {
        WriteWav(BedPath("engine-room"));
        var first = Source();

        WriteWav(BedPath("hangar"));
        var second = Source();

        Assert.Equal(1, Load(first).CustomCount);
        Assert.Equal(2, Load(second).CustomCount);
    }

    [Fact]
    public void AReloadDuringPlaybackDoesNotStopIt()
    {
        WriteWav(BedPath("engine-room"));

        var library = Load(Source());

        var sink = new RecordingAudioSink();
        var arbiter = new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start();

        arbiter.Enqueue(new AudioRequest
        {
            Channel = AudioChannel.Bed,
            Clip = library.Bed(),
            Loop = true,
        });

        var playing = sink.Started[0].Id;
        Assert.Contains(playing, sink.Live);

        // The Commander drops another one in, and the library is rebuilt underneath.
        WriteWav(BedPath("hangar"));

        var reloaded = Load(Source());

        Assert.Equal(2, reloaded.CustomCount);
        Assert.NotSame(library, reloaded);

        // Still going, and never stopped.
        Assert.Contains(playing, sink.Live);
        Assert.Empty(sink.Stopped);
    }

    private static void WriteWav(string path, int samples = 240)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var data = samples * 2;

        using var file = File.Create(path);
        using var write = new BinaryWriter(file);

        write.Write("RIFF"u8.ToArray());
        write.Write(36 + data);
        write.Write("WAVE"u8.ToArray());
        write.Write("fmt "u8.ToArray());
        write.Write(16);
        write.Write((short)1);
        write.Write((short)1);
        write.Write(AudioFormat.Standard.SampleRate);
        write.Write(AudioFormat.Standard.SampleRate * 2);
        write.Write((short)2);
        write.Write((short)16);
        write.Write("data"u8.ToArray());
        write.Write(data);
        write.Write(new byte[data]);
    }
}
