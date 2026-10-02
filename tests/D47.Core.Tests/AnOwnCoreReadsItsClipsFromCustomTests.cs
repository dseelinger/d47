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
        var folder = Path.Combine(Path.GetTempPath(), "d47-core-clips-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        try
        {
            Assert.Null(CoreClips.For(folder, "covas", LoopState.Idle));

            var path = Path.Combine(folder, CoreClips.FileName("covas", LoopState.Idle));
            File.WriteAllBytes(path, []);
            Assert.Null(CoreClips.For(folder, "covas", LoopState.Idle));

            File.WriteAllBytes(path, [1]);
            Assert.Equal(path, CoreClips.For(folder, "covas", LoopState.Idle));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
