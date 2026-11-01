using D47.Core.Audio;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>The music pack reads only folders named as a situation, so a download being unpacked plays nothing.</summary>
public class AMusicPackFolderBeingUnpackedIsNotReadTests
{
    private const string Root = @"C:\d47\data\music-pack";

    [Fact]
    public void APartialOrOldFolderIsNotReadAndASituationFolderIs()
    {
        var files = new MemoryFileSystem();
        files.WriteBytes(Path.Combine(Root, "docked", "a.wav"), [0]);
        files.WriteBytes(Path.Combine(Root, "docked.partial", "b.wav"), [0]);
        files.WriteBytes(Path.Combine(Root, "supercruise.old", "c.wav"), [0]);
        files.WriteText(Path.Combine(Root, "manifest.json"), "{}");

        var pack = new FolderAudioSource(Root, files, NullLogger.Instance, musicPack: true);

        Assert.Equal([CueLibrary.MusicPrefix + "docked.a"], pack.Names);
    }
}
