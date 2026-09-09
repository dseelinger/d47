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
    public void AFileAddedBetweenPollsAppearsInTheLibrary()
    {
        var drops = Source();

        Assert.DoesNotContain("engine-room", Load(drops).BedNames);

        WriteWav(BedPath("engine-room"));

        Assert.True(drops.Poll(), "the folder changed and the poll said it had not");
        Assert.Contains("engine-room", Load(drops).BedNames);
    }

    [Fact]
    public void AFileRemovedBetweenPollsDisappears()
    {
        WriteWav(BedPath("engine-room"));

        var drops = Source();
        Assert.Contains("engine-room", Load(drops).BedNames);

        File.Delete(BedPath("engine-room"));

        Assert.True(drops.Poll());
        Assert.DoesNotContain("engine-room", Load(drops).BedNames);
    }

    /// <summary>
    /// A file replaced in place is a change too, and the one a name-only comparison would miss — which
    /// is why the write times travel with the names.
    /// </summary>
    [Fact]
    public void AFileReplacedInPlaceIsNoticed()
    {
        WriteWav(BedPath("engine-room"));

        var drops = Source();
        Assert.False(drops.Poll());

        WriteWav(BedPath("engine-room"), samples: 480);
        File.SetLastWriteTimeUtc(BedPath("engine-room"), DateTime.UtcNow.AddMinutes(1));

        Assert.True(drops.Poll());
    }

    [Fact]
    public void AnUnchangedFolderDoesNoWork()
    {
        WriteWav(BedPath("engine-room"));

        var drops = Source();

        for (var i = 0; i < 20; i++)
        {
            Assert.False(drops.Poll(), "an unchanged folder reported a change");
        }

        Assert.Equal(0, drops.Rebuilds);
    }

    [Fact]
    public void AReloadDuringPlaybackDoesNotStopIt()
    {
        WriteWav(BedPath("engine-room"));

        var drops = Source();
        var library = Load(drops);

        var sink = new RecordingAudioSink();
        var arbiter = new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start();

        arbiter.Enqueue(new AudioRequest
        {
            Channel = AudioChannel.Bed,
            Clip = library.Bed("engine-room"),
            Loop = true,
        });

        var playing = sink.Started[0].Id;
        Assert.Contains(playing, sink.Live);

        // The Commander drops another one in, and the library is rebuilt underneath.
        WriteWav(BedPath("hangar"));
        Assert.True(drops.Poll());

        var reloaded = Load(drops);

        Assert.Contains("hangar", reloaded.BedNames);
        Assert.NotSame(library, reloaded);

        // Still going, and never stopped.
        Assert.Contains(playing, sink.Live);
        Assert.Empty(sink.Stopped);
    }

    /// <summary>A rebuild is counted, so the throttle above has something to assert against.</summary>
    [Fact]
    public void EveryRebuildIsCounted()
    {
        var drops = Source();

        WriteWav(BedPath("one"));
        Assert.True(drops.Poll());

        WriteWav(BedPath("two"));
        Assert.True(drops.Poll());

        Assert.Equal(2, drops.Rebuilds);
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
