using D47.Core.Storage;
using Xunit;
using D47.Core.Audio;
using D47.Core.Interface;

namespace D47.Core.Tests;

public class AnOwnCoreReadsItsClipsFromCustomTests
{
    [Fact]
    public void AnOwnCoreReadsCustom()
    {
        Assert.Equal("custom.thinking.mp4", CoreClips.FileName("own.my-core", LoopState.Thinking));
    }

    [Fact]
    public void AStockCoreReadsItsOwnId()
    {
        Assert.Equal("covas.speaking.mp4", CoreClips.FileName("covas", LoopState.Speaking));
    }

    [Fact]
    public void OnlyANonEmptyFileIsOffered()
    {
        var files = new MemoryFileSystem();
        const string folder = "C:/d47-test/clips";

        Assert.Null(CoreClips.For(files, folder, "covas", LoopState.Idle));

        var path = Path.Combine(folder, CoreClips.FileName("covas", LoopState.Idle));
        files.WriteBytes(path, []);
        Assert.Null(CoreClips.For(files, folder, "covas", LoopState.Idle));

        files.WriteBytes(path, [1]);
        Assert.Equal(path, CoreClips.For(files, folder, "covas", LoopState.Idle));
    }
}
