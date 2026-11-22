using Xunit;

namespace D47.Vr.Tests;

/// <summary>
/// A decoded picture's fourth byte is unused, and a quad blends by it, so the presenter's copy sets it to 255 and
/// then scales every channel by the opacity. The decoder's own buffer is left as it was.
/// </summary>
public class TheVideoCopyIsOpaqueAndPremultipliedTests
{
    [Fact]
    public void TheUnusedByteIsSetOpaque()
    {
        byte[] from = [0x10, 0x20, 0x30, 0x00, 0x40, 0x50, 0x60, 0x7F];
        var to = new byte[from.Length];

        VrVideoCopy.Opaque(from, to, 1f);

        Assert.Equal([0x10, 0x20, 0x30, 0xFF, 0x40, 0x50, 0x60, 0xFF], to);
        Assert.Equal(0x00, from[3]);
    }

    [Fact]
    public void BelowFullOpacityEveryChannelIsScaled()
    {
        byte[] from = [0x80, 0x40, 0xFE, 0x00];
        var to = new byte[from.Length];

        VrVideoCopy.Opaque(from, to, 0.5f);

        Assert.Equal([0x40, 0x20, 0x7F, 0x7F], to);
    }

    [Fact]
    public void APictureIsCopiedOnceUntilItOrTheOpacityChanges()
    {
        var copy = new VrVideoCopy();
        var video = new FakeVideo();
        var picture = video.Show(2, 1);

        Assert.True(copy.Take(picture, 1f));
        Assert.False(copy.Take(picture, 1f));
        Assert.True(copy.Take(picture, 0.5f));
        Assert.True(copy.Take(video.Show(2, 1), 0.5f));
        Assert.Equal((2, 1, 8), (copy.Width, copy.Height, copy.RowBytes));
    }

    [Fact]
    public void NoPictureOrAShortBufferIsNotCopied()
    {
        var copy = new VrVideoCopy();

        Assert.False(copy.Take(null, 1f));
        Assert.False(copy.Take(new VrPicture(new object(), new byte[4], 2, 1), 1f));
        Assert.Equal(0, copy.Width);
    }
}
