using D47.Core.Interface;
using Xunit;

namespace D47.Core.Tests.Interface;

/// <summary>A picture is scaled down to at most 1568 pixels on the long edge and 1.15 megapixels, never up.</summary>
public class AScreenPictureFitsWhatTheModelSeesTests
{
    [Theory]
    [InlineData(3840, 2160)]
    [InlineData(2560, 1440)]
    [InlineData(1920, 1080)]
    [InlineData(5120, 1440)]
    [InlineData(1600, 1600)]
    [InlineData(1728, 1792)]
    [InlineData(1567, 9000)]
    public void BothCapsHold(int width, int height)
    {
        var (fitWidth, fitHeight) = ScreenPictures.Fit(width, height);

        Assert.InRange(Math.Max(fitWidth, fitHeight), 1, ScreenPictures.MaxLongEdge);
        Assert.InRange((long)fitWidth * fitHeight, 1, ScreenPictures.MaxPixels);
        Assert.InRange(Math.Abs(((double)fitWidth / fitHeight) - ((double)width / height)), 0, 0.01 * width / height);
    }

    [Fact]
    public void AWideScreenIsHeldByThePixelCap()
    {
        var (width, height) = ScreenPictures.Fit(3840, 2160);

        Assert.True(width < ScreenPictures.MaxLongEdge, $"{width}x{height}");
        Assert.True((long)width * height > ScreenPictures.MaxPixels * 99 / 100, $"{width}x{height}");
    }

    [Fact]
    public void ASmallPictureIsNotScaledUp() =>
        Assert.Equal((800, 600), ScreenPictures.Fit(800, 600));

    [Fact]
    public void TheLensCropIsTheCentredEightyPercent() =>
        Assert.Equal((200, 100, 1600, 800), ScreenPictures.LensCrop(2000, 1000));
}
