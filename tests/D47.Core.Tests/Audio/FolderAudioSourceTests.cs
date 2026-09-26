using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>Drop your own audio in and d47 uses it, kept distinct from the set it ships with.</summary>
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
    /// A real wav in the format d47 uses, written by hand so the test does not depend on an asset it
    /// would then also be testing.
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
        Assert.Equal(CueLibrary.DefaultBed, library.Bed().Name);
    }

    [Fact]
    public void AFileInAStatesFolderReplacesItsShippedCue()
    {
        Write($"{FolderAudioSource.CuesFolder}/thinking/any-name.wav");

        var library = Load();

        Assert.Equal("any-name", library.For(LoopState.Thinking).Name);
        Assert.Equal(1, library.CustomCount);
    }

    [Fact]
    public void AFileLooseInTheCuesFolderIsNotRead()
    {
        Write($"{FolderAudioSource.CuesFolder}/thinking.wav");
        Write($"{FolderAudioSource.AlertsFolder}/underfire.wav");

        Assert.Empty(Source().Names);
        Assert.Equal("thinking", Load().For(LoopState.Thinking).Name);
    }

    /// <summary>A folder named for no loop state is reported rather than fatal.</summary>
    [Fact]
    public void ACueFolderNamedForNothingIsReportedRatherThanFatal()
    {
        Write($"{FolderAudioSource.CuesFolder}/pondering/hmm.wav");

        var library = Load();

        Assert.Single(library.Skipped);
        Assert.Contains("pondering", library.Skipped[0], StringComparison.Ordinal);

        // And it names what would have worked.
        Assert.Contains("thinking", library.Skipped[0], StringComparison.Ordinal);
    }

    [Fact]
    public void AnAlertFolderNamedForNothingNamesTheKebabCaseFolders()
    {
        Write($"{FolderAudioSource.AlertsFolder}/ambush/boom.wav");

        Assert.Contains("bounty-hunter", Assert.Single(Load().Skipped), StringComparison.Ordinal);
    }

    /// <summary>A file loose in music/ has not said when it should play.</summary>
    [Fact]
    public void AFileWithNoSituationFolderIsNotPickedUp()
    {
        Write($"{FolderAudioSource.MusicFolder}/loose.wav");

        Assert.DoesNotContain(Source().Names, name => name.Contains("loose", StringComparison.Ordinal));
    }

    /// <summary>The folders exist whether or not anything is in them.</summary>
    [Fact]
    public void TheConventionFoldersAreMadeAtStartup()
    {
        var install = Path.Combine(_root, "install");
        var paths = new AppPaths(install);

        paths.EnsureCreated();

        string[] folders =
        [
            .. Enum.GetValues<LoopState>().Select(state => $"{FolderAudioSource.CuesFolder}/{CueLibrary.FolderName(state)}"),
            .. Enum.GetValues<AlertCue>().Select(alert => $"{FolderAudioSource.AlertsFolder}/{CueLibrary.FolderName(alert)}"),
            FolderAudioSource.BedsFolder,
            FolderAudioSource.MusicFolder,
        ];

        foreach (var folder in folders)
        {
            Assert.True(
                Directory.Exists(Path.Combine(paths.Audio, folder)),
                $"data/audio/{folder} was not created, so the convention is invisible");
        }

        Assert.True(Directory.Exists(Path.Combine(paths.Audio, "alerts", "under-fire")));
        Assert.True(Directory.Exists(Path.Combine(paths.Audio, "cues", "listening")));
    }
}
