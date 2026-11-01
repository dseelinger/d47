using D47.Core.Audio;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>A music folder with files of the Commander's plays those and not the downloaded pack's.</summary>
public class AMusicPackIsReadBeneathOwnMusicTests
{
    [Fact]
    public void AFolderWithTracksInBothPlaysOnlyTheLaterSource()
    {
        var library = Load(
            new Music(isPack: true, "docked.pack-a", "docked.pack-b"),
            new Music(isPack: false, "docked.mine"));

        Assert.Equal(["mine"], library.Music("docked").Select(track => track.Name));
    }

    [Fact]
    public void AFolderWithTracksOnlyInThePackPlaysThePacks()
    {
        var library = Load(
            new Music(isPack: true, "docked.pack-a", "supercruise.pack-b"),
            new Music(isPack: false, "docked.mine"));

        Assert.Equal(["pack-b"], library.Music("supercruise").Select(track => track.Name));
        Assert.Equal(["mine"], library.Music("docked").Select(track => track.Name));
    }

    [Fact]
    public void PackTracksAreNotCountedInTheFolderCounts()
    {
        var library = Load(new Music(isPack: true, "docked.pack-a", "supercruise.pack-b"));

        Assert.Empty(library.FolderCounts);
        Assert.Equal(0, library.CustomCount);
        Assert.Equal(@"Nothing in data\audio yet.", library.DescribeDrops());
    }

    private static CueLibrary Load(params Music[] sources) =>
        CueLibrary.Load(null, [new EmbeddedCueSource(typeof(CueLibrary).Assembly), .. sources]);

    private sealed class Music(bool isPack, params string[] names) : ICueSource
    {
        public IEnumerable<string> Names => names.Select(name => CueLibrary.MusicPrefix + name);

        public bool Required => false;

        public bool IsPack => isPack;

        public Stream Open(string name) => throw new NotSupportedException();

        public IPcmStream Stream(string name, string clipName) => new Silence();
    }

    private sealed class Silence : IPcmStream
    {
        public int Read(Span<byte> buffer) => 0;

        public void Dispose()
        {
        }
    }
}
