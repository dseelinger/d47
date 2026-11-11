using D47.Core.Interface;
using Xunit;

namespace D47.Core.Tests.Interface;

public class AHeadsetPictureIsLiftedAndTheLiftIsKeptTests
{
    [Fact]
    public void TheGammaTableKeepsTheEndsAndRaisesTheMiddle()
    {
        var table = PictureLift.Table(PictureLift.HeadsetGamma);

        Assert.Equal(0, table[0]);
        Assert.Equal(255, table[255]);
        Assert.Equal(64, table[16]);
        Assert.True(table[60] > 60);
    }

    [Fact]
    public void OnlyAHeadsetCaptureIsLifted()
    {
        Assert.Equal(0.5, PictureLift.For(ScreenPictures.FromHeadset));
        Assert.Null(PictureLift.For("Elite's window"));
        Assert.Null(PictureLift.For(null));
    }

    [Fact]
    public void ACropFileKeepsTheLiftBesideTheSquare()
    {
        var square = new CropSquare(10, 20, 500);
        var text = PictureLift.Write(square, 0.5);

        Assert.Equal(0.5, PictureLift.Parse(text));
        Assert.Equal(square, CropSquare.Parse(text));
    }

    [Fact]
    public void ACropFileWithoutALiftReadsAsNoLift()
    {
        var square = new CropSquare(10, 20, 500);

        Assert.Null(PictureLift.Parse("{\"x\":10,\"y\":20,\"side\":500}"));
        Assert.Null(PictureLift.Parse(PictureLift.Write(square, null)));
        Assert.Null(PictureLift.Parse("not json"));
        Assert.Equal(square, CropSquare.Parse(PictureLift.Write(square, null)));
    }
}
