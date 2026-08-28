using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>
/// Drop your own audio in and d47 uses it, kept distinct from the set it ships with
/// (Phase 12, "Custom Sound Cues").
/// </summary>
public class FolderAudioSourceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "d47-audio-tests",
        Guid.NewGuid().ToString("n"));

    public FolderAudioSourceTests()
    {
        foreach (var folder in new[]
                 {
                     FolderAudioSource.CuesFolder,
                     FolderAudioSource.BedsFolder,
                     FolderAudioSource.MusicFolder,
                 })
        {
            Directory.CreateDirectory(Path.Combine(_root, folder));
        }
    }

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

    /// <summary>
    /// A real wav in the format d47 uses, written by hand so the test does not depend on an
    /// asset it would then also be testing.
    /// </summary>
    private void Write(string relative, int sampleRate = 48_000, short channels = 1, short bits = 16)
    {
        var path = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        const int Samples = 240;
        var data = Samples * channels * (bits / 8);

        using var file = File.Create(path);
        using var write = new BinaryWriter(file);

        write.Write("RIFF"u8.ToArray());
        write.Write(36 + data);
        write.Write("WAVE"u8.ToArray());
        write.Write("fmt "u8.ToArray());
        write.Write(16);
        write.Write((short)1);
        write.Write(channels);
        write.Write(sampleRate);
        write.Write(sampleRate * channels * (bits / 8));
        write.Write((short)(channels * (bits / 8)));
        write.Write(bits);
        write.Write("data"u8.ToArray());
        write.Write(data);
        write.Write(new byte[data]);
    }

    private FolderAudioSource Source() =>
        new(_root, NullLogger<FolderAudioSource>.Instance);

    private CueLibrary Load() =>
        CueLibrary.Load(null, new EmbeddedCueSource(typeof(CueLibrary).Assembly), Source());

    /// <summary>The shipped set alone still satisfies everything it did before.</summary>
    [Fact]
    public void AnEmptyFolderChangesNothing()
    {
        var library = Load();

        Assert.Equal(0, library.CustomCount);
        Assert.Empty(library.Skipped);
        Assert.Contains(CueLibrary.DefaultBed, library.BedNames);
    }

    [Fact]
    public void AFolderCueOverridesTheEmbeddedOneOfTheSameName()
    {
        var shipped = CueLibrary.Load().For(LoopState.Thinking);

        Write($"{FolderAudioSource.CuesFolder}/thinking.wav");

        var library = Load();

        Assert.NotEqual(shipped.Pcm.Length, library.For(LoopState.Thinking).Pcm.Length);
        Assert.True(library.IsCustom("thinking"), "an overridden cue is not marked as the Commander's");
    }

    [Fact]
    public void AFolderBedAppearsInTheRowsChoices()
    {
        Write($"{FolderAudioSource.BedsFolder}/engine-room.wav");

        var library = Load();

        Assert.Contains("engine-room", library.BedNames);
        Assert.True(library.IsCustom("engine-room"));

        // And the shipped ones are still there and still not theirs.
        Assert.Contains(CueLibrary.DefaultBed, library.BedNames);
        Assert.False(library.IsCustom(CueLibrary.DefaultBed));
    }

    /// <summary>
    /// A Commander's file that will not load is a file to tell them about, not a reason d47 does
    /// not start. The shipped set failing is the opposite, and that difference is the point.
    /// </summary>
    [Fact]
    public void AMalformedDropInIsSkippedAndNamedRatherThanTakingTheLibraryDown()
    {
        Write($"{FolderAudioSource.BedsFolder}/stereo-bed.wav", channels: 2);
        Write($"{FolderAudioSource.BedsFolder}/wrong-rate.wav", sampleRate: 44100);

        var library = Load();

        Assert.DoesNotContain("stereo-bed", library.BedNames);
        Assert.DoesNotContain("wrong-rate", library.BedNames);

        Assert.Equal(2, library.Skipped.Count);
        Assert.Contains(library.Skipped, reason => reason.Contains("stereo-bed", StringComparison.Ordinal));
        Assert.Contains(library.Skipped, reason => reason.Contains("44100", StringComparison.Ordinal));
    }

    /// <summary>A cue named for no loop state is the same kind of mistake, and the same answer.</summary>
    [Fact]
    public void ACueNamedForNothingIsReportedRatherThanFatal()
    {
        Write($"{FolderAudioSource.CuesFolder}/pondering.wav");

        var library = Load();

        Assert.Single(library.Skipped);
        Assert.Contains("pondering", library.Skipped[0], StringComparison.Ordinal);

        // And it names what would have worked, because "no" without "instead try" is a dead end.
        Assert.Contains("thinking", library.Skipped[0], StringComparison.Ordinal);
    }

    /// <summary>
    /// A file loose in music/ has not said when it should play. Left alone rather than guessed
    /// at — a track assigned to a situation nobody chose is worse than a track that never plays.
    /// </summary>
    [Fact]
    public void AFileWithNoSituationFolderIsNotPickedUp()
    {
        Write($"{FolderAudioSource.MusicFolder}/loose.wav");

        Assert.DoesNotContain(Source().Names, name => name.Contains("loose", StringComparison.Ordinal));
    }

    /// <summary>The three folders exist whether or not anything is in them.</summary>
    [Fact]
    public void TheConventionFoldersAreMadeAtStartup()
    {
        var install = Path.Combine(_root, "install");
        var paths = new AppPaths(install);

        paths.EnsureCreated();

        foreach (var folder in new[]
                 {
                     FolderAudioSource.CuesFolder,
                     FolderAudioSource.BedsFolder,
                     FolderAudioSource.MusicFolder,
                 })
        {
            Assert.True(
                Directory.Exists(Path.Combine(paths.Audio, folder)),
                $"data/audio/{folder} was not created, so the convention is invisible");
        }
    }
}
